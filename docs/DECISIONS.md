# Architecture decision log

| ID | Decision | State | Reason / caveat |
|---|---|---|---|
| ADR-001 | MelodyFlowAPI is the public ASP.NET Core host and composition root | **Agreed; phase 1 implemented** | API references Application and Infrastructure in process; no duplicate Backend.Host project |
| ADR-002 | Clean Architecture-inspired dependency rules | Agreed design | Domain pure, Application orchestrates, Infrastructure adapts |
| ADR-003 | Independent Worker with PostgreSQL-backed durable queue | **Agreed and implemented:** `generation_jobs` is polled with `FOR UPDATE SKIP LOCKED`; leases recover work after process loss | No extra broker is required for the local MVP; PostgreSQL remains a shared dependency |
| ADR-004 | PostgreSQL + EF Core/Npgsql | **Agreed; mappings and queue migrations generated** | `InitialMusicSchema` and `AddPostgresGenerationJobQueue` are not applied to a database |
| ADR-005 | n8n for automation schedules/orchestration | **Agreed:** local n8n calls the local API over HTTP; no DB direct | No user/API authentication in this local-only phase; do not expose the API outside the machine |
| ADR-006 | LLM provides composition plan; code composes MIDI; FluidSynth renders | Agreed direction, unimplemented | Not generative audio; optional OpenRouter or LM Studio for planning |
| ADR-007 | FFmpeg for media and encoding | Agreed | Separate process, checked exit code |
| ADR-008 | Google Flow visuals imported manually first | Agreed | `VisualAsset.IsManualUpload` and nullable `ProviderName` |
| ADR-009 | Music tracks/assets reusable | Agreed | Join entities preserve per-video ordering |
| ADR-010 | Max 3 total GenerationJob attempts | Agreed | Retry only transient failures |
| ADR-011 | YouTube integration later, private upload for review | Agreed, later phase | OAuth; policy/licensing checks |
| ADR-012 | MCP only when useful; not required for MVP | Agreed | Could expose safe music tools later |
| ADR-013 | Testing essential, incremental | Agreed | xUnit Domain tests then database/contract/integration |

## Decisions still open

The source of a commercially suitable SoundFont license and the arrangement planner choice (LM Studio or OpenRouter) remain open. Idempotency keys are optional and scoped to `(MusicProjectId, JobType)`; reusing a key with a different normalized request returns 409. The local app does not use user login, JWTs or API keys; its boundary is loopback binding, restricted CORS, rate limiting and input validation.
