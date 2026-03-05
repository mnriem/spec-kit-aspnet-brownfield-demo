---
description: "Task list for Docker Compose Developer Infrastructure implementation"
---

# Tasks: Docker Compose Developer Infrastructure

**Input**: Design documents from `/specs/001-docker-compose-dev-infra/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅
**Tests**: No automated tests — plan.md explicitly defines "Manual end-to-end (no automated test suite for infrastructure)". All verification is manual.
**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with other [P]-marked tasks in the same phase (different files or non-overlapping sections)
- **[Story]**: User story this task belongs to (US1–US4 maps to spec.md priorities P1–P4)
- Exact file paths are included in every description

---

## Phase 1: Setup (Credential Safety & Repository Configuration)

**Purpose**: Establish the security baseline before any Docker artefacts are created. These two files prevent credential exposure for the lifetime of the feature.

- [X] T001 [P] Create `.gitignore` at repository root with `.env` entry plus standard .NET/VS ignores (`*.user`, `*.suo`, `*.swp`, `.vs/`, `[Bb]in/`, `[Oo]bj/`) per R-011 and FR-009
- [X] T002 [P] Create `.env.example` at repository root with all four documented variables: `SA_PASSWORD` (required, with complexity note), `SQL_PORT` (optional, default 1433), `ConnectionStrings__CarrotwareCMS` and `ConnectionStrings__NorthwindConnection` (optional, SA auth format `Server=localhost,1433;...;Encrypt=False;TrustServerCertificate=True;`) per FR-008, FR-011, and data-model §5

---

## Phase 2: Foundational (Core Docker Compose Skeleton)

**Purpose**: Define the `sqlserver` service skeleton in `compose.yaml`. This is a blocking prerequisite — the db-init service, health-gated init, and volume mount all extend this base definition.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 Create `compose.yaml` at repository root (no `version:` key per R-010 / Compose Spec v1.0) defining the `sqlserver` service with: `image: mcr.microsoft.com/mssql/server:2022-developer-latest`, `platform: linux/amd64` (R-002), `container_name: carrotcake-sqlserver`, environment block (`ACCEPT_EULA: Y`, `MSSQL_PID: Developer`, `SA_PASSWORD: ${SA_PASSWORD:?SA_PASSWORD must be set…}` per R-009), and `restart: unless-stopped` per FR-001, R-001, data-model §2

**Checkpoint**: `compose.yaml` parses (`docker compose config` returns no errors) — user story work can now proceed.

---

## Phase 3: User Story 1 — Zero-Prerequisite Local Environment Startup (Priority: P1) 🎯 MVP

**Goal**: A developer with only Docker Desktop and the .NET SDK can clone the repo, copy `.env.example` to `.env`, set `SA_PASSWORD`, run `docker compose up -d`, and reach the CMS home page via `dotnet run` — with no local SQL Server installed.

**Independent Test**: New contributor follows quickstart.md on a clean machine; `docker compose ps` shows `sqlserver (healthy)`; `docker compose logs db-init` shows both databases created; `dotnet run` in `CMSAdmin/` serves the CMS home page without errors.

### Implementation for User Story 1

- [X] T004 [P] [US1] Add `healthcheck` block to `sqlserver` service in `compose.yaml`: `test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$SA_PASSWORD\" -Q 'SELECT 1' -No || exit 1"]`, `interval: 10s`, `timeout: 5s`, `retries: 12`, `start_period: 30s` per FR-005 and R-004
- [X] T005 [P] [US1] Add `ports` block to `sqlserver` service in `compose.yaml`: `"${SQL_PORT:-1433}:1433"` per FR-015 and data-model §2
- [X] T006 [P] [US1] Add top-level `volumes:` section in `compose.yaml` declaring `carrotcake-sqldata: driver: local`, and add `volumes:` mount `carrotcake-sqldata:/var/opt/mssql` to the `sqlserver` service per FR-013, data-model §4 and §8
- [X] T007 [US1] Create `docker/init-db.sh` at repository root implementing CarrotCoreMVC database initialization: `set -euo pipefail`; `SA_PASSWORD` validation; check `sys.databases` WHERE `name='CarrotCoreMVC'` via `sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -h -1 -No`; if absent: `CREATE DATABASE [CarrotCoreMVC]`; execute 26 table scripts via `sqlcmd -d CarrotCoreMVC -b -No` in the exact FK-dependency order from R-006 (Tier 0: `__EFMigrationsHistory`, `AspNetCache`, `AspNetRoles`, `AspNetUsers`, `carrot_ContentType`, `carrot_SerialCache`, `carrot_Sites`; Tier 1: `AspNetRoleClaims`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserRoles`, `AspNetUserTokens`, `carrot_UserData`, `carrot_ContentCategory`, `carrot_ContentTag`, `carrot_RootContentSnippet`, `carrot_TextWidget`; Tier 2: `carrot_RootContent`, `carrot_UserSiteMapping`, `carrot_ContentSnippet`; Tier 3: `carrot_Content`, `carrot_ContentComment`, `carrot_Widget`, `carrot_CategoryContentMapping`, `carrot_TagContentMapping`; Tier 4: `carrot_WidgetData`); then all files in `/scripts/carrot/dbo/Views/*.sql` (alphabetical); then all files in `/scripts/carrot/dbo/Stored Procedures/*.sql` (alphabetical); emit `[init]` prefixed log lines matching quickstart.md expected output per FR-002, R-005, R-006, data-model §6
- [X] T008 [US1] Add Northwind database initialization to `docker/init-db.sh`: check `sys.databases` WHERE `name='Northwind'`; if absent: `CREATE DATABASE [Northwind]` then execute `/scripts/northwind/northwind.sql` (script opens with `USE [Northwind]`, no further `-d` flag needed); emit `[init]` log lines per FR-003 and R-007; ensure `[init] Done.` is the final line and script exits `0`
- [X] T009 [P] [US1] Add `db-init` service to `compose.yaml` with: `image: mcr.microsoft.com/mssql/server:2022-developer-latest`, `platform: linux/amd64`, `container_name: carrotcake-db-init`, `depends_on: sqlserver: condition: service_healthy`, `entrypoint: ["/bin/bash", "/docker/init-db.sh"]`, `environment: SA_PASSWORD: ${SA_PASSWORD:?…}`, read-only volume mounts (`./CMSDataScripts:/scripts/carrot:ro`, `./Northwind:/scripts/northwind:ro`, `./docker/init-db.sh:/docker/init-db.sh:ro`), `restart: no` per FR-006, R-003, data-model §3

