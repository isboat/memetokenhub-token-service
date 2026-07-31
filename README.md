# MemeTokenHub Token Service

The Token Service is the canonical project directory and discovery API for MemeTokenHub. It owns token metadata, private drafts, publication state, discovery indexes, on-chain metadata projections, and cached community aggregates. It exposes public search and feed endpoints while protecting creator and moderator write operations with JWT capability policies.

The service does **not** own follows, votes, KOL support, user accounts, claims, or payments. Those domains remain authoritative in their respective services; this service stores only the token-facing projections needed for fast discovery.

## Features

- Create and update private token project drafts.
- Publish validated projects with an immutable first-publication timestamp.
- Browse and search published projects with filters, sorting, and pagination.
- Retrieve trending, unclaimed, featured, and new discovery feeds.
- List projects by creator and aggregate published project counts by network.
- Return cached token sentiment and analytics projections.
- Issue short-lived HMAC-signed upload URLs for logos and banners.
- Enforce unique contract addresses per network through MongoDB indexes.
- Protect concurrent writes with optimistic version checks.
- Expose Swagger UI, RFC 7807 problem responses, and live/ready health probes.

## Technology

| Area | Technology |
| --- | --- |
| Runtime | .NET 10 / ASP.NET Core Web API |
| Persistence | MongoDB |
| API documentation | Swashbuckle / OpenAPI / Swagger UI |
| Authentication | JWT bearer authentication |
| Tests | NUnit, ASP.NET Core `WebApplicationFactory`, Coverlet |
| Packaging | `dotnet publish` and Docker |
| Deployment | GitHub Actions and Azure Web Apps |

The repository pins the .NET SDK in [`global.json`](global.json). Repository-wide nullable reference types, analyzer rules, warnings-as-errors, and formatting enforcement are configured in [`Directory.Build.props`](Directory.Build.props) and [`.editorconfig`](.editorconfig).

## Repository layout

```text
src/MemeTokenHub.TokenService.Api/
├── Application/
│   ├── Abstractions/       # SOLID interfaces for services, persistence, uploads, and time
│   ├── Models/             # Request, response, pagination, and error contracts
│   └── Services/           # Token application/business logic
├── Configuration/          # Strongly typed MongoDB, media, and Swagger configuration
├── Controllers/            # HTTP API endpoints
├── Domain/                 # MongoDB token document and domain value types
├── Infrastructure/         # MongoDB, upload signing, clock, indexes, and health checks
├── Middleware/             # RFC 7807 exception translation
└── Program.cs              # Composition root and HTTP pipeline

tests/
├── MemeTokenHub.TokenService.UnitTests/
├── MemeTokenHub.TokenService.IntegrationTests/
└── MemeTokenHub.TokenService.ContractTests/
```

Interfaces and concrete classes are kept in separate files. Controllers depend on application abstractions, application services depend on repository and clock abstractions, and infrastructure supplies the concrete implementations.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- MongoDB reachable by the API
- A JWT signing key of appropriate strength for the selected signing algorithm
- A media URL-signing key
- Optional: Docker for container builds
- Optional: a GitHub token with `read:packages` when consuming `MemeTokenHub.Shared`

Verify the selected SDK:

```bash
dotnet --version
```

## Configuration

ASP.NET Core configuration can be supplied through JSON, environment variables, command-line arguments, or a managed secret store. Use double underscores in environment variable names to represent nested keys.

| Setting | Environment variable | Required | Description |
| --- | --- | --- | --- |
| `MongoDb:ConnectionString` | `MongoDb__ConnectionString` | Yes | MongoDB connection string. |
| `MongoDb:DatabaseName` | `MongoDb__DatabaseName` | Yes | Token database name; defaults to `MemeTokenHubTokens`. |
| `Jwt:Key` | `Jwt__Key` | Yes | Secret used to validate backend JWT signatures. No production fallback is committed. |
| `Jwt:Issuer` | `Jwt__Issuer` | Yes | Expected JWT issuer. |
| `Jwt:Audience` | `Jwt__Audience` | Yes | Expected JWT audience. |
| `MediaStorage:UploadBaseUrl` | `MediaStorage__UploadBaseUrl` | Yes | HTTPS base URL that receives signed uploads. |
| `MediaStorage:AssetBaseUrl` | `MediaStorage__AssetBaseUrl` | Yes | Approved HTTPS CDN/public asset base URL. |
| `MediaStorage:SigningKey` | `MediaStorage__SigningKey` | Yes | Secret used to sign upload URLs. No production fallback is committed. |
| `MediaStorage:UploadLifetimeMinutes` | `MediaStorage__UploadLifetimeMinutes` | No | Signed upload lifetime; defaults to 10 minutes. |

