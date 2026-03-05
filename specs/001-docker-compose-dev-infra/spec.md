# Feature Specification: Docker Compose Developer Infrastructure

**Feature Branch**: `001-docker-compose-dev-infra`  
**Created**: 2026-03-05  
**Status**: Draft  
**Input**: User description: "Add Docker Compose support so developers can run and test the CMS on Windows, macOS, and Linux without installing or configuring SQL Server locally."

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Zero-Prerequisite Local Environment Startup (Priority: P1)

A developer who has just cloned the repository on any supported platform (Windows, macOS, or Linux) copies the provided `.env.example` file to `.env`, sets a strong SA password, runs a single command to start the infrastructure, and then launches the CMS application with the standard dotnet run command — all without installing or configuring a local SQL Server instance.

**Why this priority**: This is the core value of the feature. Without it, every other story is inaccessible. Developers currently blocked by Windows-only SQL Server authentication gain immediate access to the full development workflow.

**Independent Test**: Can be fully tested by a new contributor following the README "Quick Start" instructions on a clean machine with only Docker Desktop and the .NET SDK installed, and verifying the CMS home page loads successfully.

**Acceptance Scenarios**:

1. **Given** a machine with Docker Desktop and the .NET SDK installed but no local SQL Server, **When** the developer copies `.env.example` to `.env`, sets `SA_PASSWORD`, and runs `docker compose up -d`, **Then** a SQL Server container starts, the `CarrotCoreMVC` and `Northwind` databases are created and populated from the CMSDataScripts SQL project scripts, and SQL Server is reachable on `localhost:1433`.
2. **Given** the infrastructure is running and healthy, **When** the developer runs `dotnet run` from the `CMSAdmin` directory, **Then** the application starts, connects to the containerised SQL Server using SA password authentication read from environment variables, and serves the CMS home page without errors.
3. **Given** the developer sets no `SA_PASSWORD` value in `.env`, **When** `docker compose up` is executed, **Then** the command fails with a descriptive error before any container is started, preventing silent misconfiguration.

---

### User Story 2 - Persistent Database Across Restarts (Priority: P2)

A developer stops and restarts the Docker infrastructure expecting their seeded or manually modified data to still be present, without needing to re-run any initialisation scripts.

**Why this priority**: Without persistence, developers lose their test data on every restart. This is independently deliverable once the base container is working.

**Independent Test**: Can be fully tested by creating a record in the CMS, running `docker compose down` (without the `--volumes` flag), running `docker compose up -d`, and confirming the record is still present.

**Acceptance Scenarios**:

1. **Given** the SQL Server container has been populated with data, **When** the developer runs `docker compose down` and then `docker compose up -d`, **Then** previously created database records are present and the application connects normally.
2. **Given** the developer explicitly wants a clean slate, **When** the developer runs `docker compose down --volumes`, **Then** the persistent data volume is removed and the databases are re-initialised from the CMSDataScripts scripts on the next `docker compose up`.

---

### User Story 3 - Cross-Platform Connection String Override Without Modifying Source Files (Priority: P3)

A developer switches between using the Docker infrastructure and a local SQL Server installation (or CI environment) without modifying any `appsettings.json` files committed to source control.

**Why this priority**: Preserving the original `appsettings.json` files avoids accidental credential commits and keeps the repository clean.

**Independent Test**: Can be fully tested by verifying `git status` shows no modified `appsettings.json` files after completing the full Docker-based developer setup and running the application successfully.

**Acceptance Scenarios**:

1. **Given** a developer has set the connection string environment variables in their `.env` file, **When** `dotnet run` is executed for `CMSAdmin`, **Then** the application uses the environment-variable-supplied connection strings and the `appsettings.json` files remain unmodified in source control.
2. **Given** no connection string environment variables are set, **When** `dotnet run` is executed, **Then** the application falls back to the connection strings defined in `appsettings.json` (enabling developers with a local Windows SQL Server to continue working exactly as before).
3. **Given** a developer commits all their changes, **When** `git status` and `git diff` are run, **Then** neither `.env` nor any `appsettings.json` file containing credentials appears in the tracked changes (`.env` is git-ignored).

