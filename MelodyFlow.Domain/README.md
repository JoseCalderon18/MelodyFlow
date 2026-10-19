# MelodyFlow.Domain

> **Status:** Present in the solution; project/job entities, retry states, lease ownership and cancellation transitions are implemented.

## Template / runtime

**Class Library** (.NET 10 where applicable). Shared library or workflow folder; not automatically a process.

## Responsibility

Pure music business model, entities, invariants, enums, state transitions. No EF/HTTP/process execution.

## Planned files and features

`MusicProject`, `GenerationJob`, project/job enums and invariants. `GenerationJob` protects attempt limits, idempotency metadata, lease ownership and valid cancellation/retry transitions; `MusicTrack`/`VisualAsset` remain later-phase work.

## Must not

No DbContext, controllers, DTOs, HTTP clients or ffmpeg commands.

## Dependencies / collaboration

See [global architecture](../docs/ARCHITECTURE.md) and [contracts](../docs/API-CONTRACTS.md). Domain has no project dependencies. Application and Infrastructure reference it; API consumes Contracts and composes Application/Infrastructure in process. Worker is a separate queue process; its media handler is still pending.

## Implementation order

Inventory actual code → agree interface/schema → implement minimal vertical slice → tests → update docs and review diff. Consult `../AGENTS.md` and appropriate `.agents/skills` before changes.

## Definition of done

Project builds; relevant tests pass; dependency boundaries are respected; secrets absent from source; documentation matches behavior; source and migration diffs reviewed.