**Checkpoint**: User Story 1 is fully functional — a clean-machine contributor can run `docker compose up -d`, monitor `docker compose logs -f db-init` to confirm both databases are created, then `cd CMSAdmin && set -a && source ../.env && set +a && dotnet run` and view the CMS home page.

---

## Phase 4: User Story 2 — Persistent Database Across Restarts (Priority: P2)

**Goal**: Developer data survives `docker compose down` / `docker compose up -d` cycles without re-running any initialisation scripts. A full clean slate is achievable with `docker compose down --volumes`.

**Independent Test**: Create a CMS content item; run `docker compose down`; run `docker compose up -d`; confirm `docker compose logs db-init` shows no database creation (idempotency no-op); confirm the previously created content item is still present in the running application.

### Implementation for User Story 2

- [X] T010 [US2] Add a **Docker Developer Workflow** section to `README.md` at repository root documenting: day-to-day commands (`docker compose up -d`, `docker compose down`, `docker compose up -d` restart), the data-preservation guarantee of `docker compose down` (volume retained, `db-init` is a no-op on restart), the clean-reset procedure (`docker compose down --volumes` followed by `docker compose up -d`), and the expected `db-init` log output for both first-run and restart scenarios per FR-014, FR-016, SC-004 and SC-005

**Checkpoint**: User Story 2 is independently verified — data persists across normal restart cycles, and `docker compose down --volumes` provides full clean-slate recovery.

---

## Phase 5: User Story 3 — Cross-Platform Connection String Override Without Modifying Source Files (Priority: P3)

**Goal**: Developers switching between Docker-based and local SQL Server do so entirely through the `.env` file; `appsettings.json` is never modified and always left clean in `git status`.

**Independent Test**: Follow quickstart.md end-to-end; run `git status`; confirm zero modifications to any `appsettings.json` file; confirm `dotnet run` connects to Docker SQL Server using SA auth from environment variables.

### Implementation for User Story 3

- [X] T011 [US3] Expand the **Docker Developer Workflow** section in `README.md` at repository root with: step-by-step `dotnet run` instructions loading `.env` for macOS/Linux (`set -a && source ../.env && set +a`) and Windows PowerShell (`Get-Content ..\.env | ForEach-Object { … }`); explanation of how `ConnectionStrings__CarrotwareCMS` and `ConnectionStrings__NorthwindConnection` override `appsettings.json` via ASP.NET Core's environment variable provider (R-008); backward-compatibility note that developers without `.env` connection strings continue using Windows Integrated Auth from `appsettings.json` unchanged; and a `git status` verification step confirming no `appsettings.json` modification per FR-011, FR-012, FR-016, R-008, SC-006, SC-007

