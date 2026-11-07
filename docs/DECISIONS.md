# Architecture decision log

| ID | Decision | State | Reason / caveat |
|---|---|---|---|
| ADR-001 | MelodyFlowAPI is the public ASP.NET Core host and composition root | **Agreed; phase 1 implemented** | API references Application and Infrastructure in process; no duplicate Backend.Host project |
| ADR-002 | Clean Architecture-inspired dependency rules | Agreed design | Domain pure, Application orchestrates, Infrastructure adapts |
| ADR-003 | Independent Worker with PostgreSQL-backed durable queue | **Agreed and implemented:** `generation_jobs` is polled with `FOR UPDATE SKIP LOCKED`; leases recover work after process loss | No extra broker is required for the local MVP; PostgreSQL remains a shared dependency |
| ADR-004 | PostgreSQL + EF Core/Npgsql | **Agreed; all four current migrations applied locally** | Confirm migration history before updating any other database |
| ADR-005 | n8n for automation schedules/orchestration | **Agreed:** local n8n calls the local API over HTTP; no DB direct | No user/API authentication in this local-only phase; do not expose the API outside the machine |
| ADR-006 | Music jobs default to deterministic C# arrangement; an explicit `useAI: true` option may ask local LM Studio for a validated plan; C# composes MIDI and FluidSynth renders it | **Implemented in Worker:** `useAI` defaults false, actual use is recorded in track metadata, and only AI-enabled jobs need a loaded model | Local inference does not require a provider API key; any selected model and SoundFont have their own terms. Keep required attribution; no binary is bundled |
| ADR-007 | FFmpeg and FFprobe for encoding and media duration | **Implemented in Worker:** subprocess arguments are passed without a shell; exit status and measured duration are checked | Local executables are installed/configured by the user; generated media stays outside PostgreSQL |
| ADR-008 | Google Flow visuals imported manually first | Agreed | `VisualAsset.IsManualUpload` and nullable `ProviderName` |
| ADR-009 | Music tracks/assets reusable | Agreed; `MusicTrack` metadata and generated-audio storage are implemented | Join entities preserve per-video ordering; media binaries remain in local storage |
| ADR-010 | Max 3 total GenerationJob attempts | Agreed | Retry only transient failures |
| ADR-011 | YouTube integration later, private upload for review | Agreed, later phase | OAuth; before monetized publication verify concrete commercial terms for all selected tools/models/assets, retain required notices, and follow current AI disclosure and channel monetization policies; no eligibility promise |
| ADR-012 | MCP only when useful; not required for MVP | Agreed | Could expose safe music tools later |
| ADR-013 | Testing essential, incremental | Agreed | xUnit Domain tests then database/contract/integration |

## Decisions still open

Local LM Studio is an optional adapter for jobs explicitly marked `useAI: true`; deterministic composition is the default. OpenRouter and remote providers are not part of this implementation. The SoundFont choice remains FluidR3 GM; retain its source license and attribution with the local file. Idempotency keys are optional and scoped to `(MusicProjectId, JobType)`; reusing a key with a different normalized request returns 409. The local app does not use user login, JWTs or API keys; its boundary is loopback binding, restricted CORS, rate limiting and input validation.
