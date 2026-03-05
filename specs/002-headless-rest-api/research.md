# Phase 0 Research: Headless REST API

**Feature**: 002-headless-rest-api  
**Date**: 2026-03-05  
**Status**: Complete — all unknowns resolved

---

## R-001: Project Hosting

**Decision**: Add a new dedicated `CMSHeadlessApi` ASP.NET Core Web API project at the repository root.

**Rationale**: The headless API needs stateless JWT Bearer authentication, which cannot coexist cleanly in `CMSAdmin` without polluting its cookie-based auth pipeline. `CMSAdmin` uses `ConfigureApplicationCookie` + `CmsAuthorize`; adding `AddAuthentication().AddJwtBearer()` alongside those schemes in the same host introduces authentication scheme ambiguity on shared routes. A separate project ensures independent deployability, isolated JWT configuration, and no regression risk in the existing admin auth flow.

**Alternatives considered**:  
- *Area within CMSAdmin*: Rejected — forces dual auth-scheme resolution on every request and couples the headless API's lifecycle to admin deployments.  
- *Minimal API endpoints in CMSAdmin*: Rejected — same dual-scheme problem; also pollutes `Program.cs` startup.

---

## R-002: Token Mechanism

**Decision**: JWT Bearer tokens issued by the API itself, signed with HMAC-SHA256 (`HmacSha256`). `System.IdentityModel.Tokens.Jwt` v8.1.2 (already present in `CMSAdmin.csproj`) is the chosen library, pinned to the same version in the new project.

**Rationale**: JWTs are self-contained, stateless, and standard. The library is already a declared dependency of the repository, ensuring version consistency. HMAC-SHA256 with a ≥256-bit key from environment variables satisfies the security bar for trusted internal/partner consumers. No token database or revocation store is required for the initial scope.

**Alternatives considered**:  
- *OAuth2 / OpenID Connect via IdentityServer or Duende*: Rejected for scope — over-engineered for a single tenant/partner consumer model; adds significant operational complexity.  
- *API key in header*: Rejected — lacks built-in expiry propagation; tokens carry expiry in the payload which simplifies consumer logic.  
- *RSA-signed JWTs*: Rejected for initial scope — symmetric signing is sufficient for a single-issuer, single-audience scenario and avoids key distribution complexity.

---

## R-003: Client Credential Storage

**Decision**: New EF Core entity `CarrotApiClient` in `CarrotCMSData`. The `ClientSecret` is stored as a hash produced by `Microsoft.AspNetCore.Identity.PasswordHasher<CarrotApiClient>` (Identity v3 format, PBKDF2-SHA256, 10k iterations). The `ClientId` is a human-readable string (e.g., `my-spa-client`).

**Rationale**: `PasswordHasher<T>` is already available via the `Microsoft.AspNetCore.Identity.EntityFrameworkCore` package (transitively present in the repo). It produces a well-known, vetted PBKDF2 hash without adding new cryptographic dependencies. The admin can create clients via a future admin UI or directly in the DB; initial provisioning is out of scope.

**Alternatives considered**:  
- *Pre-shared static key in appsettings*: Rejected — cannot revoke individual clients, cannot scope to a site.  
- *BouncyCastle BCrypt*: Viable but more machinery than necessary; `PasswordHasher<T>` is purpose-built for this exact use case and already available.

---

## R-004: Published Content Predicate

**Decision**: Use the four-condition published gate from `CannedQueries.cs`:  
```csharp
ct.IsLatestVersion == true
&& ct.PageActive == true
&& ct.GoLiveDate < DateTime.UtcNow
&& ct.RetireDate > DateTime.UtcNow
```
Pages (`ContentEntry`) and blog posts (`BlogEntry`) are distinguished by `ct.ContentTypeId == ContentPageType.GetIDByType(pageType)`.

**Rationale**: This is the exact predicate used by all existing content-serving paths in `CannedQueries.GetAllByTypeList`, `GetLatestContentList`, and `GetLatestBlogList`. Re-using it ensures the API exposes exactly the same content visibility rules as the front-end, satisfying FR-006 and FR-011.

**Alternatives considered**: N/A — existing predicate is authoritative. No new status model is introduced per the spec assumptions.

---

## R-005: Navigation Tree Building

**Decision**: Fetch the flat `List<SiteNav>` via `SiteNavHelperReal.GetMasterNavigation(siteId, bActiveOnly: true)`, then build a tree in the service layer using `Parent_ContentID` grouping. Only nodes where `ShowInSiteNav == true` are included (matching FR-013). Root nodes have `Parent_ContentID == null`.

