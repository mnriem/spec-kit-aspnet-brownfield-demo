# Implementation Plan: Headless REST API for CMS Content

**Branch**: `002-headless-rest-api` | **Date**: 2026-03-05 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-headless-rest-api/spec.md`

## Summary

Add a public read-only REST API to CarrotCakeCMS that exposes pages, blog posts,
navigation trees, content snippets, and widget zones as JSON over HTTPS. The API is
secured with JWT Bearer tokens issued by a new `/api/headless/token` endpoint. A new
`CMSHeadlessApi` ASP.NET Core Web API project is added alongside `CMSAdmin`, sharing
the existing `CarrotCakeContext`, `CMSCore` business logic, and `CMSSecurity` packages
without modifying any existing project. One new database table (`CarrotApiClient`)
is introduced via an EF migration to store hashed client credentials.

---

## Technical Context

**Language/Version**: C# / .NET 8 / ASP.NET Core Web API — same runtime as all
existing projects in the solution  
**Primary Dependencies**: `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.10;
`System.IdentityModel.Tokens.Jwt` 8.1.2 (already in solution);
`Microsoft.AspNetCore.Identity.EntityFrameworkCore` 8.0.10 (for `PasswordHasher<T>`);
`Microsoft.EntityFrameworkCore.SqlServer` 8.0.10  
**Storage**: SQL Server (same `CarrotCoreMVC` database used by CMSAdmin); new
`CarrotApiClients` table via EF migration  
**Testing**: No automated test project in this feature scope per brownfield baseline;
manual integration via test scripts in `specs/002-headless-rest-api/test-scripts/`;
`// TODO(TEST):` deferral comments required on all new service methods  
**Target Platform**: Same Windows/macOS/Linux server targets as CMSAdmin  
**Project Type**: Web API service — headless content delivery layer  
**Performance Goals**: p95 < 200 ms on warm cache; p95 < 800 ms cold (SC-002, matching
constitution Principle V baselines)  
**Constraints**: Read-only; no write operations; no admin routes; JWT only (no cookie
auth); all content queries use `AsNoTracking()`; no N+1 queries  
**Scale/Scope**: ≥ 200 concurrent connections (SC-005); single issuer/audience JWT;
multi-site CMS supported via `siteId` query parameter

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

This feature introduces a new `CMSHeadlessApi` project and one EF migration. No
existing project, view, or data model is modified.

| Principle | Status | Notes |
|---|---|---|
| I — Layered Architecture Fidelity | ⚠️ NEW PROJECT — justified | A new `CMSHeadlessApi` deployment unit sits alongside `CMSAdmin` in the hierarchy, referencing `CMSCore`, `CarrotCMSData`, `CMSSecurity`. JWT authentication cannot coexist cleanly in CMSAdmin's cookie-auth pipeline (see Complexity Tracking). No cross-layer shortcuts introduced. |
| II — Plugin Contract Compliance | ✅ N/A | No new plugins, widgets, or admin modules introduced. |
| III — Testability by Design | ✅ DESIGNED-IN | `ContentQueryService` and `TokenService` are extracted as injected services behind `IContentQueryService` / `ITokenService` interfaces. `CarrotCakeContext` is constructor-injected, not `Create()`-called. Every new service method carries `// TODO(TEST):` per constitution. |
| IV — Security by Default | ✅ PASS | All content endpoints carry `[Authorize]`; token endpoint is the only `[AllowAnonymous]` path with inline comment justification. Client secrets hashed with `PasswordHasher<CarrotApiClient>` (PBKDF2-SHA256). JWT signing key sourced from env var. No antiforgery token needed (stateless API, no form POSTs). |
| V — Performance Discipline | ✅ PASS | All EF reads use `AsNoTracking()`. N+1 eliminated by JOIN-in-LINQ for categories/tags. Site-scoped metadata cached via `IMemoryCache` (TTL ≤ 5 min). `AddResponseCaching()` registered. |
| VI — UX Consistency | ✅ N/A | No admin views introduced. |
| VII — Structured Observability | ✅ PASS | Controllers inject `ILogger<T>`; all unhappy paths log `Warning` or `Error` before returning. No `catch { }` blocks without inline comment. `CarrotFileLogger` wired in `appsettings.json`. |
| Technology Constraints (.NET 8) | ✅ PASS | New project targets `net8.0`; all EF Core packages at `8.0.10`. |
| Technology Constraints (SQL Server) | ✅ PASS | Same connection string and `CarrotCakeContext` as CMSAdmin. |
| Technology Constraints (EF migrations) | ✅ PASS | `CarrotApiClient` table added via `dotnet ef migrations add AddCarrotApiClient` in `CarrotCMSData`. |

