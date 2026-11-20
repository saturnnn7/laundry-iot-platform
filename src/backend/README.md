# Laundry backend

Modular monolith on .NET 10: one Host, shared BuildingBlocks and one folder per module.

Prerequisites: .NET 10 SDK (see global.json), Docker for the Testcontainers test, PowerShell 7 for the module script.

Build: `dotnet build`
Test: `dotnet test`

The integration tests start a PostgreSQL container (Testcontainers), so they need a running Docker daemon and pull `postgres:17-alpine` on the first run. Without Docker they fail, they are not skipped.

CI (`.github/workflows/backend.yml`) runs on every pull request: build and tests, the NuGet license check, a smoke test of the module script, and a smoke test of the Docker image.
Run the license check locally from the repo root: `pwsh ./tools/check-licenses.ps1`
Packages with a license that is not allowed need an entry in `tools/license-exceptions.txt`.

Run (set the connection string and the JWT signing key, see below; the outbox can be switched off for a local check):

    ConnectionStrings__Default="Host=localhost;Database=laundry;Username=postgres;Password=..." \
    Outbox__Enabled=false \
    dotnet run --project src/Host --urls http://localhost:8080

JWT signing key: the host does not start without `Jwt:SigningKey` (at least 32 bytes). For local runs use user secrets:

    dotnet user-secrets set "Jwt:SigningKey" "<random 48+ characters>" --project src/Host

or the environment variable `Jwt__SigningKey`. Never put a key in a tracked file. `Jwt:Issuer` and `Jwt:Audience` default to `laundry` and `laundry-api`.

Every route requires a valid bearer token unless it is marked anonymous (`/health` and the module ping routes are). Errors are RFC 9457 problem responses with a `type` slug.

Health check: `GET /health`. Module endpoints live under `/api/v1/<module>/...`.

## Identity quickstart

Needs Docker and PowerShell 7. Run these from `src/backend`.

Start PostgreSQL with a generated password (host port 5433, so a local PostgreSQL on 5432 is not in the way):

    $dbPassword = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(18)) -replace '[+/=]', 'x'
    docker run -d --name laundry-pg -e POSTGRES_PASSWORD=$dbPassword -e POSTGRES_DB=laundry -p 5433:5432 postgres:17-alpine
    docker exec laundry-pg pg_isready -U postgres

Run the Host in this window. The signing key is generated for this session only (48 random bytes, at least 32 are required):

    $env:ConnectionStrings__Default = "Host=localhost;Port=5433;Database=laundry;Username=postgres;Password=$dbPassword"
    $env:Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    $env:Database__MigrateOnStartup = 'true'
    dotnet run --project src/Host --urls http://localhost:8080

In a second window, register, log in and call `/me`:

    $api = 'http://localhost:8080/api/v1'
    $email = 'jane@example.com'
    $password = 'a long enough password'

    Invoke-RestMethod -Method Post -Uri "$api/auth/register" -ContentType 'application/json' `
        -Body (@{ email = $email; password = $password; displayName = 'Jane Doe'; accountType = 'Resident' } | ConvertTo-Json)

    $login = Invoke-RestMethod -Method Post -Uri "$api/auth/login" -ContentType 'application/json' `
        -Body (@{ email = $email; password = $password } | ConvertTo-Json)
    $login.expiresAtUtc

    Invoke-RestMethod -Uri "$api/me" -Headers @{ Authorization = "Bearer $($login.accessToken)" }

Register answers 201 with `id`, `email`, `displayName`, `accountType`. Login answers 200 with `accessToken`, `tokenType` and `expiresAtUtc`. `/me` answers 200 with the profile and `memberships` (an empty array until locations exist; each item will have `locationId`, `locationName` and `role`). A wrong password or an unknown email answers 401, and five wrong passwords lock the account for five minutes (429).

Stop the Host with Ctrl+C, then remove the container (its data goes with it):

    docker rm -f laundry-pg