---

### User Story 4 - Automated Health-Gated Database Initialisation (Priority: P4)

The database initialisation scripts run automatically when the SQL Server container first starts, and the scripts do not run again on subsequent restarts unless the data volume has been removed. Initialisation is gated by a health check so that any dependent steps wait for SQL Server to be ready before attempting connections.

**Why this priority**: Eliminates manual setup steps and prevents intermittent startup failures that confuse new contributors.

**Independent Test**: Can be fully tested by removing the data volume, running `docker compose up -d`, waiting for the health check to pass, and verifying both `CarrotCoreMVC` and `Northwind` databases exist with the expected schema objects present.

**Acceptance Scenarios**:

1. **Given** the data volume does not exist, **When** `docker compose up -d` is run, **Then** the SQL Server container passes its health check before initialisation scripts execute, both databases are created, and all schema objects from the CMSDataScripts SQL project are present.
2. **Given** the data volume already contains initialised databases, **When** `docker compose up -d` is run, **Then** the initialisation scripts do not drop and re-create the databases, preserving any existing developer data.
3. **Given** SQL Server takes longer than usual to start, **When** the health check is still pending, **Then** the initialisation step waits and retries up to the configured maximum before reporting a failure, rather than exiting immediately.

---

### Edge Cases

- What happens when `SA_PASSWORD` does not meet SQL Server's minimum complexity requirements? The container must exit and produce a visible error rather than silently restarting.
- What happens when port `1433` is already in use (e.g., a local SQL Server installation)? The `SQL_PORT` variable allows mapping to an alternative host port without changing the compose file.
- What happens if `docker compose up` is run without Docker Desktop running? The command should fail immediately with Docker's standard error, not with a confusing CMS application error.
- What happens on Apple Silicon (ARM64) hardware? The SQL Server image must declare a platform requirement (`linux/amd64`) if it does not natively support ARM64, and this must be documented.
- What happens when the CMSDataScripts SQL files contain objects that already exist in the database (re-initialisation scenario)? Scripts must be idempotent or guarded with existence checks to avoid errors on re-runs.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The infrastructure MUST start a SQL Server instance accessible from the developer's host machine on a configurable port (defaulting to `1433`) using SA password authentication.
- **FR-002**: On first start (no existing data volume), the infrastructure MUST automatically create the `CarrotCoreMVC` database and apply all schema objects defined in the `CMSDataScripts` SQL project (`Tables`, `Views`, `Stored Procedures`).
- **FR-003**: On first start (no existing data volume), the infrastructure MUST automatically create the `Northwind` database and apply its SQL initialisation script from the `Northwind` project directory.
- **FR-004**: Subsequent starts against an existing data volume MUST NOT drop or re-initialise existing databases, preserving developer data.
- **FR-005**: The SQL Server container MUST expose a health check that reports `healthy` only after the SQL Server engine accepts query connections.
- **FR-006**: Database initialisation scripts MUST NOT execute until the SQL Server health check reports `healthy`.
- **FR-007**: All database credentials (SA password) MUST be supplied exclusively via environment variables or a `.env` file; no credentials may appear in any source-controlled file.
- **FR-008**: A `.env.example` file listing all required and optional environment variables with placeholder values MUST be provided and committed to source control.
- **FR-009**: The `.env` file MUST be listed in `.gitignore` to prevent accidental credential commits.
- **FR-010**: An absent or empty `SA_PASSWORD` environment variable MUST cause an immediate, descriptive startup failure before the container is created.
- **FR-011**: The application connection strings MUST be overridable via environment variables using the standard ASP.NET Core environment variable configuration provider, without modifying any `appsettings.json` file.
- **FR-012**: The `appsettings.json` files for `CMSAdmin` and all plugin projects MUST remain unmodified in source control, retaining their existing Windows Integrated Authentication strings as fallback defaults for developers with a local SQL Server.
- **FR-013**: Database data MUST be stored in a named Docker volume so that it persists across `docker compose down` / `docker compose up` cycles.
- **FR-014**: Running `docker compose down --volumes` MUST remove the named data volume, enabling clean re-initialisation on the next `docker compose up`.
- **FR-015**: The SQL host port MUST be configurable via a `SQL_PORT` environment variable (defaulting to `1433`) to allow co-existence with a local SQL Server installation.
- **FR-016**: The complete developer workflow MUST be documented (prerequisites, `.env` setup, `docker compose up -d`, `dotnet run`, and clean-reset procedure) in the project README or a dedicated setup guide.
- **FR-017**: Containerising the `CMSAdmin` web application is explicitly out of scope; Docker Compose manages only the SQL Server infrastructure service.