**Gate result: PASS with one justified layer violation. Complexity Tracking entry required.**

**Post-design re-check (Phase 1)**: Confirmed. The design reuses all existing layers
correctly. No new abstractions are introduced beyond the minimum required. The new
project is the smallest possible deployment unit for the JWT-only API.

---

## Project Structure

### Documentation (this feature)

```text
specs/002-headless-rest-api/
├── plan.md                        # This file
├── research.md                    # Phase 0 — 11 decisions documented
├── data-model.md                  # Phase 1 — CarrotApiClient entity + DTO projections
├── quickstart.md                  # Phase 1 — developer setup guide
├── contracts/
│   ├── auth.md                    # Phase 1 — token endpoint + JWT contract
│   └── endpoints.md               # Phase 1 — full route/request/response contracts
├── test-scripts/
│   ├── headless-api.http          # VS Code REST Client test file (all endpoints)
│   └── test-api.sh                # Bash curl smoke-test script
└── tasks.md                       # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
CMSHeadlessApi/                       # NEW project
├── CMSHeadlessApi.csproj             # Web API, net8.0; refs CMSCore, CMSData, CMSSecurity, CarrotLog
├── Program.cs                        # Minimal host: DI wiring, JWT Bearer auth, routing
├── appsettings.json                  # Connection string, HeadlessApi JWT config (key = placeholder)
├── appsettings.Development.json      # Dev overrides (key from user-secrets or env var)
├── Properties/
│   └── launchSettings.json
├── Controllers/
│   ├── TokenController.cs            # POST /api/headless/token  [AllowAnonymous]
│   ├── PagesController.cs            # GET /api/headless/pages   [Authorize]
│   ├── PostsController.cs            # GET /api/headless/posts   [Authorize]
│   ├── NavigationController.cs       # GET /api/headless/navigation [Authorize]
│   ├── SnippetsController.cs         # GET /api/headless/snippets  [Authorize]
│   └── WidgetZonesController.cs      # GET /api/headless/widgetzones [Authorize]
├── Models/
│   ├── Dto/
│   │   ├── PageSummaryDto.cs
│   │   ├── PageDto.cs
│   │   ├── PostSummaryDto.cs
│   │   ├── PostDto.cs
│   │   ├── CategoryDto.cs
│   │   ├── TagDto.cs
│   │   ├── NavigationNodeDto.cs
│   │   ├── SnippetDto.cs
│   │   └── WidgetInstanceDto.cs
│   ├── Request/
│   │   ├── TokenRequest.cs           # clientId, clientSecret
│   │   ├── PageQueryParams.cs        # siteId, slug, page, pageSize
│   │   ├── PostQueryParams.cs        # siteId, slug, category, tag, dateFrom, dateTo, page, pageSize
│   │   ├── NavigationQueryParams.cs  # siteId
│   │   ├── SnippetQueryParams.cs     # siteId, name
│   │   └── WidgetZoneQueryParams.cs  # siteId, pageSlug, zone
│   └── Response/
│       ├── ApiResponse.cs            # ApiResponse<T>, ApiMeta
│       ├── PagedApiResponse.cs       # PagedApiResponse<T>, PagedApiMeta
│       └── TokenResponse.cs          # token, tokenType, expiresInSeconds, expiresAt
└── Services/
    ├── IContentQueryService.cs
    ├── ContentQueryService.cs        # Wraps CarrotCakeContext + CannedQueries + SiteNavHelperReal
    ├── ITokenService.cs
    └── TokenService.cs               # JWT issuance + CarrotApiClient lookup

CarrotCMSData/
└── Models/
    └── CarrotApiClient.cs            # NEW entity (see data-model.md)
    # (CarrotCakeContext.cs updated:  DbSet<CarrotApiClient> + Fluent mapping)

CarrotCMSData/Migrations/
└── <timestamp>_AddCarrotApiClient.cs # NEW migration
```

**Structure Decision**: New `CMSHeadlessApi` project at repository root. All new artefacts
(controllers, services, DTOs, request/response models) are self-contained within it. The
only changes to existing projects are the `CarrotApiClient` entity and `DbSet` addition to
`CarrotCMSData` and the EF migration. `CMSAdmin` is completely unmodified.

