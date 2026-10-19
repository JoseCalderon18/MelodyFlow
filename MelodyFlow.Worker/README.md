# MelodyFlow.Worker

> **Status:** Present in the solution with PostgreSQL queue polling, leases, retry/recovery and cooperative cancellation. No music/audio handler is registered yet.

## Template / runtime

**Worker Service** (.NET 10 where applicable). Independent deployment.

## Responsibility

Independent process consuming durable work; MIDI composition, FluidSynth, FFmpeg, persistence status; retries and cancellation.

## Planned files and features

The process polls the shared MelodyFlow PostgreSQL `generation_jobs` table for job types with registered handlers. Claims use `FOR UPDATE SKIP LOCKED` and a renewable lease. The worker currently has no music handler, so it leaves jobs queued instead of claiming work it cannot complete. MIDI/audio and FFmpeg adapters remain future work.

## Must not

Never perform heavy work on HTTP thread. Never claim background work when queue has not accepted it.

## Dependencies / collaboration

See [global architecture](../docs/ARCHITECTURE.md) and [workflow states](../docs/WORKFLOWS-AND-STATES.md). Worker is an independent process and shares PostgreSQL persistence through Infrastructure; it does not call an internal API host. Configure `ConnectionStrings:MelodyFlow` with Worker User Secrets before startup.

## Implementation order

Inventory actual code → agree interface/schema → implement minimal vertical slice → tests → update docs and review diff. Consult `../AGENTS.md` and appropriate `.agents/skills` before changes.

## Definition of done

Project builds; relevant tests pass; dependency boundaries are respected; secrets absent from source; documentation matches behavior; source and migration diffs reviewed.
