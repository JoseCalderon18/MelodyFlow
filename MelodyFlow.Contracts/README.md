# MelodyFlow.Contracts

> **Status:** Present in the solution; project and generation-job request/response DTOs for API v1 are implemented.

## Template / runtime

**Class Library** (.NET 10 where applicable). Shared library or workflow folder; not automatically a process.

## Responsibility

Shared versioned public REST request/response DTOs between Web and API; optional internal contracts separated by namespace.

## Planned files and features

`CreateMusicProjectRequest`, `ProjectResponse`, `CreateGenerationJobRequest`, `GenerationJobResponse`, `PagedResponse<T>` and input validation. Only Music jobs are currently accepted.

## Must not

Never use DTOs as EF entities; no domain rules or DbContext.

## Dependencies / collaboration

See [global architecture](../docs/ARCHITECTURE.md) and [API contracts](../docs/API-CONTRACTS.md). Contracts is referenced by the API at build time and serialized over the WebApp/API HTTP boundary. It contains no EF entities or persistence dependency; there is no internal Backend HTTP contract.

## Implementation order

Inventory actual code → agree interface/schema → implement minimal vertical slice → tests → update docs and review diff. Consult `../AGENTS.md` and appropriate `.agents/skills` before changes.

## Definition of done

Project builds; relevant tests pass; dependency boundaries are respected; secrets absent from source; documentation matches behavior; source and migration diffs reviewed.
