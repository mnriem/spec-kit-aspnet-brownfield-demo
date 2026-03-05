# CarrotCakeCMS (MVC Core)
Source code for CarrotCakeCMS (MVC - Core), .Net Core 8

[SITE_CT]: http://www.carrotware.com/contact?from=github-core
[REPO_SF]: http://sourceforge.net/projects/carrotcakecmscore/
[REPO_GH]: https://github.com/ninianne98/CarrotCakeCMS-Core/

[DOC_PDF]: http://www.carrotware.com/fileassets/CarrotCakeCoreDevNotes.pdf?from=github-core
[DOC]: http://www.carrotware.com/carrotcake-download?from=github-core "CarrotCakeCMS User Documentation"
[TMPLT]: http://www.carrotware.com/carrotcake-templates?from=github-core
[IDE]: https://visualstudio.microsoft.com/
[BUILD22]: https://learn.microsoft.com/en-us/visualstudio/msbuild/walkthrough-using-msbuild?view=vs-2022
[VS2022C]: https://visualstudio.microsoft.com/vs/community/
[SQL]: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
[SSMS]: https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms

Welcome to the GitHub project for CarrotCake CMS MVC Core, an open source c# project. CarrotCake is a [template-based][TMPLT] MVC .Net Core CMS (content management system) built with C#, SQL server, jQueryUI, and TinyMCE, providing an intuitive WYSIWYG/drag and drop edit experience. This content management system supports multi-tenant webroots with shared databases. 

## If you have found this tool useful please [contact us][SITE_CT].

Source code and [documentation][DOC_PDF] is available on [GitHub][REPO_GH] and [SourceForge][REPO_SF]. Documentation and assemblies can be found [here][DOC].

Some features include: blogging engine, configurable date based blog post URLs, blog post content association with categories and tags, assignment/customization of category and tag URL patterns, simple content feedback collection and review, blog post pagination/indexes (with templating support), designation of default listing blog page (required to make search, category links, or tag links function), URL date formatting patterns, RSS feed support for posts and pages, import and export of site content, and import of content from WordPress XML export files.

Other features also include date based release and retirement of content - allowing you to queue up content to appear or disappear from your site on a pre-arranged schedule, site time-zone designation, site search, and ability to rename the administration folder. Supports the use of layout views to provide re-use when designing content view templates.

---

## CarrotCakeCMS (MVC Core) Developer Quick Start Guide

Copyright (c) 2011, 2015, 2023, 2024 Samantha Copeland
Licensed under the MIT or GPL v3 License

CarrotCakeCMS (MVC Core) is maintained by Samantha Copeland

### Install Development Tools

1. **[Visual Studio Community/Pro/Enterprise][IDE]** ([VS 2022 Community][VS2022C])  Typically being developed on VS 2022 Enterprise. Use of [MSBuild for 2022][BUILD22] is also acceptable. Both require patch version 17.8 or later, for .Net 8 support. 
1. **[SQL Server Express 2016 (or higher/later)][SQL]** - currently vetted on 2016 and 2019 (Express Editions).  Entity Framework Core 8 does not work with older versions of SQL Server, such as 2014/2012/2008R2 and earlier.
1. **[SQL Server Management Studio (SSMS)][SSMS]** - required for managing the database

### Get the Source Code

1. Go to the repository ([GitHub][REPO_GH] or [SourceForge][REPO_SF]) in a browser

1. Download either a ZIP archive or connect using either a GIT or SVN client to check out

### Open the Project

1. Start **Visual Studio**

1. Open **CarrotCakeCoreMVC.sln** solution in the root of the repository

	Note: If your file extensions are hidden, you will not see the ".sln"
	Other SLN files are demo widgets for how to wire in custom code/extensions

