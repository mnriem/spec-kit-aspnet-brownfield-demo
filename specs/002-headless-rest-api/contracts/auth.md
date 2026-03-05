# Contract: Authentication

**Feature**: 002-headless-rest-api  
**Date**: 2026-03-05

---

## Overview

The headless API uses **JWT Bearer authentication**. All content endpoints require a valid, unexpired bearer token. The only unauthenticated endpoint is the token issuance endpoint.

---

## Token Endpoint

### `POST /api/headless/token`

**Authentication**: None (anonymous, `[AllowAnonymous]` with documented justification).

**Request**

```
Content-Type: application/json
```

```json
{
  "clientId": "my-spa-client",
  "clientSecret": "s3cr3t-PlainTextPassedOverTLS"
}
```

| Field          | Type     | Required | Constraints              |
|----------------|----------|----------|--------------------------|
| `clientId`     | `string` | yes      | 3–128 chars              |
| `clientSecret` | `string` | yes      | 8–512 chars              |

**Success Response — HTTP 200**

```json
{
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "tokenType": "Bearer",
    "expiresInSeconds": 3600,
    "expiresAt": "2026-03-05T15:00:00Z"
  },
  "meta": {
    "requestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "timestamp": "2026-03-05T14:00:00Z"
  }
}
```

**Error Responses**

| Status | Body | Condition |
|--------|------|-----------|
| 400    | ProblemDetails — validation | `clientId` or `clientSecret` missing/invalid length |
| 401    | ProblemDetails — `"Invalid credentials"` | ClientId not found, secret mismatch, client inactive, or expired |

**Security notes**:
- Response time for failed auth MUST be constant (prevent timing attacks). Use `PasswordHasher<T>.VerifyHashedPassword` which has constant-time comparison built in.
- Do NOT distinguish between "client not found" and "wrong secret" in the 401 message.
- Transport MUST be HTTPS in production.

---

## Bearer Token Usage

All content endpoints require the header:

```
Authorization: Bearer <token>
```

**JWT Claims**

| Claim         | Value                               |
|---------------|-------------------------------------|
| `iss`         | `"CarrotCakeCMS"`                   |
| `aud`         | `"CarrotCakeHeadlessConsumers"`     |
| `sub`         | `clientId` string                   |
| `jti`         | `Guid.NewGuid()`                    |
| `iat`         | Unix timestamp (seconds since epoch)|
| `exp`         | `iat + TokenExpiryMinutes * 60`     |
| `site_scope`  | `SiteId.ToString()` or `"*"`       |

**Validation parameters** (configured in `HeadlessApi` appsettings section):

```json
{
  "HeadlessApi": {
    "Issuer": "CarrotCakeCMS",
    "Audience": "CarrotCakeHeadlessConsumers",
    "TokenExpiryMinutes": 60,
    "JwtKey": "REPLACE_WITH_ENV_VAR_OR_DEV_PLACEHOLDER"
  }
}
```

**JWT signing**: HMAC-SHA256. Key sourced from environment variable `CARROT_HEADLESS_JWT_KEY` (Base64-encoded, ≥256 bits / 32 bytes).

---

## Error Conditions

| Condition | Status | Error message |
|-----------|--------|---------------|
| Token absent | 401 | `"Authorization token is required"` |
| Token malformed | 401 | `"Invalid token format"` |
| Token expired | 401 | `"Token has expired"` |
| Token signature invalid | 401 | `"Token validation failed"` |
| Site scope mismatch | 403 | `"Token is not authorized for site {siteId}"` |

---

## Configuration Reference

`CMSHeadlessApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "CarrotwareCMS": "Server=...;Database=CarrotCoreMVC;..."
  },
  "HeadlessApi": {
    "Issuer": "CarrotCakeCMS",
    "Audience": "CarrotCakeHeadlessConsumers",
    "TokenExpiryMinutes": 60,
    "JwtKey": "DEV_ONLY_PLACEHOLDER_SET_ENV_VAR_IN_PROD"
  }
}
```

**Required environment variable in production**:
```
CARROT_HEADLESS_JWT_KEY=<base64-encoded-256-bit-key>
```

Generate with:
```bash
openssl rand -base64 32
```
