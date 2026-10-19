---
name: queue-worker
description: Implement safe independent background jobs, durable queue integration, retries, cancellation and progress in MelodyFlow.Worker.
---

# Queue Worker

## When to use

Use when asked to implement, debug or review the bounded topic in the description. First read `AGENTS.md`, `docs/DECISIONS.md` and `docs/ARCHITECTURE.md`. Respect user's explicit approval policy.

## Preconditions

- Inspect actual solution/project and Git diff; distinguish planned vs existing components.
- Check credentials/host/worker/queue are actually available; never fabricate endpoints.
- Identify a specific acceptance test and impacted entity/DTO/API contract before writing.

## Workflow

1. Read applicable docs and real source; map requested behavior to a single feature.
2. Summarize current state and conflicts with catalog or decisions; request confirmation if changing architecture/schema.
3. Use the agreed PostgreSQL `generation_jobs` table as the durable queue unless the user approves a different provider. Define lifecycle/lease; persist and enqueue in the same row/transaction; claim with row locks and fencing tokens; use an outbox only if a future design adds a separate broker; bound processes/timeouts; update status; test worker crash and retry; record metrics.
4. Minimize edited file set, keep layers separated and choose clear names.
5. Build relevant projects/tests where possible; capture exact output and any blocked execution.
6. Review diff, update governing README/docs and report next step.

## Rules

- MUST preserve existing user files and unrelated working changes.
- MUST make changes testable and explain how errors/cancellation/invalid inputs behave.
- MUST NOT commit, push, deploy, spend credits, apply migrations or publish without explicit approval.
- MUST NOT store API keys or audio/video binary data in PostgreSQL.
- MUST respect ownership documented in `docs/ARCHITECTURE.md`.
- When in read-only mode, do not write. Show a proposal and concrete snippets instead.

## Example

**Task:** AttemptCount increments per execution; MaxAttempts=3 total.

**Expected response:** proposed files and fields, input/output, validation rules, tests and clear status of what actually ran. Before any destructive step, ask approval.

## Edge cases

Avoid claiming exactly-once delivery; no FFmpeg in API controller.
Also handle missing files, unavailable services, network failures, duplicate job executions, null metadata, cancellation and pre-existing partial implementation; do not silently discard data.

## Acceptance criteria

- No architecture or dependency-boundary violations.
- Required behavior covered by a relevant test and/or reproducible smoke procedure.
- Documentation reflects actual implementation (not optimistic roadmap statements).
- Changed files and remaining uncertainties listed in final response.

## References

- Project: [`docs/ARCHITECTURE.md`](../../../docs/ARCHITECTURE.md)
- Governance: [`AGENTS.md`](../../../AGENTS.md), [`DECISIONS.md`](../../../docs/DECISIONS.md)
