# CarrotCakeCMS-Core Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-05

## Active Technologies
- Shell (bash), YAML — no .NET changes; existing .NET 8 / ASP.NET Core MVC application is unchanged + Docker Compose v2 plugin (`docker compose`); `mcr.microsoft.com/mssql/server:2022-developer-latest`; `/opt/mssql-tools18/bin/sqlcmd` (bundled in image) (001-docker-compose-dev-infra)
- SQL Server 2022 Developer Edition in Docker; named volume `carrotcake-sqldata` for persistence (001-docker-compose-dev-infra)
- C# / .NET 8 / ASP.NET Core Web API — same runtime as all + `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.10; (002-headless-rest-api)
- SQL Server (same `CarrotCoreMVC` database used by CMSAdmin); new (002-headless-rest-api)

- (001-docker-compose-dev-infra)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for 

## Code Style

: Follow standard conventions

## Recent Changes
- 002-headless-rest-api: Added C# / .NET 8 / ASP.NET Core Web API — same runtime as all + `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.10;
- 001-docker-compose-dev-infra: Added Shell (bash), YAML — no .NET changes; existing .NET 8 / ASP.NET Core MVC application is unchanged + Docker Compose v2 plugin (`docker compose`); `mcr.microsoft.com/mssql/server:2022-developer-latest`; `/opt/mssql-tools18/bin/sqlcmd` (bundled in image)

- 001-docker-compose-dev-infra: Added

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
