# Research: Docker Compose Developer Infrastructure

**Feature**: `001-docker-compose-dev-infra`  
**Phase**: 0 — Pre-design research  
**Date**: 2026-03-05

---

## R-001 · SQL Server Docker Image Selection

**Decision**: `mcr.microsoft.com/mssql/server:2022-developer-latest`

**Rationale**:
- SQL Server 2022 Developer Edition is free for development and testing use under Microsoft's EULA.
- SQL Server 2022 defaults to compatibility level 160 but is backward compatible with level 130 (SQL Server 2016), satisfying the constitution's minimum requirement.
- The `developer-latest` tag is appropriate for dev infrastructure; production deployments would use a pinned tag or licensed edition.

**Alternatives considered**:
- SQL Server 2019 (`2019-latest`): Older, no benefit for dev infra. Rejected.
- SQL Server Express (`express`): Row-size and database-size limits would break seeding of larger datasets. Rejected.
- Azure SQL Edge: Not a general-purpose SQL Server replacement. Rejected.

---

## R-002 · Apple Silicon (ARM64) Platform Requirement

**Decision**: Add `platform: linux/amd64` to both the `sqlserver` and `db-init` services.

**Rationale**:
- `mcr.microsoft.com/mssql/server:2022-developer-latest` does not publish a native `linux/arm64` image for the Developer Edition.
- Docker Desktop on Apple Silicon (M1/M2/M3) emulates `linux/amd64` via Rosetta 2 / QEMU; performance is adequate for developer workloads.
- Without the explicit `platform` declaration, `docker compose up` silently pulls the wrong architecture manifest and fails at container start time.

**Note**: If Microsoft ships a native ARM64 SQL Server Developer image, remove the `platform` declaration from `compose.yaml`.

---

## R-003 · Database Initialisation Pattern

**Decision**: Separate `db-init` one-shot service with `depends_on: condition: service_healthy` and `restart: no`.

**Rationale**:
- The official `mssql/server` image does not support an `initdb.d`-style directory (unlike PostgreSQL or MySQL). Scripts cannot be mounted and auto-executed.
- A separate init service using the same image (which bundles `sqlcmd`) keeps the compose file self-contained without requiring a separate tools image.
- `restart: no` ensures the init container runs once per `docker compose up` invocation and then exits cleanly — it will not restart on subsequent `docker compose up` calls as long as the named volume already contains initialised databases (guarded by the idempotency check in the init script).
- A health-check dependency (`condition: service_healthy`) on the `sqlserver` service guarantees `sqlcmd` connections in the init script are not attempted before the SQL Server engine is ready.

**Alternatives considered**:
- Override `sqlserver` entrypoint with a custom startup script: Fragile — future image updates could break the override; conflates infrastructure and initialisation concerns. Rejected.
- `mssql-tools` as a separate image: Would require a second `platform: linux/amd64` declaration and introduces an additional image pull; reusing the SQL Server image is simpler. Rejected.
- Docker-compose `command` hook on the `sqlserver` service: Same fragility as entry-point override. Rejected.

---

## R-004 · SQL Server Health Check Configuration

**Decision**: Use `CMD-SHELL` form of health check invoking `/opt/mssql-tools18/bin/sqlcmd`:

```yaml
healthcheck:
  test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$SA_PASSWORD\" -Q 'SELECT 1' -No || exit 1"]
  interval: 10s
  timeout: 5s
  retries: 12
  start_period: 30s
```

**Rationale**:
- SQL Server 2022 images ship `mssql-tools18` (the newer ODBC 18 toolset); the binary is at `/opt/mssql-tools18/bin/sqlcmd`.
- `$$SA_PASSWORD` in Compose YAML escapes the `$` so Compose does not substitute it at parse time; the container shell receives `$SA_PASSWORD` and expands it from the container's own env.
- `-No` disables TLS certificate validation — appropriate for localhost dev (no cert configured); without it, `sqlcmd` refuses connections to the self-signed cert on new installs.
- `start_period: 30s` accounts for SQL Server's ~15–25 s cold-start time; health check failures during startup do not count toward `retries`.
- `retries: 12` with `interval: 10s` allows up to 2 minutes of grace beyond `start_period` before the container is declared unhealthy.

**Alternatives considered**:
- NetCat (`nc -z localhost 1433`): Only verifies port is open, not that the engine accepts queries. Rejected.
- WaitForIt / custom polling scripts: Adds complexity; `sqlcmd` is already available in the image. Rejected.

---

## R-005 · Idempotency Strategy for Initialisation Scripts

**Decision**: Guard at the database level — check whether `CarrotCoreMVC` (and `Northwind`) databases already exist before executing any DDL. Individual SQL files are NOT wrapped in `IF NOT EXISTS` guards.

**Init script logic**:
```bash
DB_EXISTS=$(/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name='CarrotCoreMVC'" \
  -h -1 -No 2>/dev/null | tr -d '[:space:]')
if [ "$DB_EXISTS" = "0" ]; then
  # create database and run scripts
fi
```

**Rationale**:
- Modifying all 41 SQL source files to add `IF NOT EXISTS` guards would create a large, error-prone diff that mixes infrastructure concerns into the schema source files.
- The database-level guard is simpler, more reliable, and explicitly matches FR-004 ("subsequent starts MUST NOT drop or re-initialise").
- If a developer deletes the volume, both databases will be absent and the full init sequence runs again — matching the clean-reset requirement (FR-014).

---

## R-006 · CarrotCoreMVC Table Execution Order (FK Dependency Resolution)

**Decision**: Scripts must be executed in the following explicit order to satisfy FK constraints. Alphabetical execution would fail due to forward FK references.

