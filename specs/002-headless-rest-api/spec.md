# Feature Specification: Headless REST API for CMS Content

**Feature Branch**: `002-headless-rest-api`  
**Created**: 2026-03-05  
**Status**: Draft  
**Input**: User description: "Add a public read-only REST API to the CMS that exposes pages, blog posts, navigation trees, content snippets, and widget zones as JSON endpoints. API consumers should be able to query content by URL slug, category, tag, date range, and site ID for multi-site setups. Secure the API with token-based authentication so it can serve as a headless backend for SPAs, static site generators, or mobile apps."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Retrieve Published Pages by Slug (Priority: P1)

A front-end developer building a single-page application (SPA) needs to fetch the full content of a CMS page by its URL slug. They call the API with an API token and receive all published page data as JSON — including title, body content, metadata, and canonical URL — which they then render in their application.

**Why this priority**: Fetching individual pages by slug is the most fundamental read operation and the baseline capability required by every headless consumer. Without it, no downstream use case (blog posts, navigation, snippets) is meaningful.

**Independent Test**: Can be fully tested by issuing a GET request to `/api/content/pages?slug=/about-us` with a valid API token and confirming the response contains the correct page title, body, and metadata.

**Acceptance Scenarios**:

1. **Given** a published page exists with slug `/about-us`, **When** an authenticated consumer requests the page by that slug, **Then** the API returns the page title, body content, metadata, and HTTP 200.
2. **Given** a page exists but is in draft/unpublished state, **When** a consumer requests it by slug, **Then** the API returns HTTP 404 (draft content is never exposed).
3. **Given** a non-existent slug is requested, **When** the consumer calls the endpoint, **Then** the API returns HTTP 404 with a descriptive error message.
4. **Given** no API token is provided, **When** the consumer calls any content endpoint, **Then** the API returns HTTP 401 Unauthorized.

---

### User Story 2 - List and Filter Blog Posts (Priority: P2)

A mobile app developer needs to display a paginated list of blog posts filtered by category, tag, or date range. They call the API with filter parameters and receive a structured JSON list of post summaries (title, slug, excerpt, publish date, categories, tags) which they render in a news feed.

**Why this priority**: Blog post listing with filtering is the second most common headless use case and critical for content-driven apps and static site generators. It enables news feeds, search pages, and archives.

**Independent Test**: Can be fully tested by requesting `/api/content/posts?category=news&page=1` with a valid token and verifying the response contains a paginated list of posts belonging to that category.

**Acceptance Scenarios**:

1. **Given** published blog posts exist, **When** a consumer requests the post list with no filters, **Then** the API returns a paginated list with post summaries and total count.
2. **Given** posts exist with various categories, **When** the consumer filters by `category=news`, **Then** only posts in that category are returned.
3. **Given** posts exist with various tags, **When** the consumer filters by `tag=featured`, **Then** only posts bearing that tag are returned.
4. **Given** a date range filter is applied (`dateFrom`, `dateTo`), **When** the consumer requests posts, **Then** only posts published within that range are returned.
5. **Given** a multi-site CMS setup, **When** the consumer passes `siteId=<id>`, **Then** only content belonging to that site is returned.

---

### User Story 3 - Fetch Navigation Trees (Priority: P3)

A static site generator needs the full navigation hierarchy to generate a site menu. The consumer requests the navigation tree for a given site and receives a structured JSON tree of navigation nodes — including page titles, URLs, nesting levels, and order — which is used to build HTML menus.

**Why this priority**: Navigation trees are essential for building coherent front-end layouts but are often simpler to render than page content. Separating from P1/P2 allows those to ship first and be useful independently.

**Independent Test**: Can be fully tested by requesting `/api/content/navigation?siteId=<id>` and verifying the response is a hierarchical JSON tree with parent-child relationships and href values.

**Acceptance Scenarios**:

1. **Given** a site has a configured navigation tree, **When** the consumer requests navigation for that site, **Then** the API returns a nested JSON tree representing the full menu hierarchy.
2. **Given** a navigation node is hidden or unpublished, **When** the consumer requests the tree, **Then** hidden nodes are excluded from the response.
3. **Given** no `siteId` is provided on a single-site CMS, **When** the consumer requests navigation, **Then** the default site's navigation is returned.

---

### User Story 4 - Retrieve Content Snippets and Widget Zones (Priority: P4)

