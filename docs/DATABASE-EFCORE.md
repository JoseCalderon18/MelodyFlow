# Database and EF Core (Npgsql) — design/runbook

## Responsibility

Domain entities describe business state. `MelodyFlow.Infrastructure` contains `MelodyFlowDbContext`, `IEntityTypeConfiguration<T>`, Npgsql/PostgreSQL implementation and migrations. `Application` defines persistence interfaces if needed. DTOs do not create tables.

## Known local development configuration (verify first)

| Item | Previously configured |
|---|---|
| Docker container | `melodyflow-postgres` (running and accepting connections) |
| PostgreSQL version | 18 (reported in the existing local setup; not re-verified in this check) |
| Windows host | `localhost` |
| Container port | `5432` |
| Windows host port | `5433` (`5433 → 5432/tcp` confirmed from Docker) |
| Database | `melodyflow_db` |
| User | `admin` (confirmed from container configuration) |
| Password | **Never in docs or repo** |
| Bind mount | `D:\MelodyFlow\Postgres\Volumen` → `/var/lib/postgresql` |

Docker currently reports `melodyflow-postgres` running, `pg_isready` accepts connections, and the host mapping is `localhost:5433` to container port `5432`. Use **5433** from applications running on Windows. The password is read only from local Docker configuration and must not be copied into tracked files or shown in logs.

Inside a Docker network the hostname and port differ from Windows localhost (container name + `5432`). Do not assume Docker networking is configured until tested.

## Setup and examples

1. `docker ps --filter name=melodyflow-postgres` and inspect container health and logs.
2. The solution uses `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 and EF Core/Design 10.0.12. Infrastructure contains the `DbContext`, mappings, repository and migration; the API project is the startup host and carries the EF design-time tooling.
   Install the matching EF command-line tool if needed: `dotnet tool install --global dotnet-ef --version 10.0.12`.
3. Fill `appsettings.Local.json` in both host projects. These files are ignored by Git and loaded only in Development; use the same connection string in each. Docker's internal port is 5432, but Windows connects through host port 5433:

```json
{
  "ConnectionStrings": {
    "MelodyFlow": "Host=localhost;Port=5433;Database=melodyflow_db;Username=admin;Password=SET_LOCALLY"
  }
}
```

The API and Worker each have their own ignored file. The local file overrides normal JSON settings; environment variables and command-line arguments override it. Alternatively, use the documented User Secrets/environment-variable approach and leave the local file absent.

Use environment variables or secret store in containers/production; never commit real password, OAuth token or API key.

## Current schema and queue mapping

`MusicProject`, `GenerationJob` and `MusicTrack` are mapped in MelodyFlow.Infrastructure. Column names are snake_case, enums are stored as strings, requested job parameters and arrangement plans use jsonb, and job/project deletion is Restrict. Queue indexes cover due-job polling and expired leases. Idempotency is unique within project + job type. Lease fields and retry scheduling are checked by the database. All four migrations, including `AddMusicTrackAiAttribution` for `music_tracks.used_ai`, are applied to the local development database; check migration history before updating another database.

`generation_jobs` is also the durable queue: the API commits a job row before returning 202. The Worker claims due rows with `FOR UPDATE SKIP LOCKED`, assigns a fencing token and lease expiry, then renews the lease while a handler executes. A lost worker lease is retried within the three-attempt limit. This design needs no Redis or RabbitMQ service.

Inspect the migration and generated SQL before any database update. Changing stored enum names later requires a migration.

## Migrations workflow

- Current implemented scope: `MusicProject`, `GenerationJob`, the PostgreSQL queue fields/indexes and `MusicTrack` metadata. Publishing/analytics tables are not created.
- `InitialMusicSchema`, `AddPostgresGenerationJobQueue`, `AddGeneratedMusicTracks` and `AddMusicTrackAiAttribution` are applied to the local development database. The source alone does not establish another database's schema or whether it contains existing data.
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
