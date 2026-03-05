# Tasks: Headless REST API for CMS Content

**Input**: Design documents from `/specs/002-headless-rest-api/`
**Prerequisites**: plan.md ✅, spec.md ✅, data-model.md ✅, contracts/auth.md ✅, contracts/endpoints.md ✅, research.md ✅, quickstart.md ✅

**Tests**: No automated test project in this feature scope per plan.md brownfield baseline. Test script artefacts (`headless-api.http`, `test-api.sh`) are included in Polish phase. `// TODO(TEST):` deferral comments are required on all new service methods.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. User Story 5 (Token Auth, P1) is implemented first because it is a hard prerequisite for all content stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this implements — [US1]–[US5] map to spec.md user stories
- Exact file paths are included in every task description

---

## Phase 1: Setup

**Purpose**: Create the new `CMSHeadlessApi` project and wire it into the solution. No existing project is modified except the solution file.

- [x] T001 Add `CMSHeadlessApi` project to `CarrotCakeCoreMVC_All.sln` via `dotnet sln add CMSHeadlessApi/CMSHeadlessApi.csproj`
- [x] T002 Create `CMSHeadlessApi/CMSHeadlessApi.csproj` targeting `net8.0` with ProjectReferences to `../CMSCore/CMSCore.csproj`, `../CarrotCMSData/CMSData.csproj`, `../CMSSecurity/` and `../CarrotLog/CarrotLog.csproj`, and NuGet packages: `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.10, `System.IdentityModel.Tokens.Jwt` 8.1.2, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 8.0.10, `Microsoft.EntityFrameworkCore.SqlServer` 8.0.10 per plan.md tech stack
- [x] T003 [P] Create `CMSHeadlessApi/appsettings.json` with `ConnectionStrings:CarrotwareCMS` placeholder and `HeadlessApi` section (`Issuer: "CarrotCakeCMS"`, `Audience: "CarrotCakeHeadlessConsumers"`, `TokenExpiryMinutes: 60`, `JwtKey: "DEV_ONLY_PLACEHOLDER_SET_ENV_VAR_IN_PROD"`) per `contracts/auth.md`
- [x] T004 [P] Create `CMSHeadlessApi/appsettings.Development.json` with dev override note for `HeadlessApi:JwtKey` (set via `dotnet user-secrets` or `CARROT_HEADLESS_JWT_KEY` env var) and `CarrotFileLogger` sink configuration per plan.md
- [x] T005 [P] Create `CMSHeadlessApi/Properties/launchSettings.json` with `http` (port 5100) and `https` (port 7100) profiles

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: EF entity and migration, shared model infrastructure, and the `Program.cs` host with full DI/auth pipeline. No user story work can begin until this phase is complete.

**⚠️ CRITICAL**: ContentQueryService, TokenService, and all controllers depend on the entity, response envelopes, and Program.cs being in place.

### EF Entity & Migration

- [x] T006 Create `CarrotCMSData/Models/CarrotApiClient.cs` with entity class: `ApiClientId` (Guid PK), `ClientId` (string, unique 3–128 chars), `ClientSecretHash` (string, PBKDF2), `ScopeToSiteId` (Guid?, FK → `carrot_Sites.SiteID`), `IsActive` (bool), `CreatedDateUtc` (DateTime), `ExpiresDateUtc` (DateTime?), `Description` (string?), navigation property `ScopeToSite` per `data-model.md`
- [x] T007 Modify `CarrotCMSData/CarrotCakeContext.cs` to add `DbSet<CarrotApiClient> CarrotApiClients` property and Fluent API mapping: table name `CarrotApiClients`, PK `ApiClientId` with `HasDefaultValueSql("NEWSEQUENTIALID()")`, unique index on `ClientId`, FK to `carrot_Sites` on `ScopeToSiteId` (nullable, no cascade delete) per `data-model.md`
- [x] T008 Run EF migration: `dotnet ef migrations add AddCarrotApiClient --context CarrotCakeContext --output-dir Migrations --project CarrotCMSData --startup-project CMSHeadlessApi` and verify the generated migration file in `CarrotCMSData/Migrations/`

### Shared Response Models

- [x] T009 [P] Create `CMSHeadlessApi/Models/Response/ApiResponse.cs` with `ApiResponse<T>` (properties: `T Data`, `ApiMeta Meta`) and `ApiMeta` (properties: `Guid RequestId`, `DateTime Timestamp`) per `contracts/endpoints.md`
- [x] T010 [P] Create `CMSHeadlessApi/Models/Response/PagedApiResponse.cs` with `PagedApiResponse<T>` (properties: `List<T> Data`, `PagedApiMeta Meta`) and `PagedApiMeta` (properties: `int Page`, `int PageSize`, `int Total`, `int TotalPages`, `Guid RequestId`, `DateTime Timestamp`) per `contracts/endpoints.md`

### Service Interface

- [x] T011 Create `CMSHeadlessApi/Services/IContentQueryService.cs` with full interface: `GetPagesAsync(PageQueryParams, CancellationToken)`, `GetPageBySlugAsync(Guid siteId, string slug, CancellationToken)`, `GetPostsAsync(PostQueryParams, CancellationToken)`, `GetPostBySlugAsync(Guid siteId, string slug, CancellationToken)`, `GetNavigationAsync(Guid siteId, CancellationToken)`, `GetSnippetByNameAsync(Guid siteId, string name, CancellationToken)`, `GetWidgetZoneAsync(Guid siteId, string pageSlug, string zone, CancellationToken)`, `SiteExistsAsync(Guid siteId, CancellationToken)`, `GetDefaultSiteIdAsync(CancellationToken)` per plan.md interface definition

### Program.cs Host Configuration

- [x] T012 Create `CMSHeadlessApi/Program.cs` skeleton: register `CarrotCakeContext` with SQL Server connection string, `IMemoryCache` (`AddMemoryCache`), `AddResponseCaching`, `AddProblemDetails`, `AddHealthChecks`, `CarrotFileLogger`, `IHttpContextAccessor` (`AddHttpContextAccessor`), `IContentQueryService` → `ContentQueryService` (Scoped), `ITokenService` → `TokenService` (Scoped); wire `app.UseResponseCaching()`, `app.MapHealthChecks("/health")`, `app.UseAuthentication()`, `app.UseAuthorization()`, `app.MapControllers()` per plan.md
- [x] T013 Configure JWT Bearer authentication in `CMSHeadlessApi/Program.cs`: `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` with `TokenValidationParameters` — `ValidateIssuer=true`, `ValidIssuer` from config, `ValidateAudience=true`, `ValidAudience` from config, `ValidateLifetime=true`, `ClockSkew=30s`, `ValidateIssuerSigningKey=true`, key sourced from `CARROT_HEADLESS_JWT_KEY` env var with `HeadlessApi:JwtKey` config fallback per plan.md and `contracts/auth.md`

**Checkpoint**: Project builds (`dotnet build CMSHeadlessApi`) and migration is present in `CarrotCMSData/Migrations/`. User story implementation can now begin.

---

## Phase 3: User Story 5 — Obtain an API Token (Priority: P1) 🎯 First MVP Gate

**Goal**: API consumers can exchange a client ID and secret for a signed JWT bearer token. This is a prerequisite for all content story endpoints.

**Independent Test**: `POST /api/headless/token` with valid `clientId`/`clientSecret` body → HTTP 200 with `data.token`, `data.tokenType: "Bearer"`, `data.expiresAt` in the future. `POST` with wrong secret → HTTP 401. `POST` with missing field → HTTP 400. Token is then usable on a content endpoint to verify auth pipeline end-to-end.

- [x] T014 [P] [US5] Create `CMSHeadlessApi/Models/Request/TokenRequest.cs` with properties `ClientId` (string, `[Required, StringLength(128, MinimumLength=3)]`) and `ClientSecret` (string, `[Required, StringLength(512, MinimumLength=8)]`) per `contracts/auth.md`
- [x] T015 [P] [US5] Create `CMSHeadlessApi/Models/Response/TokenResponse.cs` with properties `Token` (string), `TokenType` (string, value `"Bearer"`), `ExpiresInSeconds` (int), `ExpiresAt` (DateTime) per `contracts/auth.md`
- [x] T016 [US5] Create `CMSHeadlessApi/Services/ITokenService.cs` with single method `IssueTokenAsync(TokenRequest request, CancellationToken ct): Task<TokenResponse?>` (returns `null` on invalid credentials to allow generic 401) per plan.md
- [x] T017 [US5] Implement `CMSHeadlessApi/Services/TokenService.cs`: inject `CarrotCakeContext`, `IConfiguration`; in `IssueTokenAsync` — (1) look up `CarrotApiClient` by `ClientId` using `AsNoTracking()`, (2) return `null` if not found, `IsActive=false`, or `ExpiresDateUtc` in the past, (3) call `PasswordHasher<CarrotApiClient>.VerifyHashedPassword(client, client.ClientSecretHash, plainTextSecret)`, return `null` on mismatch, (4) on success build JWT with claims: `sub=ClientId`, `jti=Guid.NewGuid()`, `iat`, `exp`, `iss="CarrotCakeCMS"`, `aud="CarrotCakeHeadlessConsumers"`, `site_scope=ScopeToSiteId.ToString() ?? "*"` signed with HMAC-SHA256; add `// TODO(TEST):` on all methods per plan.md and `contracts/auth.md`
- [x] T018 [US5] Create `CMSHeadlessApi/Controllers/TokenController.cs`: `[Route("api/headless/token")]`, `[AllowAnonymous]` (with inline justification comment per plan.md), `[HttpPost]` action — validate `ModelState` → 400 on failure, call `ITokenService.IssueTokenAsync` → `null` returns `Unauthorized(ProblemDetails{detail="Invalid credentials"})`, success returns `Ok(new ApiResponse<TokenResponse>{Data=result, Meta=...})`; inject `ILogger<TokenController>` per `contracts/auth.md`