Development-only JWT and media signing values are defined in `appsettings.Development.json`. Never reuse them outside local development. Production values should be injected from Azure App Settings, Key Vault, or an equivalent managed secret store.

Example local shell configuration:

```bash
export ASPNETCORE_ENVIRONMENT=Development
export MongoDb__ConnectionString='mongodb://localhost:27017'
export Jwt__Key='replace-with-a-long-random-local-signing-key'
export MediaStorage__SigningKey='replace-with-a-long-random-local-upload-key'
```

## Private package feed

[`NuGet.config`](NuGet.config) registers the private `MemeTokenHub.Shared` GitHub Packages feed at `https://nuget.pkg.github.com/isboat/index.json` and maps the `MemeTokenHub.Shared` package ID to it. Authenticate locally before adding or restoring a private package version:

```bash
dotnet nuget update source MemeTokenHub.Shared \
  --source https://nuget.pkg.github.com/isboat/index.json \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_TOKEN \
  --store-password-in-clear-text
```

Use a token with only the permissions required for package access. Do not commit credentials to `NuGet.config` or any application settings file.

## Run locally

Restore, build, and start the API from the repository root:

```bash
dotnet restore MemeTokenHub.TokenService.sln
dotnet build MemeTokenHub.TokenService.sln --configuration Debug --no-restore
dotnet run --project src/MemeTokenHub.TokenService.Api
```

The development launch profile exposes the application on the URLs printed by `dotnet run`. Open:

- Swagger UI: `/swagger`
- OpenAPI document: `/swagger/v1/swagger.json`
- Liveness probe: `/health/live`
- Readiness probe: `/health/ready`

At startup, the service creates the token ID, network/contract uniqueness, text-search, publication, and trending indexes. MongoDB must therefore be available for a normal non-test startup.

## Authentication and authorization

Public discovery endpoints do not require authentication. Write endpoints require a valid backend bearer token and the `projects:write` capability; users with the `Moderator` role also satisfy the write policy.

Send a token using the standard header:

```http
Authorization: Bearer <backend-jwt>
```

A creator may modify or publish only their own token. Moderators may modify or publish any token. Drafts are hidden from public callers and unrelated authenticated users.

## API overview

All token routes use the `/api/tokens` base path.

| Method | Route | Authentication | Purpose |
| --- | --- | --- | --- |
| `GET` | `/api/tokens` | Optional | Search published tokens with filters, sorting, and pagination. |
| `GET` | `/api/tokens/{tokenId}` | Optional | Get token detail; owners may retrieve their own drafts. |
| `POST` | `/api/tokens` | `projects:write` | Create a private token draft. |
| `PUT` | `/api/tokens/{tokenId}` | Owner or moderator | Update editable token metadata. |
| `POST` | `/api/tokens/{tokenId}/publish` | Owner or moderator | Idempotently publish a draft. |
| `POST` | `/api/tokens/media/upload-url` | `projects:write` | Create a signed logo or banner upload URL. |
| `GET` | `/api/tokens/feeds/{type}` | Optional | Get `trending`, `unclaimed`, `featured`, or `new` projects. |
| `GET` | `/api/tokens/by-creator/{creatorId}` | Optional | Get a creator's published projects. |
| `GET` | `/api/tokens/networks` | Optional | Get supported networks and published project counts. |
| `GET` | `/api/tokens/{tokenId}/sentiment` | Optional | Get cached hot/not-hot sentiment aggregates. |
| `GET` | `/api/tokens/analytics/{tokenId}` | Optional | Get the current public analytics projection. |

### Search parameters

`GET /api/tokens` accepts:

- `search`: text search across indexed project fields.
- `network`: normalized blockchain network filter.
- `category`: project category filter.
- `creatorId`: creator identifier filter.
- `status`: `Unclaimed`, `Claimed`, or `Featured`.
- `sortBy`: `createdAt` or `popularity`.
- `limit`: page size from 1 through 100; defaults to 20.
- `offset`: zero-based result offset.

List responses use this shape:

```json
{
  "items": [],
  "limit": 20,
  "offset": 0,
  "total": 0
}
```

