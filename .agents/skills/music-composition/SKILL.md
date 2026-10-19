---
name: music-composition
description: Build the MelodyFlow deterministic music pipeline from an LLM arrangement JSON to MIDI, FluidSynth WAV and FFmpeg MP3.
---

# Music Composition

## When to use

Use when asked to implement, debug or review the bounded topic in the description. First read `AGENTS.md`, `docs/DECISIONS.md` and `docs/MUSIC-PIPELINE.md`. Respect user's explicit approval policy.

## Preconditions

- Inspect actual solution/project and Git diff; distinguish planned vs existing components.
- Check credentials/host/worker/queue are actually available; never fabricate endpoints.
- Identify a specific acceptance test and impacted entity/DTO/API contract before writing.

## Workflow

1. Read applicable docs and real source; map requested behavior to a single feature.
2. Summarize current state and conflicts with catalog or decisions; request confirmation if changing architecture/schema.
3. Select/validate arrangement schema; mock planner; render reproducible MIDI; verify SF2 license and FluidSynth executable; run constrained subprocess with cancellation; probe WAV/MP3; document output metadata.
4. Minimize edited file set, keep layers separated and choose clear names.
5. Build relevant projects/tests where possible; capture exact output and any blocked execution.
6. Review diff, update governing README/docs and report next step.

## Rules

- MUST preserve existing user files and unrelated working changes.
- MUST make changes testable and explain how errors/cancellation/invalid inputs behave.
- MUST NOT commit, push, deploy, spend credits, apply migrations or publish without explicit approval.
- MUST NOT store API keys or audio/video binary data in PostgreSQL.
- MUST respect ownership documented in `docs/MUSIC-PIPELINE.md`.
- When in read-only mode, do not write. Show a proposal and concrete snippets instead.

## Example

**Task:** A calm lofi plan at 78 BPM with chords produces a valid MIDI and audio using licensed SF2.

**Expected response:** proposed files and fields, input/output, validation rules, tests and clear status of what actually ran. Before any destructive step, ask approval.

## Edge cases

LM Studio is a local LLM server, not DAW; do not generate audio directly with generative AI.
Also handle missing files, unavailable services, network failures, duplicate job executions, null metadata, cancellation and pre-existing partial implementation; do not silently discard data.

## Acceptance criteria

- No architecture or dependency-boundary violations.
- Required behavior covered by a relevant test and/or reproducible smoke procedure.
- Documentation reflects actual implementation (not optimistic roadmap statements).
- Changed files and remaining uncertainties listed in final response.

## References

- Project: [`docs/MUSIC-PIPELINE.md`](../../../docs/MUSIC-PIPELINE.md)
- Governance: [`AGENTS.md`](../../../AGENTS.md), [`DECISIONS.md`](../../../docs/DECISIONS.md)
