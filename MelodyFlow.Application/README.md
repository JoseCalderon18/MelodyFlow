# MelodyFlow.Application

> **Status:** Present in the solution; project and generation-job create/read/cancel use cases and repository ports are implemented.

## Template / runtime

**Class Library** (.NET 10 where applicable). Shared library or workflow folder; not automatically a process.

## Responsibility

Use cases analogous to your Java Service layer; ports/interfaces for repos, queue, media and planners; calls Domain.

## Planned files and features

`CreateProject`, `GetProject`, paged `GetProjects`, durable job acceptance, request fingerprinting for idempotency, status lookup and cancellation. Queue persistence is implemented by Infrastructure.

## Must not

Do not implement Npgsql, n8n or external HTTP adapters here.

## Dependencies / collaboration

See [global architecture](../docs/ARCHITECTURE.md) and [contracts](../docs/API-CONTRACTS.md). Application references Domain and defines project/job repository ports; Infrastructure implements them. The API host invokes use cases in process and maps Contracts. Worker uses the job queue port from a separate process.

## Implementation order

Inventory actual code → agree interface/schema → implement minimal vertical slice → tests → update docs and review diff. Consult `../AGENTS.md` and appropriate `.agents/skills` before changes.

## Definition of done

Project builds; relevant tests pass; dependency boundaries are respected; secrets absent from source; documentation matches behavior; source and migration diffs reviewed.