**Checkpoint**: `POST /api/headless/token` returns JWT on valid credentials; HTTP 401 on invalid; HTTP 400 on missing fields. Token validates against JWT Bearer middleware.

---

## Phase 4: User Story 1 — Retrieve Published Pages by Slug (Priority: P1) 🎯 MVP

**Goal**: Authenticated consumers can list published CMS pages paginated and retrieve a single page by URL slug with full content and metadata.

**Independent Test**: With a valid token, `GET /api/headless/pages?slug=/about-us` → HTTP 200 with `data.title`, `data.body`, `data.publishDate` populated. `GET /api/headless/pages?slug=/nonexistent` → 404. `GET /api/headless/pages` without token → 401. Draft/unpublished page slug → 404.

- [x] T019 [P] [US1] Create `CMSHeadlessApi/Models/Request/PageQueryParams.cs` with `SiteId` (Guid?), `Slug` (string?), `Page` (int=1, `[Range(1, int.MaxValue)]`), `PageSize` (int=20, `[Range(1, 100)]`) per `contracts/endpoints.md`
- [x] T020 [P] [US1] Create `CMSHeadlessApi/Models/Dto/PageSummaryDto.cs` with `RootContentId` (Guid), `SiteId` (Guid), `Slug`, `Title`, `NavTitle`, `MetaDescription`, `PublishDate` (DateTime), `Thumbnail` (string?), `ShowInSiteNav` (bool), `ShowInSiteMap` (bool) per `data-model.md`
- [x] T021 [P] [US1] Create `CMSHeadlessApi/Models/Dto/PageDto.cs` with all fields: `RootContentId`, `SiteId`, `Slug`, `Title`, `NavTitle`, `PageHeading`, `Body`, `LeftColumnBody` (string?), `RightColumnBody` (string?), `MetaKeywords`, `MetaDescription`, `PublishDate`, `RetireDate`, `CreateDate`, `EditDate`, `IsActive` (bool), `ShowInSiteNav` (bool), `ShowInSiteMap` (bool), `Thumbnail` (string?), `ContentType` (string) per `data-model.md`
- [x] T022 [US1] Create `CMSHeadlessApi/Services/ContentQueryService.cs`: implement `IContentQueryService`; inject `CarrotCakeContext`, `IMemoryCache`, `IHttpContextAccessor`, `IConfiguration`; implement `SiteExistsAsync` (query `CarrotSites` with `AsNoTracking()`, cache in `IMemoryCache` with 5-minute TTL), `GetDefaultSiteIdAsync` (return single site id if `CarrotSites.Count()==1` else `null`), `GetPagesAsync` (apply published predicate: `IsLatestVersion==true && PageActive==true && GoLiveDate < UtcNow && RetireDate > UtcNow && SiteId==siteId` + `ContentTypeId == ContentPageType.GetIDByType(ContentPageType.PageType.ContentEntry)` + pagination `Skip/Take` with `CountAsync`, all queries `AsNoTracking()`), `GetPageBySlugAsync` (same predicate + `FileName == slug` case-insensitive); add private `CheckSiteScopeAsync(Guid siteId)` helper that reads `site_scope` claim and throws `UnauthorizedAccessException` if mismatch; stub remaining 5 methods with `throw new NotImplementedException(); // TODO(TEST):` per plan.md; add `// TODO(TEST):` on all implemented methods
- [x] T023 [US1] Create `CMSHeadlessApi/Controllers/PagesController.cs`: `[Authorize]`, `[Route("api/headless/pages")]`; inject `IContentQueryService`, `ILogger<PagesController>`; single `[HttpGet]` action with `[FromQuery] PageQueryParams params` — resolve `siteId` (use `params.SiteId` or call `GetDefaultSiteIdAsync`; if multi-site and absent return 400; call `SiteExistsAsync` → false → 404); if `params.Slug` is present call `GetPageBySlugAsync` → null → 404 ProblemDetails, else return `Ok(ApiResponse<PageDto>)`; if no slug call `GetPagesAsync` return `Ok(PagedApiResponse<PageSummaryDto>)`; wrap in try/catch for `UnauthorizedAccessException` → 403; log 404 at `LogInformation`, 400 at `LogDebug` per plan.md error handling conventions and `contracts/endpoints.md`