---

## Complexity Tracking

> **New `CMSHeadlessApi` project — justified layer addition**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| New web project (CMSHeadlessApi) alongside CMSAdmin | JWT Bearer authentication requires its own ASP.NET Core authentication pipeline with `AddAuthentication().AddJwtBearer()`. CMSAdmin uses `ConfigureCmsAuth()` which configures cookie authentication, sliding cookie expiry, and `IdentityOptions` — adding JWT Bearer to the same pipeline creates scheme ambiguity on shared routes and risks breaking admin auth silently. Additionally, the headless API is a separately deployable unit (can be scaled independently, has no admin views, serves different consumers). | Adding an Area to CMSAdmin would force dual-scheme resolution (`[Authorize(AuthenticationSchemes="Bearer")]` on every controller) and couples the headless API lifecycle to admin deployments, increasing regression risk. |

---

## Architecture: Where to Add the New API

The headless API lives in a new **`CMSHeadlessApi`** project (ASP.NET Core Web API,
`net8.0`) at the repository root, added to `CarrotCakeCoreMVC_All.sln`. It is a
first-class peer to `CMSAdmin`, not an area or sub-application of it.

**Layer placement**: `CMSHeadlessApi` sits at the same level as `CMSAdmin` in the
application layer, consuming:
- `CMSCore` — `ContentPageHelper`, `SiteNavHelperReal`, `CannedQueries`, `SiteData`,
  `ContentPageType`, `ContentSnippet`
- `CarrotCMSData` — `CarrotCakeContext`, all entity/view models
- `CMSSecurity` — `PasswordHasher<T>` via Identity packages
- `CarrotLog` — `CarrotFileLogger` for structured file logging

**No existing project is modified** except `CarrotCMSData` (new entity + DbSet) and
`CarrotCakeCoreMVC_All.sln` (new project reference).

---

## Authentication Design

### Token Issuance

```
POST /api/headless/token
Body: { "clientId": "my-spa", "clientSecret": "plain-text-over-tls" }
```

1. Look up `CarrotApiClient` by `ClientId` in the database.
2. Verify `IsActive=true` and `ExpiresDateUtc` is null or in the future.
3. Verify the secret with `PasswordHasher<CarrotApiClient>.VerifyHashedPassword(client, client.ClientSecretHash, plainTextSecret)`.
4. On success: issue a JWT signed with HMAC-SHA256 using the key from
   `CARROT_HEADLESS_JWT_KEY` environment variable (falls back to `HeadlessApi:JwtKey`
   in appsettings for local development only).
5. JWT payload includes: `sub` (ClientId), `jti` (Guid), `iat`, `exp`, `iss`
   (`"CarrotCakeCMS"`), `aud` (`"CarrotCakeHeadlessConsumers"`), `site_scope`
   (ScopeToSiteId or `"*"`).
6. Token expiry: `HeadlessApi:TokenExpiryMinutes` (default 60).

On failure: always return HTTP 401 with a generic message (no distinction between
"not found" and "wrong secret" to prevent enumeration attacks).

### Bearer Token Validation

`CMSHeadlessApi/Program.cs` wires:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidIssuer = config["HeadlessApi:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["HeadlessApi:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Convert.FromBase64String(jwtKey))
        };
    });