1. Edit **appsettings.json** under **CMSAdmin** root directory (this corresponds to the **CMSAdminCore** project)

	- In the ConnectionStrings section, configure the CarrotwareCMS value to point to your server and the name of your database.
		Note: the credentials require database owner/dbo level as it will create the database artifacts for you.
	- In the SmtpSettings, configure the pickupDirectoryLocation to a directory on your development machine (for testing purposes).

1. Right-click on **CMSAdminCore** and select **Set as StartUp Project**

1. Right-click on **CMSAdminCore** and select **Rebuild**. The project should download all required NuGet packages and compile successfully

	There may be some warnings, you can ignore them

1. To deploy a sample widget, select the individual project and select **Rebuild**.  The post build task will copy the widget views and assemblies into the main website project. 

1. SQL Server should be running with an empty database matching the one specified in the connection string. If you are running the code a second or later time, it will auto update if there are schema changes (see dbo note above).  
	- Do not share a database between the Core, MVC 5, and WebForms editions.  You can update the schema if you want to upgrade and take your existing data to the newer version.  
	- If you manually add the first EF migration to an existing MVC5 version of this CMS, it will automatically migrate the data.  This is not done automatically to prevent accidental or unintentional upgrades
	- Password hashes will not be valid when upgrading MVC 5 (or possibly earlier MVC Core versions) to MVC Core 8, so perform a password recovery to set valid ones.

### Make a backup FIRST when upgrading!

```sql
-- if you are coming from a database older than SQL 2016 as an upgrade from an earlier CMS version and are upgrading to SQL 2016 or later, run a compatibility update
-- https://learn.microsoft.com/en-us/sql/t-sql/statements/alter-database-transact-sql-compatibility-level?view=sql-server-ver16
-- COMPATIBILITY_LEVEL { 160 | 150 | 140 | 130 | 120 | 110 | 100 | 90 | 80 }
-- *REQUIRED* if seeing "SqlException: Incorrect syntax near the keyword 'WITH'. Incorrect syntax near the keyword 'with'. "

-- change the database from CarrotCoreMVC to whatever DB name you are actually using
ALTER DATABASE [CarrotCoreMVC]
	SET COMPATIBILITY_LEVEL =  130        -- SQL 2016

-- if you plan to use an existing database from the MVC 5 version, you will need to have some entries in the migrations table
-- password hashes from MVC 5 will be invalid, perform a password recovery to set valid ones

-- to create the migrations table:

--========================
CREATE TABLE [dbo].[__EFMigrationsHistory](
	[MigrationId] [nvarchar](150) NOT NULL,
	[ProductVersion] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED 
(
	[MigrationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO
--========================

-- main CMS MVC 5-> MVC Core 8 - create the ef table (if needed) and execute the insert for 00000000000000_Initial
-- the password hashes will be incorrect, so perform a password reset once the DB has been upgraded
IF (NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] where [MigrationId]='00000000000000_Initial')
			AND EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[membership_User]') AND type in (N'U'))) BEGIN
	insert into [__EFMigrationsHistory]([MigrationId],[ProductVersion])
		values ('00000000000000_Initial','8.0.0')
END

--========================
-- below are manual entry migrations in case you are upgrading from MVC5 to .Net Core, only run if the table already exists

-- photo gallery widget - create the ef table (if needed) and execute the insert for 20230625212349_InitialGallery
IF (NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] where [MigrationId]='20230625212349_InitialGallery')
			AND EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[tblGallery]') AND type in (N'U'))) BEGIN
	insert into [__EFMigrationsHistory]([MigrationId],[ProductVersion])
		values ('20230625212349_InitialGallery','8.0.0')
END

-- simple calendar widget - create the ef table (if needed) and execute the insert for 20230709210325_InitialCalendar
IF (NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] where [MigrationId]='20230709210325_InitialCalendar')
			AND EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[tblGallery]') AND type in (N'U'))) BEGIN
	insert into [__EFMigrationsHistory]([MigrationId],[ProductVersion])
		values ('20230709210325_InitialCalendar','8.0.0')
END

-- event calendar widget - create the ef table (if needed) and execute the insert for 20230723225354_InitialEventCalendar
IF (NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] where [MigrationId]='20230723225354_InitialEventCalendar')
			AND EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[tblGallery]') AND type in (N'U'))) BEGIN
	insert into [__EFMigrationsHistory]([MigrationId],[ProductVersion])
		values ('20230723225354_InitialEventCalendar','8.0.0')
END

-- faq widget - create the ef table (if needed) and execute the insert for 20240421191144_InitialFaq2
IF (NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] where [MigrationId]='20240421191144_InitialFaq2')
			AND EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[carrot_FaqCategory]') AND type in (N'U'))) BEGIN
	insert into [__EFMigrationsHistory]([MigrationId],[ProductVersion])
		values ('20240421191144_InitialFaq2','8.0.0')
END

--========================

-- to validate
select * from [__EFMigrationsHistory] where [MigrationId] like '%Initial%'
```