**Checkpoint**: `GET /api/headless/pages` (list) and `GET /api/headless/pages?slug=...` (single) both functional with correct 200/400/401/404 responses. Multi-site `siteId` logic and site scope enforcement validated.

---

## Phase 5: User Story 2 — List and Filter Blog Posts (Priority: P2)

**Goal**: Authenticated consumers can list published blog posts with category, tag, date range, and site filters, and retrieve a single post by slug with categories and tags populated.

**Independent Test**: With a valid token, `GET /api/headless/posts?category=news&page=1` → HTTP 200 paginated list with `data[0].categories` populated. `GET /api/headless/posts?tag=featured&dateFrom=2025-01-01` → filtered results. `GET /api/headless/posts?slug=/blog/2025/01/hello` → full `PostDto` with `body` and `tags`. Both filters combined with AND logic returns only matching posts.

- [x] T024 [P] [US2] Create `CMSHeadlessApi/Models/Request/PostQueryParams.cs` with `SiteId` (Guid?), `Slug` (string?), `Category` (string?), `Tag` (string?), `DateFrom` (string?), `DateTo` (string?), `Page` (int=1, `[Range(1, int.MaxValue)]`), `PageSize` (int=20, `[Range(1, 100)]`) per `contracts/endpoints.md`
- [x] T025 [P] [US2] Create `CMSHeadlessApi/Models/Dto/CategoryDto.cs` with `Text` (string) and `Slug` (string) properties per `data-model.md`
- [x] T026 [P] [US2] Create `CMSHeadlessApi/Models/Dto/TagDto.cs` with `Text` (string) and `Slug` (string) properties per `data-model.md`
- [x] T027 [P] [US2] Create `CMSHeadlessApi/Models/Dto/PostSummaryDto.cs` with `RootContentId` (Guid), `SiteId` (Guid), `Slug`, `Title`, `Excerpt` (string, truncated 500 chars from body), `PublishDate` (DateTime), `Thumbnail` (string?), `Categories` (`List<CategoryDto>`), `Tags` (`List<TagDto>`) per `data-model.md`
- [x] T028 [P] [US2] Create `CMSHeadlessApi/Models/Dto/PostDto.cs` with all `PageDto` fields plus `Excerpt` (string), `Categories` (`List<CategoryDto>`), `Tags` (`List<TagDto>`) per `data-model.md`
- [x] T029 [US2] Extend `CMSHeadlessApi/Services/ContentQueryService.cs`: implement `GetPostsAsync` — base query: published predicate + `ContentTypeId == ContentPageType.GetIDByType(ContentPageType.PageType.BlogEntry)` + `AsNoTracking()`; apply `category` filter by joining `vwCarrotCategoryUrls` on `SiteId + CategoryUrl` then `CarrotCategoryContentMappings` on `RootContentId` (mirrors `CannedQueries.GetContentByCategoryURL`); apply `tag` filter by joining `vwCarrotTagUrls` + `CarrotTagContentMappings` similarly; apply `dateFrom`/`dateTo` as `.Where(ct => ct.GoLiveDate >= dateFrom && ct.GoLiveDate <= dateTo)`; paginate with `CountAsync`/`Skip`/`Take`; batch-fetch categories and tags for result page items by joining `CarrotCategoryContentMappings → CarrotContentCategories` (and tags similarly) for all `RootContentId` values, materialize to `Dictionary<Guid, List<CategoryDto>>` before projecting DTO (zero N+1); implement `GetPostBySlugAsync` with same published predicate + slug match and single-item category/tag fetch; add `// TODO(TEST):` on all new methods per plan.md data access layer spec
- [x] T030 [US2] Create `CMSHeadlessApi/Controllers/PostsController.cs`: `[Authorize]`, `[Route("api/headless/posts")]`; inject `IContentQueryService`, `ILogger<PostsController>`; single `[HttpGet]` action — resolve `siteId`; parse `dateFrom`/`dateTo` as `DateTime` (return 400 ProblemDetails for bad format, 400 if `dateTo < dateFrom` with `detail="dateTo must be on or after dateFrom"`); if `params.Slug` present call `GetPostBySlugAsync` → null → 404; else call `GetPostsAsync` → `Ok(PagedApiResponse<PostSummaryDto>)`; wrap `UnauthorizedAccessException` → 403 per `contracts/endpoints.md` and plan.md error handling conventions

