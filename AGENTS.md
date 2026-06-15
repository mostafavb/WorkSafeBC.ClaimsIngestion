# WorkSafeBC.ClaimsIngestion Guidelines

## Architecture
- Keep the dependency flow one-way: `Domain -> Application -> Infrastructure -> Worker/AppHost`.
- `WorkSafeBC.Claims.Domain` must stay free of Azure, messaging, telemetry, and `Microsoft.Extensions` dependencies.
- `WorkSafeBC.Claims.Application` owns use-case orchestration, contracts, and validation. It must not reference `Infrastructure` or `Worker`.
- `WorkSafeBC.Claims.Infrastructure` implements external boundaries declared in `Application`.
- `WorkSafeBC.Claims.Worker` is the composition root and runtime orchestrator only. Do not move business rules into the worker.

## Event Pipeline Rules
- Input files arrive through blob storage and are converted into canonical `ClaimIngestionEvent` messages.
- Preserve idempotency behavior when touching ingestion flow or file-processing ledger logic.
- Changes to published event shape require contract-test updates in `tests/WorkSafeBC.Claims.Tests.Contract`.
- New file formats or parsing rules require integration coverage against Azurite and RabbitMQ.

## Build and Test
- Restore/build from the solution root with `dotnet restore WorkSafeBC.ClaimsIngestion.slnx` and `dotnet build WorkSafeBC.ClaimsIngestion.slnx -c Release`.
- Run fast feedback with `dotnet test tests\WorkSafeBC.Claims.Tests.Unit\WorkSafeBC.Claims.Tests.Unit.csproj -c Release`.
- Run the full quality gate with the solution test projects, including architecture, contract, and integration suites.
- Integration tests require Docker because Testcontainers starts Azurite and RabbitMQ locally.

## Infrastructure and Configuration
- Local development uses `docker-compose.yml` for Azurite, RabbitMQ, Jaeger, Prometheus, and SQL Server.
- Azure deployment shape is defined under `deploy\bicep\`; keep resource names parameterized and environment-specific.
- Secrets must come from GitHub Secrets, Azure Key Vault, environment variables, or user secrets. Do not hardcode production secrets.

## Pull Request Expectations
- Every change should describe architecture impact, testing performed, telemetry impact, and secret/config changes when relevant.
- Validation changes need unit tests; event shape changes need contract tests; infrastructure adapter changes need integration tests.
- Keep edits surgical. Do not fold unrelated cleanup into the same PR.
