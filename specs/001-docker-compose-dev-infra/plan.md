# Implementation Plan: Docker Compose Developer Infrastructure

**Branch**: `001-docker-compose-dev-infra` | **Date**: 2026-03-05 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-docker-compose-dev-infra/spec.md`

## Summary

Add Docker Compose support so developers on Windows, macOS, and Linux can run and test
the CMS without installing or configuring SQL Server locally. A `compose.yaml` at the
repository root starts a SQL Server 2022 Developer Edition container and a one-shot
`db-init` container that seeds both the `CarrotCoreMVC` and `Northwind` databases from
existing SQL scripts. Connection strings are overridden via environment variables;
`appsettings.json` files remain unmodified. No application code changes are required.

## Technical Context

**Language/Version**: Shell (bash), YAML — no .NET changes; existing .NET 8 / ASP.NET Core MVC application is unchanged  
**Primary Dependencies**: Docker Compose v2 plugin (`docker compose`); `mcr.microsoft.com/mssql/server:2022-developer-latest`; `/opt/mssql-tools18/bin/sqlcmd` (bundled in image)  
**Storage**: SQL Server 2022 Developer Edition in Docker; named volume `carrotcake-sqldata` for persistence  
**Testing**: Manual end-to-end (no automated test suite for infrastructure); verified via `docker compose ps`, `docker compose logs`, and `dotnet run` smoke test  
**Target Platform**: Windows (Docker Desktop + WSL 2), macOS (Intel and Apple Silicon via `linux/amd64` emulation), Linux (Docker Engine)  
**Project Type**: Developer infrastructure / DevOps tooling — no new application project  
**Performance Goals**: Infrastructure ready (health check passing) within 3 minutes; `db-init` completes within 60 seconds on standard developer hardware  
**Constraints**: `SA_PASSWORD` must be non-empty (enforced by Compose `:?` operator); Apple Silicon requires `platform: linux/amd64`; 26 table scripts must execute in FK-dependency order (documented in research.md R-006); `appsettings.json` files must remain unmodified  
**Scale/Scope**: Single developer workstation use only; no production or CI container deployment

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

This feature is infrastructure-only (Docker Compose YAML, shell scripts, `.env` files,
and documentation). No new C# projects, EF Core entities, controllers, or views are
introduced. Constitution principles are evaluated below.

| Principle | Status | Notes |
|---|---|---|
| I — Layered Architecture Fidelity | ✅ PASS | No new C# projects; no layer boundaries crossed. Docker infrastructure sits outside the application layer stack. |
| II — Plugin Contract Compliance | ✅ N/A | No new plugins introduced. |
| III — Testability by Design | ✅ PASS | No business logic changes. Connection string override uses built-in ASP.NET Core configuration — no new code paths to test. No `// TODO(TEST):` deferral needed. |
| IV — Security by Default | ✅ PASS | `SA_PASSWORD` enforced via Compose `:?` operator (fails fast if absent). `.env` excluded via `.gitignore`. `appsettings.json` retains no credentials. `TrustServerCertificate=True` is documented as dev-only. |
| V — Performance Discipline | ✅ N/A | No query paths or caching changed. |
| VI — UX Consistency | ✅ N/A | No admin views introduced. |
| VII — Structured Observability | ✅ N/A | No new controllers or service code. Init script errors are surfaced via `docker compose logs db-init`. |
| Technology Constraints (.NET 8) | ✅ PASS | `CMSAdmin` project unchanged; still targets .NET 8. |
| Technology Constraints (SQL Server) | ✅ PASS | SQL Server 2022 Developer Edition satisfies "Express 2016 or later, compatibility level 130+". |
| Technology Constraints (EF migrations) | ✅ PASS | No model changes; schema bootstrapped from existing `CMSDataScripts` SQL project files. No new migrations required. |

**Gate result: PASS — no violations. No Complexity Tracking entry required.**

**Post-design re-check (Phase 1)**: Constitution compliance confirmed. The design
introduces no new application-layer dependencies, no credential exposure, and no
layer boundary violations. The one-shot `db-init` service pattern does not modify
any existing source file.

## Project Structure

### Documentation (this feature)

```text
specs/001-docker-compose-dev-infra/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output — 11 decisions documented
├── data-model.md        # Phase 1 output — 8 configuration entities
├── quickstart.md        # Phase 1 output — developer setup guide
├── contracts/
│   ├── env-vars.md      # Phase 1 output — environment variable interface
│   └── compose-services.md  # Phase 1 output — Docker Compose service interface
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
compose.yaml             # Docker Compose service definitions (NEW)
.env.example             # Environment variable template, committed (NEW)
.gitignore               # Root-level gitignore, adds .env entry (NEW)

docker/
└── init-db.sh           # DB initialisation script run by db-init service (NEW)

# Unchanged — no modifications to any existing source file:
CMSDataScripts/dbo/      # SQL schema scripts (read-only mounted into db-init)
Northwind/northwind.sql  # Northwind schema + data (read-only mounted into db-init)
CMSAdmin/appsettings.json
CMSAdmin/appsettings.Development.json
```

**Structure Decision**: Single flat layout at repository root. All new artefacts are
either at the root (`compose.yaml`, `.env.example`, `.gitignore`) or in a new `docker/`
directory (`init-db.sh`). No changes to any existing project directory. The `docker/`
directory is chosen over `scripts/` to signal clearly that these files are Docker-lifecycle
artefacts.