**Checkpoint**: `GET /api/headless/posts` with all filter combinations (category, tag, dateFrom/dateTo, combined) returns correct independently-testable paginated results. Slug lookup returns full PostDto with categories and tags.

---

## Phase 6: User Story 3 — Fetch Navigation Trees (Priority: P3)

**Goal**: Authenticated consumers can retrieve the full hierarchical navigation tree for a site, with hidden and unpublished nodes excluded.

**Independent Test**: With a valid token, `GET /api/headless/navigation?siteId=<guid>` → HTTP 200 with a JSON array of root nodes each containing `children` arrays with correct `parentContentId`, `href`, and `navOrder`. Nodes with `ShowInSiteNav=false` are absent. Single-site CMS with no `siteId` → default site navigation returned.

- [x] T031 [P] [US3] Create `CMSHeadlessApi/Models/Request/NavigationQueryParams.cs` with `SiteId` (Guid?) per `contracts/endpoints.md`
- [x] T032 [P] [US3] Create `CMSHeadlessApi/Models/Dto/NavigationNodeDto.cs` with `RootContentId` (Guid), `ParentContentId` (Guid?), `Title` (string), `Href` (string), `NavOrder` (int), `Children` (`List<NavigationNodeDto>`) per `data-model.md`
- [x] T033 [US3] Extend `CMSHeadlessApi/Services/ContentQueryService.cs`: implement `GetNavigationAsync` — call `SiteNavHelperReal.GetMasterNavigation(siteId, bActiveOnly: true)`, filter to `ShowInSiteNav==true`, build tree in O(n) single pass: create `Dictionary<Guid, NavigationNodeDto>` keyed by `Root_ContentID`, then iterate to attach each node to its parent's `Children` list (nodes with `Parent_ContentID==null` are root nodes), return root nodes ordered by `NavOrder`; add `// TODO(TEST):` per plan.md navigation tree building spec
- [x] T034 [US3] Create `CMSHeadlessApi/Controllers/NavigationController.cs`: `[Authorize]`, `[Route("api/headless/navigation")]`; inject `IContentQueryService`, `ILogger<NavigationController>`; `[HttpGet]` action — resolve `siteId` (same multi-site/single-site logic as PagesController); call `GetNavigationAsync`; return `Ok(new ApiResponse<List<NavigationNodeDto>>{Data=result, Meta=...})`; site not found → 404; wrap `UnauthorizedAccessException` → 403 per `contracts/endpoints.md`