Errors use `application/problem+json` and include the HTTP status, title, detail, request instance, and trace identifier. Expected failures include validation errors, hidden or missing resources, authorization failures, duplicate contracts, and optimistic-concurrency conflicts.

## Persistence and concurrency

Tokens are stored in the `Tokens` collection. A compound unique index on `network` and `contractAddress` is the authoritative duplicate guard. The application converts duplicate-key races into HTTP `409 Conflict` responses.

Updates use a persisted version field and replace only the expected document version. If another request updates or publishes the token first, the stale request receives `409 Conflict` instead of overwriting newer state.

## Media uploads

The media endpoint accepts JPEG, PNG, and WebP content types for `Logo` and `Banner` assets. It returns:

- a short-lived HMAC-signed upload URL;
- the future public asset URL; and
- the server-generated expiry timestamp.

The configured upload receiver must independently enforce signature validity, expiry, content length, content type, and final object path. The token API validates HTTPS project/media URLs, but the storage service remains responsible for accepting the upload safely.

## Health checks

- `/health/live` is process-only and does not depend on external services.
- `/health/ready` pings MongoDB and becomes unhealthy when the database is unavailable.

This distinction allows an orchestrator to restart dead processes while removing instances with unavailable dependencies from traffic without creating unnecessary restart loops.

## Tests and code quality

Run the same checks used by CI:

```bash
dotnet restore MemeTokenHub.TokenService.sln
dotnet format MemeTokenHub.TokenService.sln --no-restore --verify-no-changes
dotnet build MemeTokenHub.TokenService.sln --configuration Release --no-restore
dotnet test MemeTokenHub.TokenService.sln --configuration Release --no-build --no-restore
```

Test responsibilities:

- **Unit tests:** token lifecycle rules, URL validation, pagination, duplicate races, and optimistic concurrency.
- **Integration tests:** application startup, health endpoints, Swagger UI, and protected-route authentication.
- **Contract tests:** required OpenAPI routes and bearer security definitions.

Check direct and transitive package advisories with:

```bash
dotnet list MemeTokenHub.TokenService.sln package --vulnerable --include-transitive
```

## Docker

Build the production image:

```bash
docker build -t memetokenhub-token-service:local .
```

Run it with production configuration supplied at runtime:

```bash
docker run --rm -p 8080:8080 \
  -e MongoDb__ConnectionString='mongodb://host.docker.internal:27017' \
  -e Jwt__Key='replace-with-a-production-secret' \
  -e MediaStorage__SigningKey='replace-with-a-production-secret' \
  -e MediaStorage__UploadBaseUrl='https://uploads.example.com' \
  -e MediaStorage__AssetBaseUrl='https://cdn.example.com' \
  memetokenhub-token-service:local
```

Also set `Jwt__Issuer`, `Jwt__Audience`, and `MongoDb__DatabaseName` when their defaults do not match the target environment.

## CI/CD

### Pull requests

`.github/workflows/pull-request.yml` runs exclusively for `pull_request` events. It restores dependencies, verifies formatting, builds, and runs all tests. It does not publish artifacts or deploy.

### Main and manual deployment

`.github/workflows/main.yml` runs on pushes to `main` and through `workflow_dispatch`. It:

1. restores, formats, builds, and tests;
2. publishes and uploads the API artifact;
3. signs in to Azure using OpenID Connect; and
4. deploys the artifact to Azure Web Apps.

The production GitHub environment requires these repository settings:

| Type | Name | Purpose |
| --- | --- | --- |
| Secret | `AZURE_CLIENT_ID` | Federated Azure application/client ID. |
| Secret | `AZURE_TENANT_ID` | Azure tenant ID. |
| Secret | `AZURE_SUBSCRIPTION_ID` | Azure subscription ID. |
| Variable | `AZURE_WEBAPP_NAME` | Target Azure Web App name. |

Application settings such as MongoDB, JWT, and media-signing secrets must be configured on the target Web App or sourced from Key Vault; they are intentionally not embedded in the deployment artifact.

## Further documentation

The `docs/mth-docs` submodule contains the platform overview, component diagrams, microservice integration contract, shared-library instructions, and detailed Token Service blueprint. Initialize it after cloning:

```bash
git submodule update --init --recursive
```

Start with:

- `docs/mth-docs/doc/backend/token-service-instructions.md`
- `docs/mth-docs/doc/backend/microservice-integration-instructions.md`
- `docs/mth-docs/doc/backend/shared-library-instructions.md`
