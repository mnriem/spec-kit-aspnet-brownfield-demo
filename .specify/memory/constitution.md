<!--
SYNC IMPACT REPORT
==================
Version change: (placeholder) → 1.0.0
Modified principles: N/A — initial fill from brownfield codebase analysis
Added sections: Core Principles (7), Technology Constraints, Development Workflow & Quality Gates, Governance
Removed sections: All placeholder tokens replaced
Templates updated:
  - .specify/templates/plan-template.md ✅ reviewed, no changes — speckit.plan reads constitution at feature time
  - .specify/templates/spec-template.md ✅ reviewed, no changes required
  - .specify/templates/tasks-template.md ✅ reviewed, no changes — speckit.tasks reads constitution at feature time
  - .specify/templates/checklist-template.md ✅ reviewed, no changes required
Follow-up TODOs:
  - TODO(RATIFICATION_DATE): Date set to 2026-03-04 (first constitution fill).
    If project formally adopted this CMS earlier, update to that historical date.
  - TODO(TEST_FRAMEWORK): Codebase currently has zero xUnit/NUnit/MSTest projects.
    Principle III mandates adding a test project. Track setup as a backlog item.
-->

# CarrotCakeCMS Core Constitution

## Core Principles

### I. Layered Architecture Fidelity

All feature work MUST respect the established project layer boundaries. The canonical
layer stack is:

- `CarrotCMSData` — EF Core models, `DbContext`, migrations (no business logic)
- `CMSInterfaces` — Shared contracts: `IWidget`, `IAdminModule`, `ICmsComponent`, etc.
- `CMSCore` — CMS business logic, site/page/widget orchestration, security checks
- `CMSSecurity` — ASP.NET Core Identity wrappers, `ManageSecurity`
- `CMSComponents` — Razor view components for front-end page rendering
- `WebComponents` — Reusable UI primitives (grid, captcha, calendar, etc.)
- `CMSAdmin` — Admin MVC controllers, views, and models (deployed web application)
- Plugin projects (`PluginXxx`) — Self-contained extensions; MUST NOT depend on `CMSAdmin`

Cross-layer shortcuts (e.g., a plugin referencing `CMSAdmin` internals, or a data
model containing presentation logic) are prohibited without documented rationale.
New projects MUST have a clearly justified place in this hierarchy before creation.

### II. Plugin Contract Compliance

Every widget or admin module extension MUST implement the appropriate interface contract:

- Widgets: implement `IWidget`; UI-configurable properties MUST carry `[Widget]` attribute
- Admin modules: implement `IAdminModule`; route via `[CmsAdminRoute]`
- Widget controllers: implement `IWidgetController` or `IWidgetDataObject`
- Deploy via post-build task that copies assemblies and views into `CMSAdmin/wwwroot`
  or the designated output path — manual copy is not acceptable in CI builds

`CmsTestActivator` is the approved mechanism for injecting test site context into
widget controllers without requiring a live database. New plugins MUST wire this in
their standalone `Program.cs` so widget rendering can be verified without the full CMS.

### III. Testability by Design (NON-NEGOTIABLE)

This is a brownfield project with no automated test suite. Every new or changed feature
MUST leave the codebase more testable than it found it:

- Business logic extracted to service/helper classes MUST be independently testable
  (i.e., no direct `HttpContext` or `SiteData.CurrentSite` calls without an injected
  abstraction)
- New controller actions MUST handle the unhappy path explicitly; no silent `return null`
- When a new data-access helper class is introduced it MUST follow `IDisposable` and
  support construction via `CarrotCakeContext` injection (not only static `Create()`)
- The preferred testing approach is xUnit with `CmsTestActivator` for DI bypass;
  manual "Test" controller harnesses in plugin projects are acceptable for rendering
  verification only — they do not substitute for assertions
- Pull requests adding net-new business logic without at minimum one integration or
  contract test MUST document the explicit deferral with a `// TODO(TEST):` comment

### IV. Security by Default

All state-mutating HTTP actions MUST carry `[ValidateAntiForgeryToken]`. All CMS-admin
routes MUST carry `[CmsAuthorize]` or `[CmsAdminAuthorize]` unless explicitly annotated
`[AllowAnonymous]` with a comment justifying the exception.

Additional non-negotiable rules:

- Output of user-supplied content in HTML context MUST pass through `HtmlEncoder` or
  `HttpUtility.HtmlEncode` before rendering — no raw string concatenation into HTML
- Raw SQL via `DataHelper.ExecDataSet` is permitted only for schema introspection
  (migration history checks); all other data access MUST go through EF Core with
  parameterised LINQ — no string-interpolated queries
- Exception handlers in security-critical paths (auth, authorization checks) MUST NOT
  swallow exceptions silently; they MUST at minimum log via `SiteData.WriteDebugException`
  or `ILogger` before re-throwing or redirecting
- Sensitive configuration (connection strings, SMTP credentials) MUST remain in
  `appsettings.json` / environment variables and MUST NOT appear in source control

### V. Performance Discipline

Read-heavy query paths MUST apply `AsNoTracking()`. Queries executed more than once
per request on the hot content-serving path MUST use `EF.CompileQuery`. Site-scoped
data (site metadata, templates, user roles) MUST be cached via `IMemoryCache` with a
TTL ≤ 5 minutes using the `CacheInsert`/`CacheGet` pattern established in
`CarrotHttpHelper`. Every plugin `Program.cs` MUST register `AddResponseCaching()` and
call `app.UseResponseCaching()`.

Performance anti-patterns that are PROHIBITED in new code:

- N+1 queries (fetching a list then querying each element individually)
- Synchronous `.Result` or `.Wait()` on async operations inside request pipelines
- Loading large content collections without pagination or the existing `IPagedContent`
  abstraction

Target baselines for content-serving requests: p95 < 200 ms on warm cache;
p95 < 800 ms on cold cache against SQL Server Express.

### VI. UX Consistency in the Admin Interface

The admin interface MUST use the established layout family. Razor views MUST inherit
from one of: `_LayoutMain_ccc.cshtml`, `_LayoutMain_bs.cshtml`, or `_LayoutPopup_ccc.cshtml`
(popup dialogs). Custom one-off layouts are prohibited without a documented reason.

UX requirements:

- All form validation errors MUST be surfaced via the `_PublicValidError` partial or
  the Bootstrap-compatible equivalent; no raw `ModelState` dumps in production views
- Widget configuration dialogs MUST respect the `CmsSkin` theming system
- Every destructive admin action (delete content, bulk template change, import/overwrite)
  MUST present a confirmation step before execution
- Date/time inputs in the admin MUST use the `_datetime.cshtml` partial for consistency
- Admin navigation MUST remain usable when JavaScript is degraded; core CRUD operations
  MUST work without JavaScript

### VII. Structured Observability

Every exception or error condition that could affect content delivery or data integrity
MUST be logged. The following rules apply:

- Controllers MUST inject `ILogger<T>` and use it for warning/error events; the legacy
  `SiteData.WriteDebugException` call is acceptable in existing helpers but MUST NOT be
  the only logging in new controller code
- Silent `catch { }` or `catch (Exception ex) { }` with no body are prohibited in new
  code. Exceptions in non-critical paths (e.g., cache retrieval fallback) MUST at
  minimum use `catch { /* cache miss, continuing */ }` with an inline comment
- Log messages MUST be human-readable and include the calling context (method name,
  relevant IDs such as `SiteID`, `RootContentID`)
- The `CarrotFileLogger` custom provider is the approved structured log sink; its
  configuration in `appsettings.json` MUST define a `MinLevel` and log format

## Technology Constraints

**Runtime**: .NET 8 / ASP.NET Core MVC — no framework downgrades; keep all
`PackageReference` versions in sync across projects (currently `8.0.10` for EF Core
packages).

**Database**: SQL Server (Express 2016 or later, compatibility level 130+). Entity
Framework Core 8 Code First with `CarrotCakeContext`. All schema changes MUST go
through EF migrations (`dotnet ef migrations add`); manual DDL scripts in
`CarrotCMSData/Scripts/` are for reference or one-time migrations only.

**Identity**: ASP.NET Core Identity via `IdentityUser` / `IdentityRole`. Custom user
data is extended via `CarrotUserData` and `ExtendedUserData` — do not add columns
directly to `AspNetUsers` without a migration and a corresponding model update.

**Front-end (Admin)**: jQuery + jQueryUI + TinyMCE. No additional JavaScript frameworks
in the admin bundle without explicit approval. Client assets are versioned via the
`?cms={version}&ts={timestamp}` query-string cache-buster pattern.

**Email**: MailKit (`MimeMessage`) via `MailRequest` helper. SMTP settings from
`appsettings.json` `SmtpSettings` section. No direct `System.Net.Mail` usage.

**Logging**: `CarrotFileLogger` (structured file) + ASP.NET Core `ILogger<T>`.

**Cryptography**: BouncyCastle (`BouncyCastle.Cryptography`). No use of deprecated
`System.Security.Cryptography.MD5` or `SHA1` for new security-sensitive operations.

## Development Workflow & Quality Gates

### Pre-commit checklist

Before a change is merged:

1. No new `catch { }` blocks without inline justification comment
2. New POST controller actions carry `[ValidateAntiForgeryToken]`
3. New admin controller actions carry `[CmsAuthorize]` or documented exemption
4. Query-returning methods on new helper classes use `AsNoTracking()` where appropriate
5. Any new plugin project wires `CmsTestActivator` in its standalone `Program.cs`
6. `appsettings.json` contains no credentials or secrets
7. EF migrations are included if DbContext model changed

### Code review focus areas

- Layer boundary violations (plugin → CMSAdmin direct dependency)
- Silent exception swallowing
- Missing anti-forgery tokens on POST forms
- N+1 query patterns
- Missing `IDisposable` on DbContext wrappers

### Amendment procedure

Amendments to this constitution require:

1. A written rationale for the change
2. Identification of affected layers/principles
3. Update to `CONSTITUTION_VERSION` (MAJOR for breaking principle changes, MINOR for
   additions, PATCH for clarifications)
4. Update to `LAST_AMENDED_DATE`
5. Re-check of all `.specify/templates/` for alignment

This constitution supersedes any informal coding conventions established before its
ratification. Conflicts between this document and pre-existing patterns must be
resolved in favor of this constitution for new code; legacy code may be migrated
incrementally.

## Governance

This constitution is the authoritative source of engineering principles for
CarrotCakeCMS Core. All feature specifications, implementation plans, and code reviews
MUST reference and comply with it. Deviations require explicit documentation in the
relevant artifact (spec, plan, or PR description) with a justification under a
Complexity Tracking section.

The constitution is versioned, ratified, and amended through the process described in
the Development Workflow section above. Architects and senior contributors are
responsible for enforcing compliance during review. Any contributor may propose an
amendment via the same process.

**Version**: 1.0.0 | **Ratified**: 2026-03-04 | **Last Amended**: 2026-03-04