**Checkpoint**: `GET /api/headless/navigation` returns correct hierarchical JSON tree with parent-child nesting. Hidden nodes (`ShowInSiteNav=false`) are excluded. Default-site resolution works on single-site CMS.

---

## Phase 7: User Story 4 — Retrieve Content Snippets and Widget Zones (Priority: P4)

**Goal**: Authenticated consumers can fetch named content snippets and widget zone configurations by page slug and zone name.

**Independent Test**: `GET /api/headless/snippets?name=promo-banner` → HTTP 200 with `data.body` containing HTML. `GET /api/headless/widgetzones?pageSlug=/home&zone=sidebar` → HTTP 200 with ordered `data` array of widget instances with `controlPath` and `controlProperties`. Missing name → 400. Non-existent snippet → 404.

- [x] T035 [P] [US4] Create `CMSHeadlessApi/Models/Request/SnippetQueryParams.cs` with `SiteId` (Guid?) and `Name` (string, `[Required]`) per `contracts/endpoints.md`
- [x] T036 [P] [US4] Create `CMSHeadlessApi/Models/Request/WidgetZoneQueryParams.cs` with `SiteId` (Guid?), `PageSlug` (string, `[Required]`), `Zone` (string, `[Required]`) per `contracts/endpoints.md`
- [x] T037 [P] [US4] Create `CMSHeadlessApi/Models/Dto/SnippetDto.cs` with `RootContentSnippetId` (Guid), `SiteId` (Guid), `Name` (string), `Slug` (string), `Body` (string), `IsActive` (bool), `PublishDate` (DateTime), `RetireDate` (DateTime) per `data-model.md`
- [x] T038 [P] [US4] Create `CMSHeadlessApi/Models/Dto/WidgetInstanceDto.cs` with `RootWidgetId` (Guid), `WidgetOrder` (int), `Zone` (string), `ControlPath` (string), `ControlProperties` (string), `IsActive` (bool) per `data-model.md`
- [x] T039 [US4] Extend `CMSHeadlessApi/Services/ContentQueryService.cs`: implement `GetSnippetByNameAsync` — query `vwCarrotContentSnippets` with `AsNoTracking()`, published predicate (`IsLatestVersion==true && ContentSnippetActive==true && GoLiveDate < UtcNow && RetireDate > UtcNow`), case-insensitive match on `ContentSnippetName` OR `ContentSnippetSlug`, scoped to `SiteId`; implement `GetWidgetZoneAsync` — first resolve `RootContentId` from page slug via `vwCarrotContents` (single query, `AsNoTracking()`), then query `vwCarrotWidgets` filtered by `RootContentId`, `PlaceholderName==zone`, and active/published predicate, ordered by `WidgetOrder`; return empty list (not null) for no active widgets; add `// TODO(TEST):` on all new methods per `data-model.md` and plan.md widget zone query spec
- [x] T040 [P] [US4] Create `CMSHeadlessApi/Controllers/SnippetsController.cs`: `[Authorize]`, `[Route("api/headless/snippets")]`; inject `IContentQueryService`, `ILogger<SnippetsController>`; `[HttpGet]` — validate `ModelState` (name required → 400), resolve `siteId`, call `GetSnippetByNameAsync` → null → 404 ProblemDetails with `detail="No active snippet found with name '{name}'"`, else return `Ok(ApiResponse<SnippetDto>)`; log 404 at `LogInformation` per `contracts/endpoints.md`
- [x] T041 [P] [US4] Create `CMSHeadlessApi/Controllers/WidgetZonesController.cs`: `[Authorize]`, `[Route("api/headless/widgetzones")]`; inject `IContentQueryService`, `ILogger<WidgetZonesController>`; `[HttpGet]` — validate `ModelState` (pageSlug + zone required → 400), resolve `siteId`, call `GetWidgetZoneAsync`; empty result → 404 ProblemDetails with `detail="No active widgets found for page '{pageSlug}' zone '{zone}'"`, else return `Ok(ApiResponse<List<WidgetInstanceDto>>)`; wrap `UnauthorizedAccessException` → 403 per `contracts/endpoints.md`