```

All controllers carry `[Authorize]` (JWT Bearer scheme is the default and only scheme).
`TokenController` carries `[AllowAnonymous]` with a comment:
```csharp
// AllowAnonymous: this is the credential-exchange endpoint itself; no existing
// token is available at this stage. Transport-layer security (HTTPS) is the guard.
```

### Site Scope Enforcement

If the token's `site_scope` claim is not `"*"`, any request targeting a different
`siteId` receives HTTP 403. The check is applied in `ContentQueryService` before
any database query.

### Client Credential Storage

New entity `CarrotApiClient` (see [data-model.md](data-model.md)). Key fields:
`ClientId` (human-readable, unique), `ClientSecretHash` (PBKDF2-SHA256 via
`PasswordHasher<T>`), `ScopeToSiteId` (nullable FK to `CarrotSite`), `IsActive`,
`ExpiresDateUtc`. Initial client provisioning is a manual DB insert; an admin UI is
out of scope.

---

## Endpoint Design

Full request/response contracts are in [contracts/endpoints.md](contracts/endpoints.md)
and [contracts/auth.md](contracts/auth.md). Summary of routes:

| Method | Route                          | Auth | Description                              |
|--------|-------------------------------|------|------------------------------------------|
| POST   | `/api/headless/token`         | None | Exchange credentials for JWT             |
| GET    | `/api/headless/pages`         | JWT  | List pages or get page by `?slug=`       |
| GET    | `/api/headless/posts`         | JWT  | List posts (filtered) or get by `?slug=` |
| GET    | `/api/headless/navigation`    | JWT  | Full navigation tree for a site          |
| GET    | `/api/headless/snippets`      | JWT  | Get snippet by `?name=`                  |
| GET    | `/api/headless/widgetzones`   | JWT  | Widget zone instances by page+zone name  |
| GET    | `/health`                     | None | ASP.NET Core health check                |

**Key design decisions**:
- Slug lookup uses `?slug=` query parameter (not a path segment) because CarrotCakeCMS
  slugs contain forward slashes (`/about-us`, `/blog/2025/01/hello`) that conflict with
  route parameter capture.
- The `pages` and `posts` endpoints are dual-purpose (list or single-item) gated by
  presence of `?slug=`, reducing route surface without losing clarity.
- `siteId` is optional only when the CMS has exactly one site; the service layer returns
  HTTP 400 if multi-site and `siteId` is absent.

---

## Data Access Layer

### Service: `ContentQueryService`

`ContentQueryService` is the single component that touches `CarrotCakeContext` for content
reads. It is registered as `Scoped` in DI (matching DbContext lifetime).

```csharp
public interface IContentQueryService {
    Task<(List<PageSummaryDto> Items, int Total)> GetPagesAsync(PageQueryParams p, CancellationToken ct);
    Task<PageDto?> GetPageBySlugAsync(Guid siteId, string slug, CancellationToken ct);
    Task<(List<PostSummaryDto> Items, int Total)> GetPostsAsync(PostQueryParams p, CancellationToken ct);
    Task<PostDto?> GetPostBySlugAsync(Guid siteId, string slug, CancellationToken ct);
    Task<List<NavigationNodeDto>> GetNavigationAsync(Guid siteId, CancellationToken ct);
    Task<SnippetDto?> GetSnippetByNameAsync(Guid siteId, string name, CancellationToken ct);
    Task<List<WidgetInstanceDto>> GetWidgetZoneAsync(Guid siteId, string pageSlug, string zone, CancellationToken ct);
    Task<bool> SiteExistsAsync(Guid siteId, CancellationToken ct);
    Task<Guid?> GetDefaultSiteIdAsync(CancellationToken ct);
}
```

**Published content predicate** (applied to all content queries):
```csharp
ct.IsLatestVersion == true
&& ct.PageActive == true
&& ct.GoLiveDate < DateTime.UtcNow
&& ct.RetireDate > DateTime.UtcNow
&& ct.SiteId == siteId
```
Source: `CannedQueries.GetAllByTypeList` in `CMSCore` (the authoritative published gate).

**Page type filter**:
- Pages: `ct.ContentTypeId == ContentPageType.GetIDByType(ContentPageType.PageType.ContentEntry)`
- Posts: `ct.ContentTypeId == ContentPageType.GetIDByType(ContentPageType.PageType.BlogEntry)`

**Category + tag filters** (posts only):
- Category: join `vwCarrotCategoryUrls` on `SiteId` + `CategoryUrl`, then join
  `CarrotCategoryContentMappings` — mirrors `CannedQueries.GetContentByCategoryURL`.
- Tag: join `vwCarrotTagUrls` on `SiteId` + `TagUrl`, then join
  `CarrotTagContentMappings` — mirrors `CannedQueries.GetContentByTagURL`.
- Both applied: chained `.Where()` clauses (AND logic); combined query avoids N+1.

**Post categories and tags** (per page of results): fetched as a single query joining
`CarrotCategoryContentMappings` → `CarrotContentCategories` (and same for tags) for all
`RootContentId` values in the result page. Materialised to a dictionary before projecting
to DTO — zero N+1 queries.

**Navigation tree building**:
1. Call `SiteNavHelperReal.GetMasterNavigation(siteId, bActiveOnly: true)`.
2. Filter to `ShowInSiteNav == true`.
3. Build tree from flat list: root nodes have `Parent_ContentID == null`; children
   are attached via dictionary keyed by `Root_ContentID` (O(n), single pass).

**Widget zone query**: resolve the page's `RootContentId` from the slug first (one
query on `vwCarrotContents`), then query `vwCarrotWidgets` filtered by `RootContentId`,
`PlaceholderName`, and the active/published gate. Return ordered by `WidgetOrder`.

**Invariant**: every `IQueryable` in `ContentQueryService` terminates with
`.AsNoTracking()`. No entity graph navigation is performed after materialisation.

---

## Multi-site Support

| Scenario | Behaviour |
|----------|-----------|
| `siteId` absent, single-site CMS | `GetDefaultSiteIdAsync()` returns the one site's ID automatically |
| `siteId` absent, multi-site CMS | HTTP 400: `"siteId is required for multi-site installations"` |
| `siteId` provided, site exists | Query proceeds scoped to that site |
| `siteId` provided, site not found | `SiteExistsAsync()` → false → HTTP 404 |
| Token has scoped `site_scope ≠ siteId` | HTTP 403: `"Token is not authorized for site {siteId}"` |
| Same slug in multiple sites, `siteId` absent | Resolved by case 2 above — HTTP 400 forces disambiguation |

All `siteId` values are validated as RFC 4122 GUIDs by model binding before reaching
service methods; malformed values return HTTP 400.

Site metadata (`CarrotSites` table) is cached in `IMemoryCache` (TTL 5 minutes) to
avoid a DB round-trip on every request — consistent with constitution Principle V.

---

## Pagination Strategy

- **Query parameters**: `page` (int, 1-based, default 1, min 1) and `pageSize` (int,
  default 20, min 1, max 100).
- **Validation**: `[Range]` attributes on model + `ModelState`; invalid values → HTTP 400
  via `ProblemDetails`.
- **Implementation**:
  ```csharp
  int total = await query.CountAsync(ct);
  var items = await query.Skip((page-1) * pageSize).Take(pageSize).ToListAsync(ct);
  ```
- **Response fields**: `total`, `totalPages` (`ceil(total/pageSize)`), `page`, `pageSize`.
- **Out-of-range page**: empty `data` array, HTTP 200, accurate `total` (per spec edge case).

---

## Error Handling Conventions

All error responses use ASP.NET Core `ProblemDetails` (RFC 7807), enabled by
`builder.Services.AddProblemDetails()`.

| Condition | HTTP | `detail` |
|-----------|------|----------|
| Parameter validation failure | 400 | Field-level messages in `errors` dict |
| `siteId` absent, multi-site | 400 | `"siteId is required for multi-site installations"` |
| Malformed GUID | 400 | `"siteId must be a valid GUID"` |
| Missing required param | 400 | `"Parameter '{name}' is required"` |
| `dateTo` < `dateFrom` | 400 | `"dateTo must be on or after dateFrom"` |
| Missing/invalid bearer token | 401 | ASP.NET Core default challenge response |
| Token expired | 401 | ASP.NET Core default challenge response |
| Token site scope mismatch | 403 | `"Token is not authorized for site {siteId}"` |
| Site not found | 404 | `"No site found with id '{siteId}'"` |
| Page not found | 404 | `"No published page found with slug '{slug}'"` |
| Post not found | 404 | `"No published post found with slug '{slug}'"` |
| Snippet not found | 404 | `"No active snippet found with name '{name}'"` |
| Unhandled exception | 500 | Generic message only — detail logged, never exposed |

**Logging**:
- Controllers inject `ILogger<T>`.
- 400 paths: `LogDebug`; 404 paths: `LogInformation`; exceptions: `LogError(ex, ...)`.
- No `catch { }` blocks without an inline comment.
- No exception `.Message` or stack trace returned to clients (OWASP information
  exposure; constitution Principle VII).

---

## Test Script Artifacts

Two test artefacts are in `specs/002-headless-rest-api/test-scripts/`:

### `headless-api.http` — VS Code REST Client

Compatible with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)
extension. Variables at the top configure the target environment. The token request
auto-captures the JWT via `@token = {{tokenRequest.response.body.$.data.token}}`.

**Scenarios covered**: 27 named requests spanning all seven endpoint groups:
token (valid, invalid, missing field), pages (list, pagination, by slug, 404, 401),
posts (list, category filter, tag filter, date range, combined, out-of-range page,
invalid date, by slug), navigation (tree, invalid siteId, missing siteId), snippets
(by name, 404, missing param), widget zones (by page+zone, 404, missing param),
and health check.

### `test-api.sh` — Bash curl smoke tests

Environment variable driven (`BASE_URL`, `CLIENT_ID`, `CLIENT_SECRET`, `SITE_ID`, etc.).
Requires `curl` and `jq`. Prints `PASS`/`FAIL` per test, exits non-zero on failures.

```bash
BASE_URL=http://localhost:5000 \
CLIENT_ID=dev-client \
CLIENT_SECRET=my-super-secret \
SITE_ID=<guid> \
  specs/002-headless-rest-api/test-scripts/test-api.sh
