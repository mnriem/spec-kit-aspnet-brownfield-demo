# Data Model: Docker Compose Developer Infrastructure

**Feature**: `001-docker-compose-dev-infra`  
**Phase**: 1 — Design  
**Date**: 2026-03-05

> This feature introduces no new C# model classes or EF Core entities.
> The "data model" here describes the configuration entities, file artefacts, and
> service topology that collectively constitute the developer infrastructure.

---

## 1. Configuration Entity: Environment Variables

These are the authoritative variables a developer sets in `.env`. They form the
primary interface between the host environment and the Docker infrastructure.

| Variable | Required | Default | Description |
|---|---|---|---|
| `SA_PASSWORD` | **Yes** | — | SQL Server SA account password. Must meet SQL Server complexity rules (≥8 chars, mix of upper/lower/digit/symbol). |
| `SQL_PORT` | No | `1433` | Host port mapped to SQL Server's container port 1433. Change when a local SQL Server already occupies 1433. |
| `ConnectionStrings__CarrotwareCMS` | No | _(falls back to appsettings.json)_ | ASP.NET Core override for the CarrotCoreMVC connection string. |
| `ConnectionStrings__NorthwindConnection` | No | _(falls back to appsettings.json)_ | ASP.NET Core override for the Northwind connection string. |

**State transitions**:
- Variable absent → `docker compose up` fails immediately with a descriptive error (`SA_PASSWORD` only; optional vars are silently defaulted).
- Variable present → passed to container environment at startup.
- `SA_PASSWORD` does not meet complexity → `sqlserver` container starts then exits; visible via `docker compose logs sqlserver`.

---

## 2. Service Entity: sqlserver

The primary infrastructure service providing the SQL Server engine.

| Attribute | Value |
|---|---|
| Image | `mcr.microsoft.com/mssql/server:2022-developer-latest` |
| Platform | `linux/amd64` (required for ARM64/Apple Silicon; Rosetta 2 emulation) |
| Container name | `carrotcake-sqlserver` |
| Host port | `${SQL_PORT:-1433}` → container port `1433` |
| Named volume mount | `carrotcake-sqldata:/var/opt/mssql` |
| Required env | `SA_PASSWORD`, `ACCEPT_EULA=Y`, `MSSQL_PID=Developer` |
| Health check | `sqlcmd SELECT 1` on `localhost` with SA credentials (see R-004) |
| Restart policy | `unless-stopped` |

**Volume lifecycle**:
- Created: first `docker compose up` when `carrotcake-sqldata` does not exist.
- Preserved: `docker compose down` (no flags).
- Destroyed: `docker compose down --volumes` — triggers full re-initialisation on next `up`.

---

## 3. Service Entity: db-init

A one-shot service that runs database initialisation scripts after the SQL Server engine is healthy.

| Attribute | Value |
|---|---|
| Image | `mcr.microsoft.com/mssql/server:2022-developer-latest` (reuses SQL Server image for `sqlcmd`) |
| Platform | `linux/amd64` |
| Depends on | `sqlserver` with `condition: service_healthy` |
| Restart policy | `no` (runs once per `docker compose up`) |
| Entrypoint | `/bin/bash /docker/init-db.sh` |
| Read-only mounts | `./CMSDataScripts:/scripts/carrot:ro`, `./Northwind:/scripts/northwind:ro`, `./docker/init-db.sh:/docker/init-db.sh:ro` |
| Required env | `SA_PASSWORD` |

**Idempotency**:
- Checks `sys.databases` for `CarrotCoreMVC` before running CarrotCake scripts.
- Checks `sys.databases` for `Northwind` before running Northwind scripts.
- Both guards ensure the init container is a no-op on subsequent `docker compose up` calls against an existing volume.

---

## 4. File Artefact: compose.yaml (repository root)

The Docker Compose file defining the service topology.

| Field | Value |
|---|---|
| Location | `/compose.yaml` (repository root) |
| Compose spec version | No `version:` key — per Compose Specification v1.0, the `version` top-level key is obsolete |
| Services | `sqlserver`, `db-init` |
| Volumes | `carrotcake-sqldata` (named, driver: local) |
| Networks | Default bridge (no explicit network declaration needed for 2-service setup) |

---

## 5. File Artefact: .env.example (repository root)

Committed template for developer environment setup.

```dotenv
# Required – SQL Server SA password
# Must meet SQL Server complexity requirements:
#   - At least 8 characters
#   - Mix of uppercase, lowercase, digits, and non-alphanumeric characters
SA_PASSWORD=<your-strong-password-here>

# Optional – host port for SQL Server (default: 1433)
# Change this if port 1433 is already occupied by a local SQL Server installation.
SQL_PORT=1433

# Optional – ASP.NET Core connection string overrides
# These override appsettings.json when set, enabling use of the Docker SQL Server
# instance without modifying any source-controlled file.
# Replace <SA_PASSWORD> with the same value as SA_PASSWORD above.
ConnectionStrings__CarrotwareCMS=Server=localhost,1433;Database=CarrotCoreMVC;User Id=sa;Password=<your-strong-password-here>;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;
ConnectionStrings__NorthwindConnection=Server=localhost,1433;Database=Northwind;User Id=sa;Password=<your-strong-password-here>;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;
```

---

## 6. File Artefact: docker/init-db.sh

The shell script run by the `db-init` service. Mounted read-only into the container.

**Logic flow**:
```
1. Wait (sqlserver healthy — guaranteed by depends_on condition)
2. Check sys.databases for 'CarrotCoreMVC'
3. IF absent:
   a. CREATE DATABASE [CarrotCoreMVC]
   b. Execute CarrotCoreMVC table scripts in FK-dependency order (26 files, R-006)
   c. Execute all view scripts (alphabetical)
   d. Execute all stored procedure scripts (alphabetical)
4. Check sys.databases for 'Northwind'
5. IF absent:
   a. CREATE DATABASE [Northwind]
   b. Execute Northwind/northwind.sql (opens with USE [Northwind])
6. EXIT 0
```

**Key properties**:
- Uses `/opt/mssql-tools18/bin/sqlcmd` (bundled in the SQL Server 2022 image).
- `-No` flag disables TLS certificate validation for localhost connections.
- All `sqlcmd` calls use `-b` (abort batch on error) so FK violations or syntax errors surface immediately.
- Each script runs in an explicit `-d CarrotCoreMVC` context (except Northwind which relies on `USE [Northwind]`).

---

## 7. File Artefact: .gitignore (repository root)

New file (does not currently exist). Ensures `.env` is never committed.

**Minimum required entries**:
```
.env
```

**Recommended additions** (standard .NET / Visual Studio ignores):
```
.env
*.user
*.suo
*.swp
[Bb]in/
[Oo]bj/
.vs/
```

---

## 8. Named Volume: carrotcake-sqldata

Persistent storage for the SQL Server data directory `/var/opt/mssql`.

| Attribute | Value |
|---|---|
| Name | `carrotcake-sqldata` |
| Driver | `local` (default) |
| Scope | Project-level (prefixed by Compose project name in Docker) |
| Created by | `docker compose up` on first run |
| Removed by | `docker compose down --volumes` |
| Contents on first run | Empty → populated by SQL Server + db-init |
| Contents on restart | Preserved databases and developer data |