**Checkpoint**: Both snippet and widget zone endpoints return correct structured JSON. 404 on missing snippets/pages/zones. 400 on missing required parameters. 401 without valid token.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Site scope enforcement, integration test artefacts, and end-to-end validation.

- [x] T042 Add site scope enforcement to `CMSHeadlessApi/Services/ContentQueryService.cs`: update private `CheckSiteScopeAsync(Guid siteId)` helper to read `site_scope` claim from `IHttpContextAccessor.HttpContext.User`; if claim value is not `"*"` and does not equal `siteId.ToString()`, throw `UnauthorizedAccessException($"Token is not authorized for site {siteId}")` per plan.md Site Scope Enforcement spec; ensure all `GetPagesAsync`, `GetPageBySlugAsync`, `GetPostsAsync`, `GetPostBySlugAsync`, `GetNavigationAsync`, `GetSnippetByNameAsync`, `GetWidgetZoneAsync` call this helper before any DB query
- [x] T043 [P] Create `specs/002-headless-rest-api/test-scripts/headless-api.http` with 27 named VS Code REST Client scenarios: token (valid credentials, invalid credentials, missing field), pages (list, pagination, by slug, 404 slug, 401 no-token), posts (list, category filter, tag filter, date range, combined filters, out-of-range page, invalid date, by slug), navigation (tree, invalid siteId format, missing siteId on multi-site), snippets (by name, 404, missing name param), widget zones (by pageSlug+zone, 404 page, missing params), health check; variable block at top for `@baseUrl`, `@clientId`, `@clientSecret`, `@siteId`; capture token via `@token = {{tokenRequest.response.body.$.data.token}}` per plan.md test script spec
- [x] T044 [P] Create `specs/002-headless-rest-api/test-scripts/test-api.sh` with bash curl smoke tests: environment variable driven (`BASE_URL`, `CLIENT_ID`, `CLIENT_SECRET`, `SITE_ID`); requires `curl` and `jq`; covers token request → capture JWT → use token on all content endpoints; prints `PASS`/`FAIL` per test with HTTP status comparison; exits non-zero on any failure per plan.md test script spec
- [x] T045 Provision seed `CarrotApiClient` row in the database: insert with `ClientId="dev-client"`, `ClientSecretHash` from `PasswordHasher<CarrotApiClient>.HashPassword(null, "dev-secret")`, `ScopeToSiteId=null`, `IsActive=1`, `CreatedDateUtc=GETUTCDATE()`; then run `test-api.sh` end-to-end and verify all expected PASS results per `quickstart.md`