**Rationale**: `SiteNavHelperReal` already encapsulates the correct query with `AsNoTracking()` and active-only filtering. Reusing it avoids duplicating the query predicate. Tree-building from a flat list is O(n) with a dictionary lookup and is straightforward.

**Alternatives considered**:  
- *Recursive CTE in SQL*: More efficient for very deep trees but adds a raw SQL dependency contradicting the constitution's EF-only rule for non-schema queries.  
- *`GetLevelDepthNavigation`*: Fetches only N levels; rejected because navigation consumers need the full tree.

---

## R-006: Widget Zone Data Shape

**Decision**: Use `vwCarrotWidgets` filtered by `RootContentId` (from looking up the page by slug) and `PlaceholderName` (zone name), `IsLatestVersion == true`, active and within GoLiveDate/RetireDate. Return `ControlPath`, `WidgetOrder`, `PlaceholderName`, and `ControlProperties` (the serialised widget configuration XML/JSON) per widget instance.

**Rationale**: `vwCarrotWidget` joins `CarrotWidget` + `CarrotWidgetData` (latest version). `PlaceholderName` is the zone name string. `ControlProperties` is the widget's serialised configuration — the consumer receives it as an opaque string (or deserialised JSON if it parses as JSON), matching the spec assumption that "the consumer is responsible for rendering widgets."

**Alternatives considered**: Exposing only active widgets (GoLiveDate/RetireDate gate applied). No pre-rendering of widget content.

---

## R-007: Secret Key Management

**Decision**: JWT signing key is loaded from environment variable `CARROT_HEADLESS_JWT_KEY` (Base64-encoded 256-bit key). Fallback to `appsettings.json` `HeadlessApi:JwtKey` for local development. Production deployments MUST set the environment variable; `appsettings.json` MUST contain only a placeholder. Key MUST NOT be committed to source control.

**Rationale**: Constitution Principle IV: "Sensitive configuration ... MUST remain in `appsettings.json` / environment variables and MUST NOT appear in source control." The environment variable pattern is consistent with Docker Compose `SA_PASSWORD` precedent from feature 001.

---

## R-008: Response Envelope

**Decision**: All successful responses use a typed `ApiResponse<T>` envelope:  
```json
{
  "data": { ... },
  "meta": { "requestId": "...", "timestamp": "..." }
}
```
Paginated responses use `PagedApiResponse<T>`:  
```json
{
  "data": [ ... ],
  "meta": { "page": 1, "pageSize": 20, "total": 142, "totalPages": 8, "requestId": "...", "timestamp": "..." }
}
```
Error responses use ASP.NET Core `ProblemDetails` (RFC 7807), extended with an `errors` dictionary for field-level validation errors.

**Rationale**: FR-021 mandates consistent envelope. `ProblemDetails` is the ASP.NET Core standard and is already handled by `AddProblemDetails()`. Keeping success envelopes simple reduces consumer coupling.

---

## R-009: Pagination Strategy

**Decision**: Query-string parameters `page` (1-based, default 1) and `pageSize` (default 20, max 100, min 1). Response includes `total` (total matching items before pagination), `totalPages`, `page`, `pageSize`. An out-of-range `page` returns an empty `data` array with HTTP 200 and accurate `total`.

**Rationale**: Offset-based pagination is consistent with `IPagedContent` pattern used in CMSComponents. Cursor-based pagination is unnecessary complexity for typical CMS content volumes.

---

## R-010: Category and Tag Filter Implementation

**Decision**: Category filter uses slug (`category` query param) → join `vwCarrotCategoryUrls` → `CarrotCategoryContentMappings` → `vwCarrotContents` (mirrors `CannedQueries.GetContentByCategoryURL`). Tag filter uses slug (`tag` query param) → join `vwCarrotTagUrls` → `CarrotTagContentMappings` → `vwCarrotContents` (mirrors `CannedQueries.GetContentByTagURL`). Both filters applied simultaneously with AND logic per spec edge cases.

**Rationale**: Existing `CannedQueries` methods encode the correct join pattern. Reuse prevents duplicating join logic.

---

## R-011: Test Script Tooling

**Decision**: Provide two test artefacts: (1) a VS Code REST Client `.http` file (`test-scripts/headless-api.http`) and (2) a Bash curl script (`test-scripts/test-api.sh`). Both cover all seven scenarios: token issuance, invalid credentials, page retrieval by slug, post listing with filters, navigation tree, snippet, and widget zone.

**Rationale**: VS Code REST Client (`.http`) integrates directly into the editor and requires no external tool. The curl script serves CI/CD and non-VS-Code developers. Both artefacts use a shared `BASE_URL` variable so they work against any environment.
