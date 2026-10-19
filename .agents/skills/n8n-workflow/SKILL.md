---
name: n8n-workflow
description: Design, export and test n8n workflows that securely trigger MelodyFlow generation jobs and track status via HTTP.
---

# N8N Workflow

## When to use

Use when asked to implement, debug or review the bounded topic in the description. First read `AGENTS.md`, `docs/DECISIONS.md` and `docs/N8N-INTEGRATION.md`. Respect user's explicit approval policy.

## Preconditions

- Inspect actual solution/project and Git diff; distinguish planned vs existing components.
- Check credentials/host/worker/queue are actually available; never fabricate endpoints.
- Identify a specific acceptance test and impacted entity/DTO/API contract before writing.

## Workflow

1. Read applicable docs and real source; map requested behavior to a single feature.
2. Summarize current state and conflicts with catalog or decisions; request confirmation if changing architecture/schema.
3. Confirm local n8n connectivity to API (localhost vs Docker). The current API is local-only and intentionally has no authentication: keep both processes local, use server-to-server HTTP, and do not expose the API remotely. Add an `Idempotency-Key`, bounded status polling/backoff, and completion/failed branches; redact any future credentials on export and test duplicate triggers.
4. Minimize edited file set, keep layers separated and choose clear names.
5. Build relevant projects/tests where possible; capture exact output and any blocked execution.
6. Review diff, update governing README/docs and report next step.

## Rules

- MUST preserve existing user files and unrelated working changes.
- MUST make changes testable and explain how errors/cancellation/invalid inputs behave.
- MUST NOT commit, push, deploy, spend credits, apply migrations or publish without explicit approval.
- MUST NOT store API keys or audio/video binary data in PostgreSQL.
- MUST respect ownership documented in `docs/N8N-INTEGRATION.md`.
- When in read-only mode, do not write. Show a proposal and concrete snippets instead.

## Example

**Task:** Same schedule re-run creates only one job for idempotency key.

**Expected response:** proposed files and fields, input/output, validation rules, tests and clear status of what actually ran. Before any destructive step, ask approval.

## Edge cases

Do not connect n8n directly to the app database or place secrets in workflow JSON. If the integration ever moves beyond the local machine, design authentication and authorization before exposing it.
Also handle missing files, unavailable services, network failures, duplicate job executions, null metadata, cancellation and pre-existing partial implementation; do not silently discard data.

## Acceptance criteria

- No architecture or dependency-boundary violations.
- Required behavior covered by a relevant test and/or reproducible smoke procedure.
- Documentation reflects actual implementation (not optimistic roadmap statements).
- Changed files and remaining uncertainties listed in final response.

## References

- Project: [`docs/N8N-INTEGRATION.md`](../../../docs/N8N-INTEGRATION.md)
- Governance: [`AGENTS.md`](../../../AGENTS.md), [`DECISIONS.md`](../../../docs/DECISIONS.md)