```

---

## Dependencies and Sequencing

### Files to Create / Modify

| # | File | Change Type |
|---|------|-------------|
| 1 | `CarrotCMSData/Models/CarrotApiClient.cs` | NEW — EF entity |
| 2 | `CarrotCMSData/CarrotCakeContext.cs` | MODIFY — add `DbSet<CarrotApiClient>` + Fluent mapping |
| 3 | `CarrotCMSData/Migrations/<ts>_AddCarrotApiClient.cs` | NEW — via `dotnet ef migrations add` |
| 4 | `CMSHeadlessApi/CMSHeadlessApi.csproj` | NEW — Web API project |
| 5 | `CMSHeadlessApi/Program.cs` | NEW — DI, JWT auth, routing |
| 6 | `CMSHeadlessApi/appsettings.json` | NEW — config |
| 7 | `CMSHeadlessApi/Models/Response/ApiResponse.cs` | NEW |
| 8 | `CMSHeadlessApi/Models/Response/PagedApiResponse.cs` | NEW |
| 9 | `CMSHeadlessApi/Models/Response/TokenResponse.cs` | NEW |
| 10 | `CMSHeadlessApi/Models/Request/*.cs` (6 files) | NEW — query param models |
| 11 | `CMSHeadlessApi/Models/Dto/*.cs` (9 files) | NEW — DTO types |
| 12 | `CMSHeadlessApi/Services/ITokenService.cs` + `TokenService.cs` | NEW |
| 13 | `CMSHeadlessApi/Services/IContentQueryService.cs` + `ContentQueryService.cs` | NEW |
| 14 | `CMSHeadlessApi/Controllers/TokenController.cs` | NEW |
| 15 | `CMSHeadlessApi/Controllers/PagesController.cs` | NEW |
| 16 | `CMSHeadlessApi/Controllers/PostsController.cs` | NEW |
| 17 | `CMSHeadlessApi/Controllers/NavigationController.cs` | NEW |
| 18 | `CMSHeadlessApi/Controllers/SnippetsController.cs` | NEW |
| 19 | `CMSHeadlessApi/Controllers/WidgetZonesController.cs` | NEW |
| 20 | `CarrotCakeCoreMVC_All.sln` | MODIFY — add new project |

### Recommended Implementation Order

```
Step 1 — Entity + migration
  CarrotApiClient.cs → update CarrotCakeContext.cs → run migration

Step 2 — Project scaffolding
  CMSHeadlessApi.csproj → Program.cs → appsettings.json

Step 3 — Models
  Response envelopes → Request param models → DTO types

Step 4 — Services
  TokenService (JWT issuance) → ContentQueryService (content reads)

Step 5 — Controllers (each independently testable after Step 4)
  TokenController → PagesController → PostsController →
  NavigationController → SnippetsController → WidgetZonesController

Step 6 — Integration validation
  Provision test CarrotApiClient row →
  Run test-api.sh → verify PASS/FAIL results →
  Spot-check headless-api.http in VS Code
```

### External Pre-conditions

| Pre-condition | Required for |
|---------------|-------------|
| `CarrotCoreMVC` DB with ≥ 1 site + ≥ 1 published page | Content endpoint tests |
| `CARROT_HEADLESS_JWT_KEY` env var set | Token issuance (prod/CI); `dotnet user-secrets` for local dev |
| `humao.rest-client` VS Code extension | Running `headless-api.http` |
| `jq` CLI | Running `test-api.sh` |
| Docker Compose (feature 001) | Optional alternative to local SQL Server |

### Out of Scope

- Write operations (create, update, delete) — FR-004
- Anonymous content access
- Real-time endpoints (WebSockets, webhooks)
- Full-text search
- Media/file serving
- Admin UI for managing `CarrotApiClient` records
- Rate limiting (infrastructure layer per spec)
- API version prefix (`/v1/`) — deferred

