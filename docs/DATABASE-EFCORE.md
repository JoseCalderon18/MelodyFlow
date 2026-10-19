# Database and EF Core (Npgsql) — design/runbook

## Responsibility

Domain entities describe business state. `MelodyFlow.Infrastructure` contains `MelodyFlowDbContext`, `IEntityTypeConfiguration<T>`, Npgsql/PostgreSQL implementation and migrations. `Application` defines persistence interfaces if needed. DTOs do not create tables.

## Known local development configuration (verify first)

| Item | Previously configured |
|---|---|
| Docker container | `melodyflow-postgres` |
| PostgreSQL version | 18 |
| Windows host | `localhost` |
| Host port | `5433` |
| Database | `melodyflow_db` |
| User | `admin` |
| Password | **Never in docs or repo** |
| Bind mount | `D:\MelodyFlow\Postgres\Volumen` → `/var/lib/postgresql` |

The user confirmed that the container name, host port, and database name remain current. Verify the PostgreSQL version, username, and bind mount before relying on those entries.

Inside a Docker network the hostname and port differ from Windows localhost (container name + `5432`). Do not assume Docker networking is configured until tested.

## Setup and examples

1. `docker ps --filter name=melodyflow-postgres` and inspect container health and logs.
2. The solution uses `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 and EF Core/Design 10.0.12. Infrastructure contains the `DbContext`, mappings, repository and migration; the API project is the startup host and carries the EF design-time tooling.
   Install the matching EF command-line tool if needed: `dotnet tool install --global dotnet-ef --version 10.0.12`.
3. Set the API project's development secret, not tracked `appsettings.json`:

```powershell
# Run from MelodyFlowAPI
dotnet user-secrets set "ConnectionStrings:MelodyFlow" "Host=localhost;Port=5433;Database=melodyflow_db;Username=admin;Password=REPLACE_LOCALLY"

# Change to the sibling Worker project to set its separate user-secrets store
Set-Location ..\MelodyFlow.Worker
dotnet user-secrets set "ConnectionStrings:MelodyFlow" "Host=localhost;Port=5433;Database=melodyflow_db;Username=admin;Password=REPLACE_LOCALLY"
```

Use environment variables or secret store in containers/production; never commit real password, OAuth token or API key.

## Current schema and queue mapping

`MusicProject` and `GenerationJob` are mapped in MelodyFlow.Infrastructure. Column names are snake_case, enums are stored as strings, requested job parameters use jsonb, and job-to-project deletion is Restrict. Queue indexes cover due-job polling and expired leases. Idempotency is unique within project + job type. Lease fields and retry scheduling are checked by the database. Both migrations are generated but have not been applied.

`generation_jobs` is also the durable queue: the API commits a job row before returning 202. The Worker claims due rows with `FOR UPDATE SKIP LOCKED`, assigns a fencing token and lease expiry, then renews the lease while a handler executes. A lost worker lease is retried within the three-attempt limit. This design needs no Redis or RabbitMQ service.

Inspect the migration and generated SQL before any database update. Changing stored enum names later requires a migration.

## Migrations workflow

- Current implemented scope: `MusicProject`, `GenerationJob` and the PostgreSQL queue fields/indexes. Do not create all publishing/analytics tables yet.
- `InitialMusicSchema` and `AddPostgresGenerationJobQueue` have been generated and reviewed in source; neither has been applied to PostgreSQL. The source does not establish the current database schema or whether the target database contains existing data.
- From `MelodyFlowAPI`, generate the SQL using explicit project and startup project. This command does not apply anything:

```powershell
dotnet ef migrations script --idempotent --project ..\MelodyFlow.Infrastructure\MelodyFlow.Infrastructure.csproj --startup-project .\MelodyFlow.API.csproj --context MelodyFlowDbContext
# Apply only after you inspect the generated SQL and explicitly choose to update this database:
dotnet ef database update --project ..\MelodyFlow.Infrastructure\MelodyFlow.Infrastructure.csproj --startup-project .\MelodyFlow.API.csproj --context MelodyFlowDbContext
```

Run these commands from `MelodyFlowAPI` (adjust the relative Infrastructure path if using another working directory). Never execute schema updates without explicit approval, against n8n/Odoo databases, or by deleting existing Docker volumes.

## Constraints / query guidance

Unique external IDs where appropriate, filtered index `(YouTubeChannelId, ExternalVideoId)` for non-null, due-status/time and lease-expiry indexes for job polling, explicit delete behaviors, no cascade on immutable history. The PostgreSQL queue uses row locks and lease tokens; it does not claim exactly-once delivery. Test Npgsql mappings and competing Worker claims with a real PostgreSQL integration test before relying on multiple Worker processes.

## References

https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/projects
https://www.npgsql.org/efcore/