1. If the database is empty or has pending database changes, the EF migrations will be automatically applied.

1. The first time you start up the website, it will create the required artifacts in the database (tables/views/sprocs etc.)

1. Select run mode as IIS Express and click the **Play** button (or hit F5) in the main toolbar to launch CarrotCakeCMS

1. When you run the website with an empty user database, you will be prompted to create the first user

1. Once you have created a user, you can go to the login screen, enter the credentials

1. After successfully logging in, you can create and manage your new website

### Using CarrotCakeCMS Core

For additional information on how to use CarrotCakeCMS, please see the **[CarrotCakeCMS Documentation][DOC]**.

---

## Docker Developer Workflow

> **Prerequisites**: [Docker Desktop 4.x](https://www.docker.com/products/docker-desktop/) (includes the `docker compose` plugin) and .NET SDK 8.0.
>
> This workflow provides a local SQL Server instance via Docker — no local SQL Server installation required.
> The ASP.NET Core application (`CMSAdmin`) is always run directly with `dotnet run`, not containerised.

### First-Time Setup

**Step 1 — Create your `.env` file**

```bash
# macOS / Linux
cp .env.example .env

# Windows (Command Prompt)
copy .env.example .env
```

Open `.env` and set a strong `SA_PASSWORD`. The password must meet SQL Server complexity requirements:
at least 8 characters containing uppercase letters, lowercase letters, digits, and at least one
non-alphanumeric character (e.g. `!`, `@`, `#`).

Update the connection string placeholders in `.env` to use the same password:

```dotenv
SA_PASSWORD=MyStr0ng!Pass

ConnectionStrings__CarrotwareCMS='Server=localhost,1433;Database=CarrotCoreMVC;uid=sa;Password=MyStr0ng!Pass;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;'
ConnectionStrings__NorthwindConnection='Server=localhost,1433;Database=Northwind;uid=sa;Password=MyStr0ng!Pass;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;'
```

> **Security**: `.env` is listed in `.gitignore` and must never be committed to source control.

**Step 2 — Start the infrastructure**

```bash
docker compose up -d
```

This pulls the SQL Server 2022 Developer Edition image (first run, ~1.5 GB), starts `sqlserver`,
waits for it to become healthy, then runs `db-init` which creates and seeds both databases.

Watch the logs during initial setup:

```bash
docker compose logs -f db-init
```

Expected first-run output:

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

Verify SQL Server is healthy:

```bash
docker compose ps
# sqlserver should show (healthy)
```

**Step 3 — Run the application**

The `ConnectionStrings__CarrotwareCMS` and `ConnectionStrings__NorthwindConnection` environment
variables set in `.env` override the corresponding `ConnectionStrings:*` values in `appsettings.json`
via ASP.NET Core's built-in environment variable configuration provider. No `appsettings.json` file
is modified — developers without `.env` connection strings continue using Windows Integrated Auth
from `appsettings.json` unchanged.

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

Open a browser at `https://localhost:5001` (or the URL printed by `dotnet run`).

**Verify no source files were modified**:

```bash
git status
# appsettings.json files must not appear as modified
```

### Day-to-Day Commands

| Goal | Command |
|---|---|
| Start infrastructure | `docker compose up -d` |
| Stop infrastructure (preserve data) | `docker compose down` |
| View SQL Server logs | `docker compose logs sqlserver` |
| View init script logs | `docker compose logs db-init` |
| Full clean reset (destroys all data) | `docker compose down --volumes && docker compose up -d` |

### Data Persistence Guarantee

- **`docker compose down`** — stops containers but preserves the `carrotcake-sqldata` named volume.
  All developer data survives. On the next `docker compose up -d`, the `db-init` service detects
  existing databases and exits immediately without re-running any scripts (no-op restart).

  Expected `db-init` log on restart:

  ```
  [init] Checking for CarrotCoreMVC...
  [init] CarrotCoreMVC already exists, skipping.
  [init] Checking for Northwind...
  [init] Northwind already exists, skipping.
  [init] Done.
  ```

- **`docker compose down --volumes`** — removes the named volume. All Docker SQL Server data is
  permanently deleted. On the next `docker compose up -d`, both databases are re-created from the
  schema scripts. Use this only when you want a clean slate.

### Connection String Override (Cross-Platform)

The `ConnectionStrings__CarrotwareCMS` and `ConnectionStrings__NorthwindConnection` variables in
`.env` use the ASP.NET Core double-underscore convention to map to `ConnectionStrings:CarrotwareCMS`
and `ConnectionStrings:NorthwindConnection` in the configuration hierarchy. Environment variables
take precedence over `appsettings.json`, so:

- Developers **with** `.env` connection strings → connect to Docker SQL Server using SA auth.
- Developers **without** `.env` connection strings → fall back to `appsettings.json` (Windows
  Integrated Auth, or whatever the file specifies). No code change required.

### Edge Cases & Troubleshooting

**`docker compose up` fails immediately with "SA_PASSWORD must be set"**

You have not created a `.env` file, or `SA_PASSWORD` is empty. Complete Step 1 above.

**`sqlserver` shows `(unhealthy)` or never becomes healthy**

1. Run `docker compose logs sqlserver` and look for password complexity errors.
2. Ensure `SA_PASSWORD` is at least 8 characters and contains uppercase, lowercase, a digit, and a
   non-alphanumeric symbol.
3. If the password does not meet SQL Server's requirements, the container starts and then exits.
   Fix `SA_PASSWORD` in `.env`, then run `docker compose down && docker compose up -d`.

**Port 1433 is already in use (local SQL Server running)**

Add `SQL_PORT=14330` (or any free port) to `.env` and update both connection strings to use that port:

```dotenv
SQL_PORT=14330
ConnectionStrings__CarrotwareCMS='Server=localhost,14330;Database=CarrotCoreMVC;uid=sa;Password=MyStr0ng!Pass;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;'
ConnectionStrings__NorthwindConnection='Server=localhost,14330;Database=Northwind;uid=sa;Password=MyStr0ng!Pass;MultipleActiveResultSets=true;Encrypt=False;TrustServerCertificate=True;'
```

Then restart: `docker compose down && docker compose up -d`

**Apple Silicon (M1/M2/M3) — slow startup or emulation warnings**

The SQL Server 2022 Developer Edition image does not have a native `linux/arm64` build. The
`compose.yaml` includes `platform: linux/amd64` to force Rosetta 2 / QEMU emulation via Docker
Desktop. This is expected and works correctly — startup may be slightly slower (~30–60 s extra) on
Apple Silicon compared to Intel. Ensure Rosetta integration is enabled in Docker Desktop settings
(enabled by default in Docker Desktop 4.x+).

**`docker compose up` fails with "Cannot connect to the Docker daemon"**

Docker Desktop is not running. Start Docker Desktop and wait for it to be fully ready before
re-running `docker compose up -d`.
