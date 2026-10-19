---
name: project-tests
description: Add focused xUnit unit, integration and contract tests for MelodyFlow and report actual results.
---

# Project Tests

## When to use

Use when asked to implement, debug or review the bounded topic in the description. First read `AGENTS.md`, `docs/DECISIONS.md` and `docs/TESTING.md`. Respect user's explicit approval policy.

## Preconditions

- Inspect actual solution/project and Git diff; distinguish planned vs existing components.
- Check credentials/host/worker/queue are actually available; never fabricate endpoints.
- Identify a specific acceptance test and impacted entity/DTO/API contract before writing.

## Workflow

1. Read applicable docs and real source; map requested behavior to a single feature.
2. Summarize current state and conflicts with catalog or decisions; request confirmation if changing architecture/schema.
3. Identify invariant; create failing test; implement behavior; use disposable PostgreSQL for EF, stubs for YouTube/LLM; verify test and no secret leaks; report commands and results.
4. Minimize edited file set, keep layers separated and choose clear names.
5. Build relevant projects/tests where possible; capture exact output and any blocked execution.
6. Review diff, update governing README/docs and report next step.

## Rules

- MUST preserve existing user files and unrelated working changes.
- MUST make changes testable and explain how errors/cancellation/invalid inputs behave.
- MUST NOT commit, push, deploy, spend credits, apply migrations or publish without explicit approval.
- MUST NOT store API keys or audio/video binary data in PostgreSQL.
- MUST respect ownership documented in `docs/TESTING.md`.
- When in read-only mode, do not write. Show a proposal and concrete snippets instead.

## Example

**Task:** Invalid duration rejected, retry capped at three, API 503 when backend unavailable.

**Expected response:** proposed files and fields, input/output, validation rules, tests and clear status of what actually ran. Before any destructive step, ask approval.

## Edge cases

Do not confuse test design with test execution.
Also handle missing files, unavailable services, network failures, duplicate job executions, null metadata, cancellation and pre-existing partial implementation; do not silently discard data.

## Acceptance criteria

- No architecture or dependency-boundary violations.
- Required behavior covered by a relevant test and/or reproducible smoke procedure.
- Documentation reflects actual implementation (not optimistic roadmap statements).
- Changed files and remaining uncertainties listed in final response.

## References

- Project: [`docs/TESTING.md`](../../../docs/TESTING.md)
- Governance: [`AGENTS.md`](../../../AGENTS.md), [`DECISIONS.md`](../../../docs/DECISIONS.md)
