# Laundry backend

Modular monolith on .NET 10: one Host, shared BuildingBlocks and one folder per module.

Prerequisites: .NET 10 SDK (see global.json), Docker for the Testcontainers test, PowerShell 7 for the module script.

Build: `dotnet build`
Test: `dotnet test`
Run (set the connection string and the JWT signing key, see below; the outbox can be switched off for a local check):

    ConnectionStrings__Default="Host=localhost;Database=laundry;Username=postgres;Password=..." \
    Outbox__Enabled=false \
    dotnet run --project src/Host --urls http://localhost:8080

JWT signing key: the host does not start without `Jwt:SigningKey` (at least 32 bytes). For local runs use user secrets:

    dotnet user-secrets set "Jwt:SigningKey" "<random 48+ characters>" --project src/Host

or the environment variable `Jwt__SigningKey`. Never put a key in a tracked file. `Jwt:Issuer` and `Jwt:Audience` default to `laundry` and `laundry-api`.

Every route requires a valid bearer token unless it is marked anonymous (`/health` and the module ping routes are). Errors are RFC 9457 problem responses with a `type` slug.

Health check: `GET /health`. Module endpoints live under `/api/v1/<module>/...`.

Add a module (from the repo root): `pwsh ./tools/new-module.ps1 -Name <Name>`
The script creates the five projects, registers them in the solution and the Host, and adds an Initial migration.

Layer rules (checked by tests/Laundry.Tests.Architecture):
- Domain depends only on BuildingBlocks.Domain. Application depends on its own Domain and Contracts plus BuildingBlocks.
- A module references another module only through its Contracts project. The Host references every Api project.
