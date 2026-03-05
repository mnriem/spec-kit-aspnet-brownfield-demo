# Data Model: Headless REST API

**Feature**: 002-headless-rest-api  
**Date**: 2026-03-05

---

## New Entity: CarrotApiClient

Stored in the `CarrotCMSData` project. Requires an EF Core migration.

```csharp
// CarrotCMSData/Models/CarrotApiClient.cs
namespace Carrotware.CMS.Data.Models {
    public partial class CarrotApiClient {
        public Guid   ApiClientId       { get; set; }   // PK, newsequentialid()
        public string ClientId          { get; set; }   // UNIQUE, human-readable (e.g. "my-spa")
        public string ClientSecretHash  { get; set; }   // PasswordHasher<T> v3 PBKDF2-SHA256
        public Guid?  ScopeToSiteId     { get; set; }   // NULL = all sites; non-null = scoped to one site
        public bool   IsActive          { get; set; }   // soft-disable without deleting
        public DateTime CreatedDateUtc  { get; set; }
        public DateTime? ExpiresDateUtc { get; set; }   // NULL = never expires
        public string? Description      { get; set; }

        public virtual CarrotSite? ScopeToSite  { get; set; }
    }
}
```

### SQL Column Mapping

| Column              | Type               | Constraints                  |
|---------------------|--------------------|------------------------------|
| `ApiClientId`       | `UNIQUEIDENTIFIER` | PK, default `NEWSEQUENTIALID()` |
| `ClientId`          | `NVARCHAR(128)`    | UNIQUE NOT NULL               |
| `ClientSecretHash`  | `NVARCHAR(MAX)`    | NOT NULL                     |
| `ScopeToSiteId`     | `UNIQUEIDENTIFIER` | NULL, FK → `carrot_Sites.SiteID` |
| `IsActive`          | `BIT`              | NOT NULL, default 1          |
| `CreatedDateUtc`    | `DATETIME2`        | NOT NULL                     |
| `ExpiresDateUtc`    | `DATETIME2`        | NULL                         |
| `Description`       | `NVARCHAR(256)`    | NULL                         |

### Validation Rules

- `ClientId` must be 3–128 characters, alphanumeric plus hyphen/underscore.
- `ClientSecretHash` must never be stored in plaintext; always hashed with `PasswordHasher<CarrotApiClient>`.
- `ExpiresDateUtc` if set must be in the future at creation time.
- Deleting a `CarrotApiClient` does not cascade to any content.

### Migration

```bash
dotnet ef migrations add AddCarrotApiClient \
  --context CarrotCakeContext \
  --output-dir Migrations \
  --project CarrotCMSData \
  --startup-project CMSHeadlessApi
```

---

## Existing Entities — API Projection

These entities are read-only from the API's perspective. No migrations are needed.

### Published Content Predicate (all content queries)

```csharp
ct.IsLatestVersion == true
&& ct.PageActive == true
&& ct.GoLiveDate < DateTime.UtcNow
&& ct.RetireDate > DateTime.UtcNow
&& ct.SiteId == siteId
```

Source: `CannedQueries.GetAllByTypeList`, `GetLatestContentList`, `GetLatestBlogList`.

### vwCarrotContent → PageDto / PostDto

| API Field          | DB Column                  | Notes                              |
|--------------------|----------------------------|------------------------------------|
| `rootContentId`    | `RootContentId`            | Stable content identifier          |
| `siteId`           | `SiteId`                   |                                    |
| `slug`             | `FileName`                 | URL path e.g. `/about-us`          |
| `title`            | `TitleBar`                 |                                    |
| `navTitle`         | `NavMenuText`              | Short navigation label             |
| `pageHeading`      | `PageHead`                 | H1 heading                         |
| `body`             | `PageText`                 | Rich-text HTML body                |
| `leftColumnBody`   | `LeftPageText`             | Optional left-column HTML          |
| `rightColumnBody`  | `RightPageText`            | Optional right-column HTML         |
| `metaKeywords`     | `MetaKeyword`              |                                    |
| `metaDescription`  | `MetaDescription`          |                                    |
| `publishDate`      | `GoLiveDate` (site-local)  | Converted from UTC by site TZ      |
| `retireDate`       | `RetireDate` (site-local)  |                                    |
| `createDate`       | `CreateDate` (site-local)  |                                    |
| `editDate`         | `EditDate` (site-local)    |                                    |
| `isActive`         | `PageActive`               |                                    |
| `showInSiteNav`    | `ShowInSiteNav`            |                                    |
| `showInSiteMap`    | `ShowInSiteMap`            |                                    |
| `contentType`      | `ContentTypeValue`         | `"BlogEntry"` or `"ContentEntry"`  |
| `thumbnail`        | `PageThumbnail`            | Relative image path, may be null   |

