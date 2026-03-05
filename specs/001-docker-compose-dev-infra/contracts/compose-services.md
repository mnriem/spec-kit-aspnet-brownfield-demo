# Contract: Docker Compose Services

**Feature**: `001-docker-compose-dev-infra`  
**Contract Type**: Docker Compose service interface  
**Version**: 1.0  
**Date**: 2026-03-05

---

## Overview

The `compose.yaml` at the repository root defines two services: `sqlserver` (the database
engine) and `db-init` (the one-shot initialisation service). This document specifies each
service's interface, dependencies, and expected lifecycle behaviour.

---

## Service: sqlserver

### Purpose
Runs SQL Server 2022 Developer Edition and exposes it on the host for use by the
`dotnet run`-hosted ASP.NET Core application.

### Image
```
mcr.microsoft.com/mssql/server:2022-developer-latest
```
Platform: `linux/amd64` (required; see R-002)

### Ports

| Host | Container | Variable |
|---|---|---|
| `${SQL_PORT:-1433}` | `1433` | `SQL_PORT` env var; defaults to `1433` |

### Required Environment Variables

| Variable | Value |
|---|---|
| `ACCEPT_EULA` | `Y` |
| `MSSQL_PID` | `Developer` |
| `SA_PASSWORD` | `${SA_PASSWORD:?...}` (required; fails fast if absent) |

### Volumes

| Mount | Purpose |
|---|---|
| `carrotcake-sqldata:/var/opt/mssql` | Persistent SQL Server data directory |

### Health Check

```yaml
test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$SA_PASSWORD\" -Q 'SELECT 1' -No || exit 1"]
interval: 10s
timeout: 5s
retries: 12
start_period: 30s
```

The service is declared `healthy` when `SELECT 1` succeeds. The `db-init` service
will not start until this condition is met.

### Restart Policy
`unless-stopped` — restarts automatically on Docker Desktop restart; does not restart
after explicit `docker compose stop` or `docker compose down`.

### Container Name
`carrotcake-sqlserver`

---

## Service: db-init

### Purpose
One-shot service. Runs database initialisation SQL scripts once per volume lifetime.
Exits code `0` on success, non-zero on failure.

### Image
```
mcr.microsoft.com/mssql/server:2022-developer-latest
```
Platform: `linux/amd64` (same image as `sqlserver`; no additional pull required)

### Dependencies

```yaml
depends_on:
  sqlserver:
    condition: service_healthy
```

`db-init` will not start until `sqlserver` reports `healthy`.

### Entrypoint / Command

```yaml
entrypoint: ["/bin/bash", "/docker/init-db.sh"]
```

### Required Environment Variables

| Variable | Value |
|---|---|
| `SA_PASSWORD` | `${SA_PASSWORD:?...}` (required; same password as `sqlserver`) |

### Volume Mounts (read-only)

| Host path | Container path | Purpose |
|---|---|---|
| `./CMSDataScripts` | `/scripts/carrot` | CarrotCoreMVC schema SQL files |
| `./Northwind` | `/scripts/northwind` | Northwind initialisation SQL file |
| `./docker/init-db.sh` | `/docker/init-db.sh` | The init shell script |

### Restart Policy
`no` — must not restart. After successful initialisation the container exits `0` and
remains stopped. On subsequent `docker compose up` calls, the service starts again,
finds existing databases via the idempotency guard, and exits `0` immediately.

### Container Name
`carrotcake-db-init`

---

## Named Volume: carrotcake-sqldata

```yaml
volumes:
  carrotcake-sqldata:
    driver: local
```

| Operation | Effect on volume |
|---|---|
| `docker compose up` (first time) | Volume created; populated by SQL Server + `db-init` |
| `docker compose up` (subsequent) | Volume reused; `db-init` performs no-op check and exits |
| `docker compose down` | Volume preserved |
| `docker compose down --volumes` | Volume deleted; next `up` triggers full re-initialisation |

---

## Startup Sequence

```
docker compose up -d
  │
  ├─► sqlserver starts
  │     └─► health check: SELECT 1 (retries every 10s, up to ~2.5 min total)
  │           └─► service_healthy
  │
  └─► db-init starts (after sqlserver healthy)
        ├─► check CarrotCoreMVC exists?
        │     └─► NO: CREATE DATABASE → run 26 table scripts → 12 view scripts → 3 SP scripts
        └─► check Northwind exists?
              └─► NO: CREATE DATABASE → run northwind.sql
        └─► EXIT 0
```

---

## Compose File Location

```
/compose.yaml   (repository root)
```

Command style: `docker compose` (Docker CLI plugin — not `docker-compose` standalone)
