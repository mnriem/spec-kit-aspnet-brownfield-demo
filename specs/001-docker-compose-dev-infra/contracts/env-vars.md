# Contract: Environment Variables

**Feature**: `001-docker-compose-dev-infra`  
**Contract Type**: Developer configuration interface  
**Version**: 1.0  
**Date**: 2026-03-05

---

## Overview

The environment variable interface is the sole mechanism for supplying secrets and
configurable values to both the Docker Compose infrastructure and the ASP.NET Core
application. No credentials or host-specific values may appear in source-controlled
files.

Developers set these variables in a `.env` file at the repository root (copied from
`.env.example`). The `.env` file is listed in `.gitignore` and must never be committed.

---

## Variables

### SA_PASSWORD

| Property | Value |
|---|---|
| **Required** | Yes |
| **Default** | None — absence causes immediate `docker compose up` failure |
| **Consumers** | `sqlserver` service (SQL Server SA account), `db-init` service (`sqlcmd` auth), `ConnectionStrings__CarrotwareCMS`, `ConnectionStrings__NorthwindConnection` |
| **Validation** | Non-empty enforced by Compose `:?` operator; SQL Server enforces complexity (≥8 chars, mixed case/digit/symbol) |
| **Example** | `MyStr0ng!Pass` |

**Failure modes**:
- Absent/empty → `docker compose up` exits before container creation with: `SA_PASSWORD must be set. Copy .env.example to .env and provide a strong password meeting SQL Server complexity requirements.`
- Present but too weak → `sqlserver` container starts and then exits; inspect with `docker compose logs sqlserver`

---

### SQL_PORT

| Property | Value |
|---|---|
| **Required** | No |
| **Default** | `1433` |
| **Consumers** | `sqlserver` service port mapping (`${SQL_PORT:-1433}:1433`) |
| **Validation** | Must be a valid host port number (1–65535); no Compose-level validation |
| **Example** | `14330` (when local SQL Server occupies 1433) |

**Effect**: Changes only the *host-side* port; the SQL Server container always listens on 1433 internally. If changed, the `ConnectionStrings__*` variables must use the same port.

---

### ConnectionStrings__CarrotwareCMS

| Property | Value |
|---|---|
| **Required** | No |
| **Default** | Falls back to `appsettings.json` value (Windows Integrated Auth to `.\SQL2016EXPRESS`) |
| **Consumers** | ASP.NET Core configuration provider in `CMSAdmin` and all plugin projects |
| **Validation** | Standard ADO.NET / SQL Client connection string format |
| **Example** | `Server=localhost,1433;Database=CarrotCoreMVC;User Id=sa;Password=MyStr0ng!Pass;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;` |

**Mapping**: ASP.NET Core's environment variable provider maps `ConnectionStrings__CarrotwareCMS` → `ConnectionStrings:CarrotwareCMS`, which overrides the `appsettings.json` key of the same name. Double-underscore (`__`) is the cross-platform hierarchy separator.

---

### ConnectionStrings__NorthwindConnection

| Property | Value |
|---|---|
| **Required** | No |
| **Default** | Falls back to `appsettings.json` value (Windows Integrated Auth to `.\SQL2016EXPRESS`) |
| **Consumers** | ASP.NET Core configuration provider in `Northwind` project and `CMSAdmin` |
| **Validation** | Standard ADO.NET / SQL Client connection string format |
| **Example** | `Server=localhost,1433;Database=Northwind;User Id=sa;Password=MyStr0ng!Pass;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;` |

---

## Invariants

1. `SA_PASSWORD` must be identical in all three places it is used:
   - The `SA_PASSWORD` variable itself (used by SQL Server and sqlcmd)
   - The `Password=` fragment of `ConnectionStrings__CarrotwareCMS`
   - The `Password=` fragment of `ConnectionStrings__NorthwindConnection`

2. `SQL_PORT` must be consistent between the Compose port mapping and the `Server=localhost,{SQL_PORT}` fragment in both connection strings.

3. `TrustServerCertificate=True` or `Encrypt=False` is required in both connection strings when connecting to the Developer container over localhost, as no trusted TLS certificate is configured.

4. Neither the `.env` file nor any file containing expanded values of `SA_PASSWORD` may be committed to source control.

---

## Backward Compatibility

Developers who do not set `ConnectionStrings__CarrotwareCMS` or `ConnectionStrings__NorthwindConnection` continue to use the existing Windows Integrated Authentication connection strings from `appsettings.json`. Their workflow is unaffected. The Docker setup is purely additive.