**PostDto extensions** (blog entries only):

| API Field     | Source                             | Notes                               |
|---------------|------------------------------------|-------------------------------------|
| `categories`  | `CarrotCategoryContentMappings`    | Joined via `RootContentId`          |
| `tags`        | `CarrotTagContentMappings`         | Joined via `RootContentId`          |
| `excerpt`     | Truncated `PageText` (500 chars)   | Derived field, not stored           |

### vwCarrotContentSnippet → SnippetDto

| API Field              | DB Column               |
|------------------------|-------------------------|
| `rootContentSnippetId` | `RootContentSnippetId`  |
| `siteId`               | `SiteId`                |
| `name`                 | `ContentSnippetName`    |
| `slug`                 | `ContentSnippetSlug`    |
| `body`                 | `ContentBody`           |
| `isActive`             | `ContentSnippetActive`  |
| `publishDate`          | `GoLiveDate`            |
| `retireDate`           | `RetireDate`            |

Lookup: query by `ContentSnippetSlug` (case-insensitive) OR `ContentSnippetName` (case-insensitive).  
Published predicate: `IsLatestVersion=true`, `ContentSnippetActive=true`, `GoLiveDate < Now`, `RetireDate > Now`.

### vwCarrotWidget → WidgetInstanceDto

| API Field          | DB Column           | Notes                                      |
|--------------------|---------------------|--------------------------------------------|
| `rootWidgetId`     | `RootWidgetId`      |                                            |
| `widgetOrder`      | `WidgetOrder`       | Render order within the zone               |
| `zone`             | `PlaceholderName`   | Zone name e.g. `"sidebar"`, `"footer"`    |
| `controlPath`      | `ControlPath`       | Widget type identifier / assembly path     |
| `controlProperties`| `ControlProperties` | Serialised widget configuration (opaque)   |
| `isActive`         | `WidgetActive`      |                                            |

Published predicate: `IsLatestVersion=true`, `WidgetActive=true`, `GoLiveDate < Now`, `RetireDate > Now`.  
Filter: `SiteId` (via `CarrotRootContents` join), `RootContentId` (from page slug lookup), `PlaceholderName` (zone).

### SiteNav → NavigationNodeDto

| API Field          | Source                       | Notes                                      |
|--------------------|------------------------------|--------------------------------------------|
| `rootContentId`    | `Root_ContentID`             |                                            |
| `parentContentId`  | `Parent_ContentID`           | `null` = top-level node                   |
| `title`            | `NavMenuText`                | Navigation display label                   |
| `href`             | `FileName`                   | URL path                                   |
| `navOrder`         | `NavOrder`                   | Sort order within parent                   |
| `isActive`         | `PageActive`                 |                                            |
| `children`         | tree-built from flat list    | Recursive `NavigationNodeDto[]`            |

Source: `SiteNavHelperReal.GetMasterNavigation(siteId, bActiveOnly: true)`, filtered to `ShowInSiteNav=true`.

### JWT Payload (ApiToken)

| Claim           | Value                                   |
|-----------------|-----------------------------------------|
| `sub`           | `ClientId` string                       |
| `jti`           | `Guid.NewGuid()` (unique token ID)      |
| `iat`           | Unix timestamp of issuance              |
| `exp`           | Unix timestamp of expiry                |
| `iss`           | `"CarrotCakeCMS"`                       |
| `aud`           | `"CarrotCakeHeadlessConsumers"`         |
| `site_scope`    | `ScopeToSiteId.ToString()` or `"*"`    |

---

## Entity Relationships (new)

```
CarrotApiClient ----[0..1]----> CarrotSite   (ScopeToSiteId FK, nullable)
```

No changes to any existing relationships.

---

## State Transitions

### CarrotApiClient
- **Active** (`IsActive=true`, `ExpiresDateUtc=null` or in future): credentials accepted  
- **Expired** (`ExpiresDateUtc` in the past): token issuance rejected with HTTP 401  
- **Disabled** (`IsActive=false`): token issuance rejected with HTTP 401  

Transitions are manual (admin DB update). No automated rotation in this scope.

---

## No New Migrations for Content Tables

All content entity changes are read-only projections. No columns are added to existing tables. The only schema change is the `CarrotApiClient` table.