**Checkpoint**: All 27 HTTP scenarios in `headless-api.http` execute without uncaught exceptions. `test-api.sh` exits 0. Site scope claim is enforced on all content endpoints. `dotnet build` reports zero errors.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Requires Phase 1 completion — **BLOCKS all user stories**
- **US5 Token Auth (Phase 3)**: Requires Phase 2 — enables auth pipeline for all content stories
- **US1 Pages (Phase 4)**: Requires Phase 2 + Phase 3 (needs working auth to test end-to-end)
- **US2 Posts (Phase 5)**: Requires Phase 2 — independent of US1 (shares ContentQueryService but new methods)
- **US3 Navigation (Phase 6)**: Requires Phase 2 — independent of US1/US2
- **US4 Snippets + Widgets (Phase 7)**: Requires Phase 2 — independent of US1/US2/US3
- **Polish (Phase 8)**: Requires all user story phases complete

### User Story Dependencies

| Story | Depends On | Parallel With (after Phase 2) |
|-------|------------|-------------------------------|
| US5 — Token (P1) | Phase 2 | Can start immediately after Phase 2 |
| US1 — Pages (P1) | Phase 2, US5 for end-to-end test | US2, US3, US4 (different files) |
| US2 — Posts (P2) | Phase 2 | US1, US3, US4 (different files) |
| US3 — Navigation (P3) | Phase 2 | US1, US2, US4 (different files) |
| US4 — Snippets + Widgets (P4) | Phase 2 | US1, US2, US3 (different files) |

