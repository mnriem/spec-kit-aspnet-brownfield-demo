# Quickstart: Headless REST API — Developer Setup

**Feature**: 002-headless-rest-api  
**Date**: 2026-03-05

---

## Prerequisites

- .NET 8 SDK (`dotnet --version` should return `8.x.x`)
- SQL Server (local, Docker via feature 001, or Express)
- An existing CarrotCakeCMS database (`CarrotCoreMVC`) with at least one site and some published content
- `openssl` (to generate the JWT signing key)

---

## 1. Generate a JWT Signing Key

The key is a 32-byte (256-bit) secret encoded as Base64. **Never commit it.**

```bash
openssl rand -base64 32
# Example output: 4Bm7gJkL9PqRW2xY8nVfCeHsAiOdZtUQ1mKXvNwT3hE=
```

---

## 2. Create the New Project

From the repository root:

```bash
dotnet new webapi \
  --name CMSHeadlessApi \
  --framework net8.0 \
  --output CMSHeadlessApi \
  --use-controllers

cd CMSHeadlessApi

# Add project references
dotnet add reference ../CMSCore/CMSCore.csproj
dotnet add reference ../CarrotCMSData/CMSData.csproj
dotnet add reference ../CMSSecurity/CMSSecurity.csproj
dotnet add reference ../CarrotLog/CarrotLog.csproj

# Add NuGet packages
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.10
dotnet add package System.IdentityModel.Tokens.Jwt --version 8.1.2
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.10
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.0.10
dotnet add package Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore --version 8.0.10
dotnet add package Microsoft.Extensions.Caching.Memory --version 8.0.1
dotnet add package Microsoft.Data.SqlClient --version 5.2.2
dotnet add package BouncyCastle.Cryptography --version 2.4.0
```

Add the solution reference:

```bash
cd ..
dotnet sln CarrotCakeCoreMVC_All.sln add CMSHeadlessApi/CMSHeadlessApi.csproj
```

---

## 3. Configure appsettings.json

`CMSHeadlessApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "CarrotwareCMS": "Server=(local);Database=CarrotCoreMVC;Integrated Security=True;TrustServerCertificate=True"
  },
  "HeadlessApi": {
    "Issuer": "CarrotCakeCMS",
    "Audience": "CarrotCakeHeadlessConsumers",
    "TokenExpiryMinutes": 60,
    "JwtKey": "DEV_ONLY_REPLACE_ME_SET_ENV_VAR_IN_PROD"
  },
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "AllowedHosts": "*"
}
```

`CMSHeadlessApi/appsettings.Development.json`:

```json
{
  "HeadlessApi": {
    "JwtKey": "SET_THIS_TO_YOUR_GENERATED_KEY_FOR_LOCAL_DEV"
  }
}
```

**Set the key for local development** (do not hard-code in the file):

```bash
# Option A: .NET user secrets (local machine only)
cd CMSHeadlessApi
dotnet user-secrets init
dotnet user-secrets set "HeadlessApi:JwtKey" "4Bm7gJkL9PqRW2xY8nVfCeHsAiOdZtUQ1mKXvNwT3hE="

# Option B: environment variable
export CARROT_HEADLESS_JWT_KEY="4Bm7gJkL9PqRW2xY8nVfCeHsAiOdZtUQ1mKXvNwT3hE="
```

---

## 4. Run the EF Migration

Add the `CarrotApiClient` entity to `CarrotCakeContext` (see data-model.md), then:

```bash
# From the repository root
dotnet ef migrations add AddCarrotApiClient \
  --context CarrotCakeContext \
  --project CarrotCMSData \
  --startup-project CMSHeadlessApi \
  --output-dir Migrations

dotnet ef database update \
  --context CarrotCakeContext \
  --project CarrotCMSData \
  --startup-project CMSHeadlessApi
```

---

## 5. Provision a Test API Client

After running migrations, insert a test client directly in the database.  
The client secret must be hashed using `PasswordHasher<CarrotApiClient>`. Use the seed helper in `TokenService` (or a temporary migration seed), or use the following SQL after computing the hash in a small console snippet:

```csharp
// One-time helper (e.g., in a migration seed or test console):
var hasher = new PasswordHasher<CarrotApiClient>();
string hash = hasher.HashPassword(null!, "my-super-secret");
Console.WriteLine(hash);
```

Then:

```sql
INSERT INTO CarrotApiClients
  (ApiClientId, ClientId, ClientSecretHash, ScopeToSiteId, IsActive, CreatedDateUtc, ExpiresDateUtc, Description)
VALUES
  (NEWID(), 'dev-client', '<paste-hash-here>', NULL, 1, GETUTCDATE(), NULL, 'Local development client');
```

---

## 6. Run the API

```bash
cd CMSHeadlessApi
dotnet run
# API available at: https://localhost:5001 (or http://localhost:5000)
```

Check it's running:

```bash
curl http://localhost:5000/health
# {"status":"Healthy"}
```

---

## 7. Obtain a Token

```bash
curl -s -X POST http://localhost:5000/api/headless/token \
  -H "Content-Type: application/json" \
  -d '{"clientId":"dev-client","clientSecret":"my-super-secret"}' \
  | jq '.data.token'
```

Copy the token value.

---

## 8. Call a Content Endpoint

```bash
TOKEN="<paste-token-here>"

# List all pages
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/headless/pages" | jq .

# Get page by slug
curl -s -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5000/api/headless/pages?slug=/about-us" | jq .
```

---

## 9. Run with Test Scripts

See `specs/002-headless-rest-api/test-scripts/` for:

- `headless-api.http` — VS Code REST Client file (open in VS Code with REST Client extension)
- `test-api.sh` — Bash curl script

```bash
# Run all curl tests (requires jq)
chmod +x specs/002-headless-rest-api/test-scripts/test-api.sh
BASE_URL=http://localhost:5000 \
CLIENT_ID=dev-client \
CLIENT_SECRET=my-super-secret \
  specs/002-headless-rest-api/test-scripts/test-api.sh
```

---

## Docker Compose (with feature 001 infrastructure)

If using the Docker Compose setup from feature 001, override the connection string:

```bash
SA_PASSWORD=YourStrong!Passw0rd \
CARROT_HEADLESS_JWT_KEY="4Bm7gJkL9PqRW2xY8nVfCeHsAiOdZtUQ1mKXvNwT3hE=" \
  docker compose up
```

Add `CMSHeadlessApi` as a service in `compose.yaml` following the same pattern as `CMSAdmin`.

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| 401 on token endpoint | Wrong clientId/secret | Re-check the inserted DB row and secret hash |
| `IDX10503: Signature validation failed` | `JwtKey` mismatch between token issuer and validator | Ensure same key value is configured |
| `No site found` errors | No site in `CarrotSites` table | Run CMSAdmin setup or seed from CMSDataScripts |
| Empty `data` array on `/pages` | No published pages (PageActive=true, within GoLive/Retire) | Publish at least one page in CMSAdmin |