**Checkpoint**: User Story 3 is independently verifiable — `git status` shows no modifications to `appsettings.json`, and the CMS connects to Docker SQL Server using SA credentials from `.env`.

---

## Phase 6: User Story 4 — Automated Health-Gated Database Initialisation (Priority: P4)

**Goal**: The `db-init` container never runs before SQL Server accepts connections; the health check retries gracefully if SQL Server is slow to start; the initialisation scripts never re-run against an existing data volume.

**Independent Test**: Remove the data volume (`docker compose down --volumes`), run `docker compose up -d`, watch `docker compose ps` — `db-init` stays waiting while `sqlserver` is `starting`/`unhealthy`; after `sqlserver` reaches `(healthy)`, `db-init` starts and exits `0`; both databases are present. Then `docker compose down && docker compose up -d` — `db-init` starts again, finds existing databases, logs no creation, and exits `0` immediately.

### Implementation for User Story 4

- [X] T012 [US4] Harden `docker/init-db.sh` error handling: add `set -euo pipefail` at the top (if not already present from T007); ensure each `sqlcmd` table-script call uses both `-b` (abort on error) and clearly identifies the failing script name in error output; add specific log messages for each tier of table execution so a failure points to the exact script; ensure the final exit is `exit 0` after both idempotency paths (new init and no-op) per R-005 and quickstart.md expected log output
- [X] T013 [P] [US4] Add **Edge Cases & Troubleshooting** subsection to the Docker Developer Workflow in `README.md` covering: `SA_PASSWORD` complexity failure (container exits — inspect via `docker compose logs sqlserver`), port 1433 conflict and `SQL_PORT` workaround, Apple Silicon `linux/amd64` emulation note (R-002), and `docker compose up` failure when Docker Desktop is not running per spec.md edge cases, R-002, FR-010, FR-015

**Checkpoint**: User Story 4 is independently verifiable — health-gated startup is confirmed by watching `docker compose ps` and `db-init` logs; idempotency is confirmed by restart showing no re-initialisation.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, syntax verification, and any cross-story cleanup.

- [X] T014 [P] Validate `compose.yaml` syntax by running `docker compose config` from repository root and confirming zero errors; verify all variable substitutions (`SA_PASSWORD`, `SQL_PORT`) resolve correctly with a sample .env file
- [X] T015 Run full quickstart.md validation on a clean environment: follow all steps from prerequisites through `dotnet run`, verify `docker compose ps` shows `(healthy)`, `docker compose logs db-init` shows expected output, CMS home page loads, `git status` shows no modified `appsettings.json` per SC-001, SC-002, SC-003

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Setup (T001, T002)
  🔀 parallel: T001 ‖ T002
  └─► Phase 2: Foundational (T003) — blocked until Phase 1 complete
        └─► Phase 3: US1 (T004–T009) — blocked until Phase 2 complete
              T004, T005, T006, T009 can be parallelised (different compose.yaml sections / different file from T007-T008)
              T007 → T008 (sequential: T008 appends to init-db.sh created by T007)
              └─► Phase 4: US2 (T010) — can start after Phase 3 complete
              └─► Phase 5: US3 (T011) — can start after Phase 3 complete
              └─► Phase 6: US4 (T012, T013) — can start after Phase 3 complete
                    Phases 4, 5, 6 are independent of each other
                    └─► Phase 7: Polish (T014, T015) — all phases complete
```

### User Story Dependencies

- **User Story 1 (P1)**: Depends on Phase 1 + Phase 2. No dependency on US2/US3/US4.
- **User Story 2 (P2)**: Can start after US1 is complete. No dependency on US3/US4.
- **User Story 3 (P3)**: Can start after US1 is complete. No dependency on US2/US4.
- **User Story 4 (P4)**: Can start after US1 is complete. No dependency on US2/US3.

### Within Phase 3 (US1) — Detailed Sequencing

```
T003 → T004 [P]  (compose.yaml healthcheck section)
     → T005 [P]  (compose.yaml ports section)
     → T006 [P]  (compose.yaml volumes declaration + mount)
     → T007      (docker/init-db.sh CarrotCoreMVC init)
         → T008  (docker/init-db.sh Northwind init — appends to same file)
     → T009 [P]  (compose.yaml db-init service — different file from T007/T008)