A front-end team wants to pull reusable content snippets (e.g., promotional banners, footer copy) and widget zone configurations from the CMS to render them in a SPA without hardcoding them. They retrieve snippets by key/name and widget zones by page and zone name.

**Why this priority**: Snippets and widgets add flexibility but are supplementary to core page and post data. They build on the already-established authentication and content exposure patterns.

**Independent Test**: Can be fully tested by requesting `/api/content/snippets?name=promo-banner` and `/api/content/widgetzones?pageSlug=/home&zone=sidebar`, verifying structured JSON is returned.

**Acceptance Scenarios**:

1. **Given** a content snippet named `promo-banner` exists, **When** the consumer requests it by name, **Then** the API returns the snippet's content as JSON.
2. **Given** a widget zone is configured for a page, **When** the consumer requests that zone by page slug and zone name, **Then** the API returns the ordered list of widget configurations in that zone.
3. **Given** a requested snippet does not exist, **When** the consumer calls the endpoint, **Then** the API returns HTTP 404.

---

### User Story 5 - Obtain an API Token (Priority: P1)

A developer integrating a new headless client needs to obtain an API token to authenticate their application's requests. They exchange a set of credentials (client ID and secret, or a pre-issued key) for a bearer token, which they include in subsequent API requests.

**Why this priority**: Token issuance is a prerequisite for all other stories. Without authentication, no content can be accessed, making this P1 alongside page retrieval.

**Independent Test**: Can be fully tested by posting credentials to the token endpoint and verifying a valid bearer token is returned, then using that token to retrieve a page successfully.

**Acceptance Scenarios**:

1. **Given** valid credentials are submitted, **When** the consumer calls the token endpoint, **Then** a bearer token with an expiry time is returned.
2. **Given** invalid credentials are submitted, **When** the consumer calls the token endpoint, **Then** HTTP 401 is returned with no token issued.
3. **Given** a bearer token has expired, **When** the consumer uses it to access a content endpoint, **Then** HTTP 401 is returned prompting re-authentication.
4. **Given** a valid, unexpired token is presented, **When** any content endpoint is called, **Then** the request succeeds and content is returned.

---

### Edge Cases

- What happens when a requested slug exists in multiple sites? The `siteId` parameter disambiguates; if missing, the API returns HTTP 400 asking for `siteId`.
- How does the system handle pagination beyond the available result set? The API returns an empty `items` array and the total count, not an error.
- What happens when both `category` and `tag` filters are applied simultaneously? Both filters are applied with AND logic, returning only posts matching both criteria.
- How does the system handle a `siteId` that does not exist? HTTP 404 is returned.
- What happens if the CMS has no published content? The API returns an empty list with HTTP 200 (not 404).
- How does the system handle malformed query parameters? HTTP 400 is returned with a descriptive validation message.
- What happens when a token is well-formed but tampered with? HTTP 401 is returned.

## Requirements *(mandatory)*

### Functional Requirements

**Token Authentication**

- **FR-001**: The API MUST require a valid bearer token on all content endpoints; unauthenticated requests MUST receive HTTP 401.
- **FR-002**: The API MUST provide a dedicated token issuance endpoint where consumers exchange credentials for a bearer token.
- **FR-003**: Bearer tokens MUST carry an expiry and MUST be rejected after expiry with HTTP 401.
- **FR-004**: The API MUST NOT expose any CMS administration, write, or mutation operations; all endpoints MUST be strictly read-only.

**Page Content**

- **FR-005**: The API MUST expose an endpoint to retrieve a single published page by URL slug, returning title, body, metadata, and publish date.
- **FR-006**: The API MUST return HTTP 404 for draft, scheduled, or deleted pages when queried by consumers.
- **FR-007**: The API MUST support filtering page listings by `siteId`.

**Blog Posts**

- **FR-008**: The API MUST expose an endpoint to list published blog posts with support for filtering by category, tag, date range (`dateFrom`, `dateTo`), and `siteId`.
- **FR-009**: The API MUST expose an endpoint to retrieve a single blog post by URL slug.
- **FR-010**: Blog post list responses MUST be paginated, with the consumer able to specify page number and page size, and the response including total result count.
- **FR-011**: The API MUST return only published posts; drafts and scheduled posts MUST NOT be accessible via the API.

**Navigation Trees**

- **FR-012**: The API MUST expose an endpoint to return the full navigation tree for a given site as a hierarchical JSON structure.
- **FR-013**: Navigation nodes that are hidden or unpublished MUST be excluded from the tree response.
- **FR-014**: On a single-site CMS, if no `siteId` is provided, the default site's navigation MUST be returned.

