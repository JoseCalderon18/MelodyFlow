# MelodyFlow.Infrastructure

> **Status:** Present in the solution; EF Core/Npgsql persistence, PostgreSQL durable job queue and two migrations are implemented. Neither migration is applied.

## Template / runtime

**Class Library** (.NET 10 where applicable). Shared library or workflow folder; not automatically a process.

## Responsibility

Adapters: PostgreSQL EF Core/Npgsql, external LLM planner, storage, tools and queue implementations.

## Planned files and features

`MelodyFlowDbContext`, project/job configurations and repositories, `InitialMusicSchema`, and `AddPostgresGenerationJobQueue`. Queue claims use row locks and lease tokens. Music providers and media storage remain future work.

## Must not

Do not expose public API endpoints; no secrets in source.

## Dependencies / collaboration

See [global architecture](../docs/ARCHITECTURE.md) and [database guide](../docs/DATABASE-EFCORE.md). Infrastructure references Application/Domain and implements project persistence and the shared PostgreSQL job queue. API and Worker wire it through DI; this library does not expose HTTP endpoints.

## Implementation order

Inventory actual code → agree interface/schema → implement minimal vertical slice → tests → update docs and review diff. Consult `../AGENTS.md` and appropriate `.agents/skills` before changes.

## Definition of done

Project builds; relevant tests pass; dependency boundaries are respected; secrets absent from source; documentation matches behavior; source and migration diffs reviewed.