T004, T005, T006, T009 can be submitted as a single multi-replace call on compose.yaml
after T003 creates the skeleton.
T007 → T008 must be sequential (same file, T008 extends T007's work).
```

### Parallel Opportunities

| Phase | Parallel Group | Tasks |
|---|---|---|
| Phase 1 | Setup artefacts | T001 ‖ T002 |
| Phase 3 (US1) | Compose sections | T004 ‖ T005 ‖ T006 ‖ T009 (all compose.yaml, different sections — can batch in one multi-replace) |
| Phases 4–6 | Cross-story work | T010 ‖ T011 ‖ T012 ‖ T013 (after US1 is complete) |
| Phase 7 | Polish | T014 ‖ T015 (after all stories complete) |

---

## Parallel Execution Example: User Story 1

```bash
# After T003 (compose.yaml skeleton) is complete, these can run in parallel:

# Group A — compose.yaml additions (batch as multi_replace_string_in_file):
Task T004: "Add healthcheck block to sqlserver service in compose.yaml"
Task T005: "Add ports mapping to sqlserver service in compose.yaml"
Task T006: "Add carrotcake-sqldata volume declaration and mount to compose.yaml"
Task T009: "Add db-init service definition to compose.yaml"

# Group B — init-db.sh (sequential within group):
Task T007: "Create docker/init-db.sh with CarrotCoreMVC init logic"
  → Task T008: "Add Northwind init to docker/init-db.sh"

# Groups A and B can run in parallel (compose.yaml vs docker/init-db.sh — different files)
```

---

## Parallel Execution Example: User Stories 2–4 (after US1 complete)

```bash
# All three can be worked in parallel after Phase 3 complete:
Task T010 [US2]: "Add data persistence documentation to README.md"
Task T011 [US3]: "Add dotnet run with env vars and git status verification to README.md"
Task T012 [US4]: "Harden init-db.sh error handling"
Task T013 [US4]: "Add edge cases & troubleshooting section to README.md"

# Note: T010, T011, T013 all edit README.md — batch as multi_replace_string_in_file
# T012 edits docker/init-db.sh — can run in parallel with README edits
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup — `.gitignore` and `.env.example`
2. Complete Phase 2: Foundational — `compose.yaml` skeleton with `sqlserver`
3. Complete Phase 3: User Story 1 — health check, port mapping, volume, `init-db.sh`, `db-init` service
4. **STOP and VALIDATE**: Follow quickstart.md, verify CMS home page loads end-to-end
5. All core promise of the feature is delivered — developers can use Docker SQL Server today

### Incremental Delivery

1. **Sprint 1**: Phase 1 + Phase 2 + Phase 3 → Full working infra (MVP — delivers US1)
2. **Sprint 2**: Phase 4 (US2) → Users gain documented persistence guarantee
3. **Sprint 3**: Phase 5 (US3) → Users gain clear cross-platform env var instructions
4. **Sprint 4**: Phase 6 (US4) + Phase 7 (Polish) → Hardened error handling + full validation

Each sprint adds value without breaking previous deliverables.

### Single-Developer Strategy

```
Day 1: T001 + T002 (15 min) → T003 (20 min) → T004+T005+T006 in one multi-replace (20 min)
Day 1: T007 (45 min: 26-file FK order, sqlcmd calls, log output) → T008 (15 min)
Day 1: T009 (15 min) → Manual smoke test: docker compose up -d  ← MVP DONE
Day 2: T010 + T011 + T012 + T013 batched (60 min) → T014 + T015 (30 min)
```

---

## Notes

- `[P]` tasks operate on different files or non-overlapping sections; prefer `multi_replace_string_in_file` to batch same-file edits
- `[Story]` labels enable traceability back to spec.md user stories and their independent test criteria
- All 4 user stories are delivered by exactly 4 new files: `.gitignore`, `.env.example`, `compose.yaml`, `docker/init-db.sh` (plus README.md updates)
- No existing source files are modified — `appsettings.json` files remain unchanged throughout (FR-012)
- The FK-ordered table list in T007 is the **complete authoritative ordering from R-006** — do not reorder or substitute alphabetical sorting
- `sqlcmd` binary path is `/opt/mssql-tools18/bin/sqlcmd` (SQL Server 2022 image; older `mssql-tools` path would fail)
- `-No` flag is required on all `sqlcmd` calls to bypass TLS certificate validation for localhost connections (R-004)