**Content Snippets**

- **FR-015**: The API MUST expose an endpoint to retrieve a content snippet by its name or identifier.
- **FR-016**: The API MUST return HTTP 404 when a requested snippet does not exist.

**Widget Zones**

- **FR-017**: The API MUST expose an endpoint to retrieve the ordered list of widget zone configurations for a given page and zone name.
- **FR-018**: Widget zone responses MUST include enough configuration data for the consumer to render or proxy-render each widget.

**Multi-site Support**

- **FR-019**: All content endpoints MUST accept an optional `siteId` parameter to scope queries to a specific site in multi-site CMS installations.
- **FR-020**: When a `siteId` is provided that does not exist, the API MUST return HTTP 404.

**General API Behaviour**

- **FR-021**: All responses MUST use JSON with consistent envelope structure (e.g., `data`, `meta`, `errors` fields).
- **FR-022**: The API MUST validate incoming query parameters and return HTTP 400 with descriptive messages for invalid inputs.
- **FR-023**: The API MUST return appropriate HTTP status codes for all scenarios (200, 400, 401, 404).

### Key Entities

- **Page**: A CMS content page with a unique URL slug, title, rich-text body, SEO metadata, publish status, and site association.
- **Blog Post**: A dated content item with title, slug, excerpt, full body, publish date, author reference, categories, tags, and site association.
- **Category**: A classification label that organises blog posts; has a name and slug.
- **Tag**: A free-form label attached to blog posts; has a name and slug.
- **Navigation Node**: A single item in a navigation tree with a display title, href, display order, nesting level, and visibility flag.
- **Navigation Tree**: An ordered hierarchical collection of Navigation Nodes belonging to a site.
- **Content Snippet**: A named, reusable block of CMS-managed content (text or rich text) identified by a unique name/key.
- **Widget Zone**: A named container on a page that holds an ordered list of configured widget instances.
- **Widget Instance**: A single widget placed in a widget zone, carrying its type identifier and configuration data.
- **API Token**: A time-limited credential issued to an authenticated consumer that authorises access to read-only content endpoints.
- **Site**: A multi-site tenant within the CMS; scopes all content, navigation, snippets, and widgets.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can integrate a new headless front-end client and retrieve live page content in under 30 minutes from receiving API credentials.
- **SC-002**: All content endpoints respond within 500 milliseconds for typical queries (single item or paginated list with default page size) under normal load.
- **SC-003**: The API correctly enforces authentication on 100% of content requests; no content is ever returned to unauthenticated callers.
- **SC-004**: Filtering and pagination produce correct, consistent results: applying category, tag, date range, and site scope filters in combination returns only matching published content.
- **SC-005**: The API supports at least 200 concurrent consumer connections without degraded response times or errors.
- **SC-006**: A static site generator can retrieve all published pages, posts, and navigation for a site in a single build pass using only the documented endpoints.
- **SC-007**: All edge-case inputs (invalid tokens, unknown slugs, malformed parameters) receive appropriate HTTP error responses with no unhandled exceptions exposed to the caller.

## Assumptions

- The CMS already has a concept of "published" vs "draft" content distinguishable at the data layer; the API leverages this without introducing a new status model.
- Multi-site support is already present in the underlying CMS data model; the API surfaces this via the `siteId` parameter rather than creating new site management capabilities.
- API consumers are trusted internal or partner applications, not anonymous end-users; token issuance involves a managed credential exchange (not self-registration by the public).
- Content body returned by the API is the final stored HTML/text as managed in the CMS editor; the API does not perform server-side rendering or template processing.
- Widget zone responses expose configuration data; the consumer is responsible for rendering widgets client-side or proxying to widget-specific endpoints if needed.
- Rate limiting and abuse prevention are handled at the infrastructure layer (e.g., reverse proxy or API gateway) and are out of scope for this feature.
- Token storage and rotation policies are the responsibility of the API consumer; the CMS only issues and validates tokens.

## Out of Scope

- Write operations (create, update, delete) via the API.
- Public unauthenticated access to any content endpoint.
- Real-time push/streaming (webhooks or WebSockets) for content change notifications.
- Search-as-a-service (full-text search) beyond the defined filter parameters.
- Media/asset (image, file) serving endpoints.
- CMS user management or administrative operations.