**Tier 0 — Independent (no FK):**
1. `__EFMigrationsHistory.sql`
2. `AspNetCache.sql`
3. `AspNetRoles.sql`
4. `AspNetUsers.sql`
5. `carrot_ContentType.sql`
6. `carrot_SerialCache.sql`
7. `carrot_Sites.sql`

**Tier 1 — Depend on Tier 0:**
8. `AspNetRoleClaims.sql` → `AspNetRoles`
9. `AspNetUserClaims.sql` → `AspNetUsers`
10. `AspNetUserLogins.sql` → `AspNetUsers`
11. `AspNetUserRoles.sql` → `AspNetRoles`, `AspNetUsers`
12. `AspNetUserTokens.sql` → `AspNetUsers`
13. `carrot_UserData.sql` → `AspNetUsers`
14. `carrot_ContentCategory.sql` → `carrot_Sites`
15. `carrot_ContentTag.sql` → `carrot_Sites`
16. `carrot_RootContentSnippet.sql` → `carrot_Sites`
17. `carrot_TextWidget.sql` → `carrot_Sites`

**Tier 2 — Depend on Tier 1:**
18. `carrot_RootContent.sql` → `carrot_ContentType`, `carrot_UserData`, `carrot_Sites`
19. `carrot_UserSiteMapping.sql` → `carrot_UserData`, `carrot_Sites`
20. `carrot_ContentSnippet.sql` → `carrot_RootContentSnippet`

**Tier 3 — Depend on Tier 2:**
21. `carrot_Content.sql` → `carrot_RootContent`
22. `carrot_ContentComment.sql` → `carrot_RootContent`
23. `carrot_Widget.sql` → `carrot_RootContent`
24. `carrot_CategoryContentMapping.sql` → `carrot_ContentCategory`, `carrot_RootContent`
25. `carrot_TagContentMapping.sql` → `carrot_ContentTag`, `carrot_RootContent`

**Tier 4 — Depend on Tier 3:**
26. `carrot_WidgetData.sql` → `carrot_Widget`

**After all tables** — Views and Stored Procedures (alphabetical within each group):
- All `dbo/Views/*.sql`
- All `dbo/Stored Procedures/*.sql`

---

## R-007 · Northwind SQL Script Characteristics

**Decision**: The `Northwind/northwind.sql` script must be run against a pre-created `Northwind` database; the script itself opens with `USE [Northwind]` and does not include a `CREATE DATABASE` statement.

**Init script implication**: The init script must:
1. Create the `Northwind` database with `CREATE DATABASE [Northwind]`
2. Then execute `northwind.sql` (which will `USE [Northwind]` and create all objects)

**Idempotency note**: `northwind.sql` uses plain `CREATE TABLE` without `IF NOT EXISTS` guards. The same database-level guard from R-005 applies: only run the script if the `Northwind` database does not yet exist.

---

## R-008 · ASP.NET Core Connection String Override

**Decision**: Use standard ASP.NET Core environment variable configuration provider with double-underscore hierarchy separators.

| Environment Variable | Maps to Config Key |
|---|---|
| `ConnectionStrings__CarrotwareCMS` | `ConnectionStrings:CarrotwareCMS` |
| `ConnectionStrings__NorthwindConnection` | `ConnectionStrings:NorthwindConnection` |

**Rationale**:
- Built into ASP.NET Core with no code changes required — `WebApplication.CreateBuilder` automatically loads environment variables and they override `appsettings.json` values at the same key path.
- On Windows (where `__` can be replaced by `:` in some shells), only the `__` form is consistently cross-platform.
- Satisfies FR-011 and FR-012 simultaneously: env vars override, `appsettings.json` stays untouched as Windows SA fallback.

**Connection string format for Dockerised SQL Server** (SA auth, no Windows auth):
```
Server=localhost,${SQL_PORT:-1433};Database=CarrotCoreMVC;User Id=sa;Password=${SA_PASSWORD};MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;
```

**Note**: `Encrypt=False` or `TrustServerCertificate=True` is required because the Developer container uses a self-signed cert without a trusted CA chain. This is acceptable for developer workstations only.

---

## R-009 · SA_PASSWORD Validation

**Decision**: Use Docker Compose built-in required-variable syntax with an informative error message.

```yaml
environment:
  SA_PASSWORD: ${SA_PASSWORD:?SA_PASSWORD must be set. Copy .env.example to .env and provide a strong password meeting SQL Server complexity requirements.}
```

**Rationale**:
- The `:?` operator causes `docker compose up` to exit before creating any container, printing the error message to stderr.
- No custom validation script or `healthcheck` trick is needed.
- SQL Server's own password complexity enforcement will catch weak passwords and cause the container to exit with an error (logged to `docker compose logs sqlserver`).

---

## R-010 · Docker Compose File Naming and Command Style

**Decision**: File named `compose.yaml` (preferred canonical name per Compose Specification v1.0); all documented commands use `docker compose` (the Docker CLI plugin, not the legacy `docker-compose` standalone tool).

**Rationale**:
- `compose.yaml` is the preferred filename per the Compose Specification; `docker-compose.yml` is supported for backward compatibility but signals the legacy tool.
- User requirement: "must use the new style docker compose" — this refers to `docker compose` (space, not hyphen) as a Docker CLI plugin available since Docker Desktop 3.x.

---

## R-011 · .gitignore and Credential Safety

**Decision**: Create a root-level `.gitignore` that ignores `.env` (and common development artifacts). The `.env.example` file is committed to source control.

**Rationale**:
- No `.gitignore` currently exists at the repository root (confirmed by inspection). One must be created for FR-009.
- `.env.example` is safe to commit — it contains only placeholder values and documentation (FR-008).
- The constitution (Principle IV) mandates that sensitive configuration never appears in source control.