> **Note**: US1–US4 all add new methods to `ContentQueryService.cs`. If working in parallel, coordinate on that file to avoid merge conflicts (implement each story's methods in a separate section of the class).

### Within Each User Story

- Request model and DTO files marked `[P]` can be created simultaneously (different files)
- Services before controllers
- Stub remaining interface methods with `throw new NotImplementedException(); // TODO(TEST):` to keep the build green

---

## Parallel Execution Examples

### Phase 1 (Setup) — 3 tasks in parallel after T002

```
T001 → T002 → T003 [P] + T004 [P] + T005 [P]
```

### Phase 2 (Foundational) — after T002 completes

```
T006 → T007 → T008                          (serial: entity → context → migration)
T009 [P] + T010 [P]                         (response envelopes in parallel)
T011                                         (service interface)
T012 → T013                                  (Program.cs skeleton then JWT config)
```

### Phase 3 (US5 Token Auth) — T014 and T015 in parallel

```
T014 [P] + T015 [P]    (TokenRequest + TokenResponse simultaneously)
T016 → T017 → T018     (interface → service → controller)
```

### Phase 4 (US1 Pages) — T019, T020, T021 in parallel

```
T019 [P] + T020 [P] + T021 [P]    (PageQueryParams + PageSummaryDto + PageDto simultaneously)
T022 → T023                         (ContentQueryService then PagesController)
```

### Phase 5 (US2 Posts) — T024–T028 in parallel

```
T024 [P] + T025 [P] + T026 [P] + T027 [P] + T028 [P]    (all models simultaneously)
T029 → T030                                                 (extend service then controller)
```

### Phase 6 (US3 Navigation) — T031 and T032 in parallel

```
T031 [P] + T032 [P]    (NavigationQueryParams + NavigationNodeDto simultaneously)
T033 → T034             (extend service then controller)
```

### Phase 7 (US4 Snippets + Widgets) — T035–T038 in parallel

```
T035 [P] + T036 [P] + T037 [P] + T038 [P]    (all 4 models simultaneously)
T039                                            (extend ContentQueryService with both methods)
T040 [P] + T041 [P]                            (SnippetsController + WidgetZonesController simultaneously)
```

---

## Implementation Strategy

### MVP First (US5 + US1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: US5 Token Auth
4. Complete Phase 4: US1 Pages
5. **STOP and VALIDATE**: Token → page retrieval end-to-end working
6. Deploy/demo if ready

### Incremental Delivery

| Step | Deliverable | Stories Covered |
|------|-------------|-----------------|
| Phase 1 + 2 | Skeleton project: builds, migrates, health check `/health` live | — |
| + Phase 3 | `/api/headless/token` live with credential exchange | US5 ✅ |
| + Phase 4 | `/api/headless/pages` live — **MVP! SPA can render CMS pages** | US1 ✅ |
| + Phase 5 | `/api/headless/posts` live — pagination + category/tag/date filters | US2 ✅ |
| + Phase 6 | `/api/headless/navigation` live — menus and breadcrumbs | US3 ✅ |
| + Phase 7 | `/api/headless/snippets` + `/api/headless/widgetzones` live | US4 ✅ |
| + Phase 8 | Site scope enforcement on all endpoints; test scripts complete | All ✅ |

### Parallel Team Strategy

With 2+ developers after Phase 2 is complete:

- **Developer A**: Phase 3 (US5) → Phase 4 (US1) → assist Phase 8
- **Developer B**: Phase 5 (US2) in parallel with Developer A doing Phase 3/4
- **Developer C**: Phase 6 (US3) + Phase 7 (US4) in parallel

Coordinate on `ContentQueryService.cs` — each developer works on a separate method group.

---

## Summary

| Phase | Tasks | Story | Parallel Opportunities |
|-------|-------|-------|----------------------|
| 1 — Setup | T001–T005 | — | T003, T004, T005 |
| 2 — Foundational | T006–T013 | — | T009, T010 in parallel; T012→T013 serial |
| 3 — US5 Token Auth | T014–T018 | US5 (P1) | T014, T015 |
| 4 — US1 Pages | T019–T023 | US1 (P1) | T019, T020, T021 |
| 5 — US2 Posts | T024–T030 | US2 (P2) | T024, T025, T026, T027, T028 |
| 6 — US3 Navigation | T031–T034 | US3 (P3) | T031, T032 |
| 7 — US4 Snippets + Widgets | T035–T041 | US4 (P4) | T035, T036, T037, T038; T040, T041 |
| 8 — Polish | T042–T045 | — | T043, T044 |
| **Total** | **45 tasks** | | |

**Suggested MVP scope**: Phases 1–4 (T001–T023, 23 tasks) — delivers token auth + page retrieval, which satisfies US5 and US1 and provides a working headless backend for a SPA.
