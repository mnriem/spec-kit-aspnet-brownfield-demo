# Contract: API Endpoints

**Feature**: 002-headless-rest-api  
**Date**: 2026-03-05  
**Base path**: `/api/headless`

All endpoints except `POST /api/headless/token` require `Authorization: Bearer <token>`.  
All responses use `Content-Type: application/json`.

---

## Response Envelope Types

### ApiResponse\<T\> (single item)
```json
{
  "data": { ... },
  "meta": {
    "requestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "timestamp": "2026-03-05T14:00:00Z"
  }
}
```

### PagedApiResponse\<T\> (collection)
```json
{
  "data": [ ... ],
  "meta": {
    "page": 1,
    "pageSize": 20,
    "total": 142,
    "totalPages": 8,
    "requestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "timestamp": "2026-03-05T14:00:00Z"
  }
}
```

### ProblemDetails (error)
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Not Found",
  "status": 404,
  "detail": "No published page found with slug '/about-us'",
  "instance": "/api/headless/pages?slug=%2Fabout-us",
  "errors": { "slug": ["Slug '/about-us' does not match any published page"] }
}
```

---

## Token Endpoint

### POST /api/headless/token

See [auth.md](auth.md) for full contract.

---

## Pages

### GET /api/headless/pages

List published CMS pages (ContentEntry type).

**Query Parameters**

| Param      | Type     | Required | Default | Constraints       | Description                    |
|------------|----------|----------|---------|-------------------|--------------------------------|
| `siteId`   | `Guid`   | †        | —       | Valid site GUID   | Filter to specific site        |
| `page`     | `int`    | no       | 1       | ≥ 1               | Page number (1-based)          |
| `pageSize` | `int`    | no       | 20      | 1–100             | Items per page                 |

† Required when the CMS has multiple sites; optional for single-site installs.

**Success Response — HTTP 200 — PagedApiResponse\<PageSummaryDto\>**

```json
{
  "data": [
    {
      "rootContentId": "a1b2c3d4-...",
      "siteId": "e5f6a7b8-...",
      "slug": "/about-us",
      "title": "About Us",
      "navTitle": "About",
      "metaDescription": "Learn more about Carrot Cake CMS.",
      "publishDate": "2025-01-15T08:00:00",
      "thumbnail": "/images/about-thumb.jpg",
      "showInSiteNav": true,
      "showInSiteMap": true
    }
  ],
  "meta": { "page": 1, "pageSize": 20, "total": 5, "totalPages": 1, ... }
}
```

**Error Responses**

| Status | Condition |
|--------|-----------|
| 400    | Invalid `siteId` format, `page` < 1, `pageSize` out of range |
| 400    | Multiple sites exist and `siteId` is absent |
| 401    | Missing or invalid bearer token |
| 404    | `siteId` does not match any known site |

---

### GET /api/headless/pages?slug={slug}

Retrieve a single published page by URL slug.

**Query Parameters**

| Param    | Type     | Required | Description                             |
|----------|----------|----------|-----------------------------------------|
| `slug`   | `string` | yes      | URL path, e.g. `/about-us`             |
| `siteId` | `Guid`   | †        | Required if multiple sites              |

**Success Response — HTTP 200 — ApiResponse\<PageDto\>**

```json
{
  "data": {
    "rootContentId": "a1b2c3d4-...",
    "siteId": "e5f6a7b8-...",
    "slug": "/about-us",
    "title": "About Us",
    "navTitle": "About",
    "pageHeading": "Welcome to Carrot Cake CMS",
    "body": "<p>Full HTML body...</p>",
    "leftColumnBody": null,
    "rightColumnBody": null,
    "metaKeywords": "cms, carrot",
    "metaDescription": "Learn more.",
    "publishDate": "2025-01-15T08:00:00",
    "retireDate": "2227-01-01T00:00:00",
    "createDate": "2025-01-10T09:30:00",
    "editDate": "2025-06-01T11:00:00",
    "isActive": true,
    "showInSiteNav": true,
    "showInSiteMap": true,
    "thumbnail": null,
    "contentType": "ContentEntry"
  },
  "meta": { ... }
}
```

**Error Responses**

| Status | Condition |
|--------|-----------|
| 400    | `slug` missing |
| 400    | Slug exists in multiple sites and `siteId` not provided |
| 401    | Invalid token |
| 404    | No published page with that slug |

---

## Posts

### GET /api/headless/posts

List published blog posts (BlogEntry type) with optional filtering.

**Query Parameters**

| Param      | Type       | Required | Default | Constraints       | Description                              |
|------------|------------|----------|---------|-------------------|------------------------------------------|
| `siteId`   | `Guid`     | †        | —       | Valid site GUID   | Filter to specific site                  |
| `category` | `string`   | no       | —       | Category slug     | Filter by category slug                  |
| `tag`      | `string`   | no       | —       | Tag slug          | Filter by tag slug                       |
| `dateFrom` | `string`   | no       | —       | ISO 8601 date     | Publish date ≥ dateFrom (site-local)     |
| `dateTo`   | `string`   | no       | —       | ISO 8601 date     | Publish date ≤ dateTo (site-local)       |
| `page`     | `int`      | no       | 1       | ≥ 1               | Page number (1-based)                    |
| `pageSize` | `int`      | no       | 20      | 1–100             | Items per page                           |

Multiple filters are combined with AND logic.

**Success Response — HTTP 200 — PagedApiResponse\<PostSummaryDto\>**

```json
{
  "data": [
    {
      "rootContentId": "b2c3d4e5-...",
      "siteId": "e5f6a7b8-...",
      "slug": "/blog/2025/01/hello-world",
      "title": "Hello World",
      "excerpt": "This is a summary of the post content...",
      "publishDate": "2025-01-20T10:00:00",
      "thumbnail": null,
      "categories": [{ "text": "News", "slug": "/blog/category/news" }],
      "tags": [{ "text": "featured", "slug": "/blog/tag/featured" }]
    }
  ],
  "meta": { "page": 1, "pageSize": 20, "total": 42, "totalPages": 3, ... }
}
```

**Error Responses**

| Status | Condition |
|--------|-----------|
| 400    | Invalid date format for `dateFrom`/`dateTo` |
| 400    | `dateTo` before `dateFrom` |
| 400    | Multiple sites, `siteId` absent |
| 401    | Invalid token |
| 404    | `siteId` does not exist |

---

### GET /api/headless/posts?slug={slug}

Retrieve a single published blog post by URL slug.

**Query Parameters**

| Param    | Type     | Required | Description                  |
|----------|----------|----------|------------------------------|
| `slug`   | `string` | yes      | URL path of the blog post    |
| `siteId` | `Guid`   | †        | Required if multiple sites   |

**Success Response — HTTP 200 — ApiResponse\<PostDto\>**

```json
{
  "data": {
    "rootContentId": "b2c3d4e5-...",
    "siteId": "e5f6a7b8-...",
    "slug": "/blog/2025/01/hello-world",
    "title": "Hello World",
    "navTitle": "Hello World",
    "pageHeading": "Hello, World!",
    "body": "<p>Full HTML post body...</p>",
    "excerpt": "This is a summary...",
    "metaKeywords": "hello, world",
    "metaDescription": "A brief welcome post.",
    "publishDate": "2025-01-20T10:00:00",
    "retireDate": "2227-01-01T00:00:00",
    "createDate": "2025-01-19T09:00:00",
    "editDate": "2025-01-20T08:00:00",
    "isActive": true,
    "thumbnail": null,
    "contentType": "BlogEntry",
    "categories": [{ "text": "News", "slug": "/blog/category/news" }],
    "tags": [{ "text": "featured", "slug": "/blog/tag/featured" }]
  },
  "meta": { ... }
}
```

---

## Navigation

### GET /api/headless/navigation

Return the full navigation tree for a site.

**Query Parameters**

| Param    | Type   | Required | Description                                          |
|----------|--------|----------|------------------------------------------------------|
| `siteId` | `Guid` | †        | Site to fetch navigation for; optional on single-site |

**Success Response — HTTP 200 — ApiResponse\<NavigationNodeDto[]\>**

```json
{
  "data": [
    {
      "rootContentId": "c3d4e5f6-...",
      "parentContentId": null,
      "title": "Home",
      "href": "/",
      "navOrder": 0,
      "children": [
        {
          "rootContentId": "d4e5f6a7-...",
          "parentContentId": "c3d4e5f6-...",
          "title": "About",
          "href": "/about-us",
          "navOrder": 1,
          "children": []
        }
      ]
    },
    {
      "rootContentId": "e5f6a7b8-...",
      "parentContentId": null,
      "title": "Blog",
      "href": "/blog",
      "navOrder": 2,
      "children": []
    }
  ],
  "meta": { ... }
}
```

**Error Responses**

| Status | Condition |
|--------|-----------|
| 400    | Multiple sites, `siteId` absent |
| 401    | Invalid token |
| 404    | `siteId` does not exist |

**Notes**: Only nodes where `ShowInSiteNav=true`, `PageActive=true`, within `GoLiveDate`/`RetireDate` are included. Hidden nodes are excluded per FR-013.

---

## Snippets

### GET /api/headless/snippets

Retrieve a published content snippet by name or slug.

**Query Parameters**

| Param    | Type     | Required | Description                                         |
|----------|----------|----------|-----------------------------------------------------|
| `name`   | `string` | yes      | Snippet name OR slug (case-insensitive match)       |
| `siteId` | `Guid`   | †        | Required if multiple sites                          |

**Success Response — HTTP 200 — ApiResponse\<SnippetDto\>**

```json
{
  "data": {
    "rootContentSnippetId": "f6a7b8c9-...",
    "siteId": "e5f6a7b8-...",
    "name": "promo-banner",
    "slug": "promo-banner",
    "body": "<div class=\"promo\">Special offer!</div>",
    "isActive": true,
    "publishDate": "2025-03-01T00:00:00",
    "retireDate": "2027-01-01T00:00:00"
  },
  "meta": { ... }
}
```

**Error Responses**

| Status | Condition |
|--------|-----------|
| 400    | `name` missing or empty |
| 400    | Multiple sites, `siteId` absent |
| 401    | Invalid token |
| 404    | No active snippet with that name/slug |

---

## Widget Zones

### GET /api/headless/widgetzones

Retrieve the ordered list of widget instances in a named zone on a specific page.

**Query Parameters**

| Param      | Type     | Required | Description                                      |
|------------|----------|----------|--------------------------------------------------|
| `pageSlug` | `string` | yes      | URL slug of the page containing the zone         |
| `zone`     | `string` | yes      | Zone name (PlaceholderName), e.g. `"sidebar"`   |
| `siteId`   | `Guid`   | †        | Required if multiple sites                       |

**Success Response — HTTP 200 — ApiResponse\<WidgetInstanceDto[]\>**

```json
{
  "data": [
    {
      "rootWidgetId": "a1b2c3d4-...",
      "widgetOrder": 1,
      "zone": "sidebar",
      "controlPath": "Carrotware.CMS.UI.Components.SimpleList, CMSComponents",
      "controlProperties": "<props><title>Recent Posts</title><itemCount>5</itemCount></props>",
      "isActive": true
    },
    {
      "rootWidgetId": "b2c3d4e5-...",
      "widgetOrder": 2,
      "zone": "sidebar",
      "controlPath": "Carrotware.CMS.UI.Components.PostCalendar, CMSComponents",
      "controlProperties": "<props><showDates>true</showDates></props>",
      "isActive": true
    }
  ],
  "meta": { ... }
}
```

**Error Responses**

| Status | Condition |
|--------|-----------|
| 400    | `pageSlug` or `zone` missing |
| 400    | Multiple sites, `siteId` absent |
| 401    | Invalid token |
| 404    | Page slug not found, or zone has no active widgets |
| 404    | `siteId` does not exist |

---

## Common Error Codes Summary

| HTTP Status | Used For |
|-------------|----------|
| 200         | Success (data returned) |
| 400         | Bad request — missing/invalid parameters |
| 401         | Missing, expired, or invalid bearer token |
| 403         | Token site scope mismatch |
| 404         | Requested resource not found |
| 500         | Internal server error (details logged, not exposed) |

---

## Versioning

No explicit API version prefix in the initial release. Version prefix (`/api/v1/headless`) is deferred to a future iteration if breaking changes are required.
