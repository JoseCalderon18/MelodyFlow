---
name: ai-provider-adapter
description: Connect a licensed music/visual generation provider safely in the MelodyFlow solution with approval-first operation and reviewable outputs.
---

# ai-provider-adapter

## When to use
Use this skill for tasks matching: connect a licensed music/visual generation provider safely. Read root `AGENTS.md` and relevant project `README.md` first. If files cannot be accessed, ask for them; do not fabricate implementation state.

## Detailed procedure
Inputs: provider documentation & API access, license terms, pricing, generation requirements.

Procedure:
1. Verify **actual developer API** and current provider terms; consumer UI availability (e.g., Google Flow) does not automatically imply compatible API. If absent, report blocker instead of inventing HTTP endpoints.
2. Record provider version, auth scheme, input limits, async status/webhook behavior, output format, pricing & commercial rights.
3. Define Application port (`IAudioGenerationProvider` or `IVisualGenerationProvider`) and Infrastructure adapter with DTO translations.
4. Ensure key storage, timeouts, quotas, retries, idempotency and provider-specific failure mapping.
5. Start with fake provider and 1 short low-cost authorized test; no auto-loop.
6. Save media files to storage and only metadata/storage keys in DB; verify media type and license evidence.
7. Integrate worker state and cost reporting, add tests/mocks. Ask approval before external requests.

Output: provider comparison, contract samples grounded in docs, exact files, tests, cost cap, rights checklist. Stop if official API not available or commercial use unclear.

## Completion report
Explicitly separate **Implemented**, **Validated**, **Not tested**, **Risks**, **User approval still needed**. Never claim that file creation, migrations, deployment or API requests occurred unless supported by tool output.
