# Quick Start: Docker Compose Developer Setup

**Feature**: `001-docker-compose-dev-infra`  
**Audience**: New contributors and developers switching to the Docker-based SQL Server  
**Date**: 2026-03-05

> **Scope**: This guide covers the Docker-based SQL Server infrastructure only.
> The ASP.NET Core application (`CMSAdmin`) is always run directly with `dotnet run`,
> not containerised.

---

## Prerequisites

| Tool | Minimum version | Notes |
|---|---|---|
| Docker Desktop | 4.x (includes Docker Compose plugin) | Windows, macOS, or Linux |
| .NET SDK | 8.0 | For running `CMSAdmin` with `dotnet run` |

Verify both are available before proceeding:

```bash
docker compose version   # must show "Docker Compose version v2.x.x"
dotnet --version         # must show "8.0.x"
```

---

## Step 1 — Create Your `.env` File

Copy the example file and fill in a strong SA password:

```bash
# macOS / Linux
cp .env.example .env

# Windows (Command Prompt)
copy .env.example .env
```

Open `.env` and set a strong `SA_PASSWORD`. The password must meet SQL Server complexity
requirements: at least 8 characters, containing uppercase letters, lowercase letters,
digits, and at least one non-alphanumeric character (e.g. `!`, `@`, `#`).

Then update the connection string placeholders in `.env` to use the same password:

```dotenv
SA_PASSWORD=MyStr0ng!Pass

ConnectionStrings__CarrotwareCMS=Server=localhost,1433;Database=CarrotCoreMVC;User Id=sa;Password=MyStr0ng!Pass;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;
ConnectionStrings__NorthwindConnection=Server=localhost,1433;Database=Northwind;User Id=sa;Password=MyStr0ng!Pass;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;
```

> **Security**: `.env` is listed in `.gitignore`. Never commit this file.

---

## Step 2 — Start the Infrastructure

```bash
docker compose up -d
```

This command:
1. Pulls the SQL Server 2022 Developer Edition image (first run only, ~1.5 GB).
2. Starts the `sqlserver` container.
3. Waits for SQL Server to be healthy (health check runs every 10 s; allow up to ~2.5 min).
4. Starts the `db-init` container, which creates and seeds both databases.
5. Returns control to your terminal once both containers are running.

**Watch the logs** during initial setup to confirm both databases were created:

```bash
docker compose logs -f db-init
```

Expected output (first run):

```
[init] Checking for CarrotCoreMVC...
[init] Creating CarrotCoreMVC database...
[init] Running table scripts...
[init] Running view scripts...
[init] Running stored procedure scripts...
[init] CarrotCoreMVC initialized.
[init] Checking for Northwind...
[init] Creating Northwind database...
[init] Running Northwind script...
[init] Northwind initialized.
[init] Done.
```

**Verify SQL Server is healthy:**

```bash
docker compose ps
```

The `sqlserver` service should show `(healthy)`.

---

## Step 3 — Run the Application

From the `CMSAdmin` directory, start the ASP.NET Core application. The connection string
environment variables set in your `.env` file are passed to the application by your shell
or by loading the `.env` manually.

**macOS / Linux (bash/zsh)**:

```bash
cd CMSAdmin
set -a && source ../.env && set +a
dotnet run
```

**Windows (PowerShell)**:

```powershell
cd CMSAdmin
Get-Content ..\.env | ForEach-Object {
  if ($_ -match '^([^#][^=]*)=(.*)$') {
    [System.Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2].Trim(), 'Process')
  }
}
dotnet run
```

Open a browser at `https://localhost:5001` (or the port printed by `dotnet run`).

---

## Day-to-Day Workflow

### Stop infrastructure (preserve data)

```bash
docker compose down
```

Data in the `carrotcake-sqldata` volume is preserved. Run `docker compose up -d` to
restart — the `db-init` service will detect existing databases and exit immediately
without re-running scripts.

### Restart infrastructure

```bash
docker compose up -d
```

SQL Server and all your data will be exactly as you left them.

### Check logs

```bash
docker compose logs sqlserver    # SQL Server engine logs
docker compose logs db-init      # Initialisation script output
```

---

## Clean Reset (Destroy and Rebuild Everything)

> **Warning**: This deletes all data in the Docker SQL Server. Use only when you want
> a fresh slate.

```bash
docker compose down --volumes
docker compose up -d
```

This removes the `carrotcake-sqldata` named volume, then on the next `up` recreates
both databases from the schema scripts.

---

## Troubleshooting

### `docker compose up` fails immediately with "SA_PASSWORD must be set"

You have not created a `.env` file or `SA_PASSWORD` is empty. Complete Step 1.

### `sqlserver` shows `(unhealthy)` or never becomes healthy

1. Check `docker compose logs sqlserver` for password complexity errors.
2. Ensure `SA_PASSWORD` is at least 8 characters and contains uppercase, lowercase, digit, and symbol.
3. On Apple Silicon: Docker Desktop must have the Rosetta 2 / VMs emulation enabled (enabled by default in Docker Desktop 4.x+).

### Port 1433 is already in use

Add `SQL_PORT=14330` (or any free port) to your `.env` file and update the port in
both connection strings:

```dotenv
SQL_PORT=14330
ConnectionStrings__CarrotwareCMS=Server=localhost,14330;Database=CarrotCoreMVC;...
ConnectionStrings__NorthwindConnection=Server=localhost,14330;Database=Northwind;...
```

Then restart: `docker compose down && docker compose up -d`

### `db-init` exits with a non-zero code

Run `docker compose logs db-init` to see which SQL script failed. Common causes:
- SQL Server is not yet ready (shouldn't happen with the health check, but may occur on very slow machines — increase `retries` in `compose.yaml`).
- A SQL script syntax error — check the specific error message against the file path.

### "Login failed for user 'sa'"

The `SA_PASSWORD` in `.env` does not match the password SQL Server was initialised with.
Either:
- Correct `SA_PASSWORD` in `.env` and restart with `docker compose down && docker compose up -d`, **or**
- Reset with `docker compose down --volumes && docker compose up -d` to reinitialise from scratch.

### I'm a Windows developer with a local SQL Server — do I need to do any of this?

No. The Docker setup is optional. Continue using your existing `appsettings.json`
connection strings (Windows Integrated Authentication). No files have been modified.

---

## Platform Notes

### Apple Silicon (M1/M2/M3 Mac)

The SQL Server image does not ship a native ARM64 binary. Docker Desktop transparently
emulates `linux/amd64` using Rosetta 2. The `platform: linux/amd64` declaration in
`compose.yaml` is intentional. Performance is adequate for developer workloads.

### Windows with WSL 2

Docker Desktop for Windows with WSL 2 backend is fully supported. Run all commands
from a WSL terminal or PowerShell; the `compose.yaml` path separators work on both.
