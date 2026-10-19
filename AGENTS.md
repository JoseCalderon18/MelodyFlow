# MelodyFlow — agent instructions

## Scope and precedence

Read this file and `docs/DOCUMENTATION-MAP.md`, `docs/DECISIONS.md`, target project README and relevant skill **before** making code changes. Real code/migrations and explicit user requests trump stale proposals. Do not invent implementation status.

## Working agreement

1. Inventory actual files/solution/projects and `git status`; do not move or rename existing work without permission.
2. State objective, impacted projects, docs and tests. Show proposed changes before writing when user asks for review-first.
3. Avoid creating branches/PRs unless explicitly requested. Local changes reviewed via diff; never auto-commit/push.
4. Do not run destructive commands, overwrite documents, delete volumes, perform EF `database update`, deploy, post videos or spend API credits without explicit approval.
5. Apply Clean-inspired dependency rules from `docs/ARCHITECTURE.md`. `MelodyFlowAPI` is the public ASP.NET Core host and composition root; do not create `Backend.Host` or treat Class Libraries as independent deployed services.
6. Implement only requirements in agreed phase. Media binaries never in PostgreSQL. Never leak API keys, passwords, OAuth tokens or prompts containing secrets.
7. Run smallest relevant build and test if environment allows; report exact commands/results and anything not executed.
8. Update project README, schema catalog and contracts whenever modifying their governed behavior; flag disagreement and ask before resolving architecture conflict.
9. After an approved implementation, update the global README, every project README and the relevant `docs/` files in the same change. Preserve existing documentation: prefer localized edits and do not remove large sections without discussing it first.

## Music generation rule

LLM supplies JSON **arrangement plan**; C# composer generates MIDI; FluidSynth renders via licensed SoundFont; FFmpeg creates MP3/MP4. LM Studio != LMMS. n8n is workflow orchestrator, not musical renderer. MCP optional only, not a required dependency. MaxAttempts=3 including initial.

## Final response format

Changed files, why, proposed/implemented status, tests run, risks, manual actions required and diff review guidance. Explicitly say 'No files changed' when in analysis/read-only mode.
