# MelodyFlow

> **Status — queue foundation:** The eight agreed projects build. Project CRUD, durable PostgreSQL job acceptance, idempotency, status/cancellation endpoints, queue leases and worker retry/recovery mechanics are implemented and covered by tests. `InitialMusicSchema` and `AddPostgresGenerationJobQueue` are generated but have not been applied. The Worker has no music/audio handler yet; media generation remains a later phase.

MelodyFlow is a C#/.NET 10 music composition and video production platform. An AI planner proposes structured musical arrangements, a **deterministic composer/MIDI generator** turns them into notes, FluidSynth renders audio from licensed SoundFonts, FFmpeg encodes and assembles media, and a background worker runs jobs. It is **not** an AI audio-generation model. The UI imports Google Flow visuals manually; YouTube publication is a later optional module.

## Quick orientation

| Component | Project/template | Role | Deployment |
|---|---|---|---|
| API | `MelodyFlowAPI` / ASP.NET Core Web API | Public REST boundary, composition root, validation and local protections | ASP.NET process; local launch profiles bind to localhost |
| Web | `MelodyFlow.WebApp` / Blazor Web App | User interface and status pages | Independent web process |
| Domain | `MelodyFlow.Domain` / Class Library | Entities, enums, business invariants | Referenced library, not deployable |
| Application | `MelodyFlow.Application` / Class Library | Use cases, interfaces, orchestration policies | Referenced library |
| Infrastructure | `MelodyFlow.Infrastructure` / Class Library | EF Core/Npgsql, adapters and storage implementations | Referenced library |
| Contracts | `MelodyFlow.Contracts` / Class Library | Versioned HTTP message contracts shared by clients | Referenced library |
| Worker | `MelodyFlow.Worker` / .NET Worker Service | Claims supported jobs from PostgreSQL and runs registered handlers | Independent process |
| Tests | `MelodyFlow.Tests` / xUnit | Domain, application and integration tests | Test project |
| Workflows | n8n export/config, **not** necessarily .NET project | Schedules, notifications, human approval and webhook-based orchestration | Independent n8n process |

> Names in your existing Visual Studio solution may differ. **Do not rename or recreate existing projects without inspecting the `.slnx`/`.csproj` files.** A single solution groups projects for development and multi-start debugging; process isolation comes from independent deployment, not different solution files.

## End-to-end workflow

1. The API validates and persists `MusicProject` through Application and Infrastructure/EF Core. The Blazor template is not yet connected to these endpoints.
2. Local n8n creates `Music` jobs through `POST /api/v1/generation-jobs`; the API returns 202 only after PostgreSQL commits the row. `GET` status and cancellation routes are available. Reuse `Idempotency-Key` on transport retries.
3. The Worker claims supported work with PostgreSQL row locks and renewable leases, then applies bounded retries and cancellation. No music/audio handler is registered yet, so jobs remain `Queued` until the pipeline is implemented.
4. The future music handler will request an arrangement **JSON** plan from **OpenRouter** or a local **LM Studio** endpoint, validate the plan, and compose MIDI.
5. FluidSynth will render with a licensed SoundFont and FFmpeg will create MP3/MP4. Persist file references (`StorageKey`) only; not media binaries in PostgreSQL.
6. n8n coordinates schedules/status/notifications through the local API. n8n is not the media renderer and must not connect to PostgreSQL directly.
7. YouTube publishing later uses official OAuth 2.0 and the YouTube Data API; uploads private first for review.

## Non-goals and explicit decisions

- Never call LM Studio a DAW or assume it synthesizes audio. **LMMS** is a separate optional workstation for manual editing and is not required by the automated render path.
- Do not promise monetization or that avoiding an AI-generated-audio model removes disclosure, copyright or reused-content obligations. Review current YouTube policies and licensing.
- No unverified MCP requirement. MCP may become an adapter/tool interface in a later phase, but **REST/CLI are sufficient for the first automated pipeline**.
- `MelodyFlowAPI` is the only ASP.NET Core host in this phase and composes Application/Infrastructure in process. Do not add a duplicate backend host. The local API has no user login; its launch profiles bind to localhost, CORS is restricted and requests are rate-limited and validated.

## Local prerequisites and status

.NET 10 SDK, Visual Studio 2026, Docker Desktop, PostgreSQL, n8n, and FFmpeg/FluidSynth when that phase is implemented. User previously configured a PostgreSQL container called `melodyflow-postgres` with DB `melodyflow_db` and Windows host port `5433`. **Verify the PostgreSQL version, username and connection locally; never assume it's online**. Keep credentials out of Git.

## Where to start

1. Read [`docs/DOCUMENTATION-MAP.md`](docs/DOCUMENTATION-MAP.md), [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), and [`docs/DECISIONS.md`](docs/DECISIONS.md).
2. The solution includes WebApp, API, Application, Domain, Infrastructure, Contracts, Worker and Tests; see each project README for its current state.
3. Review [`docs/DATABASE-EFCORE.md`](docs/DATABASE-EFCORE.md), configure the same connection string in API and Worker User Secrets, then inspect the generated SQL. Migrations are not applied.
4. Use the project and generation-job API contracts to prepare the local n8n workflow. The API has no authentication and must stay local; the music handler is the remaining generation feature.

## Security & contributions

Follow [`AGENTS.md`](AGENTS.md), [`docs/SECURITY-AND-RIGHTS.md`](docs/SECURITY-AND-RIGHTS.md), [`docs/TESTING.md`](docs/TESTING.md) and [`docs/SETUP-DEPLOYMENT.md`](docs/SETUP-DEPLOYMENT.md). For every change: inspect existing code; propose a plan and diff; never overwrite user files or apply schema changes without approval; run relevant tests and report results honestly.