Swagger: open `http://localhost:8080/swagger`, run `POST /api/v1/auth/login` with Try it out, copy the `accessToken` from the response, press Authorize, paste the token (without the word Bearer) and confirm. Then call `GET /api/v1/me`. The token is kept across page reloads. Routes with a padlock need the token.

## Swagger and health

Swagger UI is at `/swagger`, the OpenAPI document at `/openapi/v1.json`. Both are public. Set `Swagger:Enabled=false` (`Swagger__Enabled`) to turn them off.

Health endpoints, both anonymous and plain text:
- `GET /health/live` runs no checks and returns 200 while the process is up.
- `GET /health` runs all checks (PostgreSQL when a connection string is set) and returns 200 or 503.

## Outbox

Keys of the `Outbox` section (environment form `Outbox__<Key>`):

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Runs the pollers and the cleanup job. |
| `RetentionPeriod` | `7.00:00:00` | Processed rows older than this are deleted. Must be greater than zero. |
| `CleanupInterval` | `01:00:00` | Time between cleanup passes, one job per module schema. Must be greater than zero. |

Unprocessed and dead rows are never deleted. Rows go in batches of 1000. An invalid value stops the host at startup.

## Forwarded headers

Behind a reverse proxy, set the `ForwardedHeaders` section so the app sees the original client address and scheme (`X-Forwarded-For` and `X-Forwarded-Proto`). It is off by default and then the headers are ignored.

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `false` | Adds the forwarded headers middleware, first in the pipeline. |
| `KnownProxies` | empty | List of proxy IP addresses that may set the headers. |
| `KnownNetworks` | empty | List of CIDR networks that may set the headers, for example `172.18.0.0/16`. |
| `ForwardLimit` | `1` | How many proxy hops to process. Must be at least 1. |

Only the listed proxies and networks are trusted; the default loopback trust is removed. With `Enabled=true` at least one list must have an entry and every entry must be valid, or the host stops at startup. Lists in environment variables use an index:

    ForwardedHeaders__Enabled=true
    ForwardedHeaders__KnownNetworks__0=172.18.0.0/16
    ForwardedHeaders__KnownProxies__0=10.0.0.5

## API conventions

List endpoints use cursor pagination: `GET ...?cursor=<opaque>&limit=<n>`.

- `limit` defaults to 50, minimum 1, maximum 100. A value outside that range is a `validation` problem with an error on `limit`.
- `cursor` is an opaque string taken from `nextCursor` of the previous page. A value that cannot be decoded is a `validation` problem with an error on `cursor`. Omit it for the first page.
- Items are sorted by a UTC timestamp, then by id, so equal timestamps page correctly. Rows added while a client walks the pages never repeat a page already returned.
- The response is an object: `{ "items": [ ... ], "nextCursor": "..." }`. `nextCursor` is `null` on the last page.
- The cursor holds only the sort position. Each query adds its own scoping (location, status, ...).

In code: bind `[AsParameters] PageQuery`, call `ToRequest()`, then `query.ToPageAsync(request, timestamp, id, direction, projection)` on the scoped `IQueryable`.

## Migrations at startup

Set `Database:MigrateOnStartup=true` (`Database__MigrateOnStartup`) to apply the pending migrations of every module before the host starts listening. The default is false. A failed migration stops the startup.

## Docker

From the repo root:

    docker build -t laundry-backend:dev src/backend
    docker run --rm -p 8080:8080       -e ConnectionStrings__Default="Host=...;Database=laundry;Username=...;Password=..."       -e Jwt__SigningKey="<random 48+ characters>"       laundry-backend:dev

The container listens on port 8080 and runs as a non-root user. No secrets are baked into the image.

Add a module (from the repo root): `pwsh ./tools/new-module.ps1 -Name <Name>`
The script creates the five projects, registers them in the solution and the Host, and adds an Initial migration.

Layer rules (checked by tests/Laundry.Tests.Architecture):
- Domain depends only on BuildingBlocks.Domain. Application depends on its own Domain and Contracts plus BuildingBlocks.
- A module references another module only through its Contracts project. The Host references every Api project.