### Key Entities

- **SQL Server Container**: The containerised database engine. Key attributes: image version (SQL Server 2022 Developer Edition), exposed host port, health check configuration, credential source.
- **Named Data Volume**: Persistent storage backing the SQL Server container. Lifecycle: created on first run, removed only with `docker compose down --volumes`.
- **Initialisation Scripts**: SQL files from `CMSDataScripts/dbo` (Tables, Views, Stored Procedures) and `Northwind/northwind.sql` that define the full database schema. Applied once per volume lifetime.
- **Environment Variables / `.env` File**: The sole source of secrets and configurable values. Key variables: `SA_PASSWORD`, `SQL_PORT`, `ConnectionStrings__CarrotwareCMS`, `ConnectionStrings__NorthwindConnection`.
- **`.env.example`**: A non-secret template committed to source control documenting every variable with placeholder or safe default values.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer on a clean machine (Windows, macOS, or Linux) following the documented quick-start steps can have the CMS running end-to-end in under 10 minutes, measured from first `git clone` to loading the CMS home page.
- **SC-002**: The infrastructure startup sequence completes with both databases initialised within 3 minutes on standard developer hardware under normal network conditions.
- **SC-003**: 100% of required secrets and credentials are documented in `.env.example`; zero secrets are present in any source-controlled file after the feature is merged.
- **SC-004**: After `docker compose down` (no `--volumes`) followed by `docker compose up -d`, all previously created database records are intact — zero data loss on normal restart.
- **SC-005**: After `docker compose down --volumes` followed by `docker compose up -d`, both databases are fully re-initialised and the CMS application connects successfully — full clean-slate recovery achievable with one command sequence.
- **SC-006**: Running `git status` in a fully set-up developer environment shows no modifications to any `appsettings.json` file.
- **SC-007**: Developers with an existing local SQL Server (Windows) can continue to use it without any changes to their workflow — the Docker setup is purely additive and does not break existing setups.

## Assumptions

- The `CMSDataScripts` SQL project files in `CMSDataScripts/dbo/Tables`, `CMSDataScripts/dbo/Views`, and `CMSDataScripts/dbo/Stored Procedures` are compatible with SQL Server 2022 and can be executed sequentially via `sqlcmd` without a specialised SQL project build tool.
- The `Northwind/northwind.sql` file already present in the repository is the single source for the Northwind database schema and data.
- Developers are responsible for choosing an `SA_PASSWORD` value that meets SQL Server's minimum complexity requirements; the setup validates only that a non-empty value is provided.
- The ASP.NET Core environment variable configuration provider (which maps `ConnectionStrings__CarrotwareCMS` → `ConnectionStrings:CarrotwareCMS`) is the mechanism for overriding connection strings without touching `appsettings.json`.
- All plugin projects (`PluginCalendarModule`, `PluginEventCalendarModule`, `PluginFAQ2Module`, `PluginPhotoGallery`, `LoremIpsum`) require the same environment variable override documentation but no file modifications.
