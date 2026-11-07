# MelodyFlow

> **Status — first music pipeline implementation:** The API supports project/job operations plus generated track lookup/download. The WebApp can create projects and music jobs. The Worker defaults to deterministic C# composition and offers an opt-in local LM Studio arrangement proposal; both paths render MIDI through FluidSynth and FFmpeg. All four migrations, including the track's `used_ai` column, are applied to the local PostgreSQL database. A real audio render still depends on local audio tools and a SoundFont; LM Studio/model setup is needed only when `useAI` is enabled.

MelodyFlow is a C#/.NET 10 music composition and video production platform. By default, deterministic C# rules choose musical arrangements; an optional AI planner can propose them when selected per job. The MIDI composer creates notes, FluidSynth renders audio from a SoundFont, FFmpeg encodes media, and a background Worker runs jobs. It is **not** an AI audio-generation model. YouTube publication is a later optional module.

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

1. The WebApp creates a `MusicProject` through the public API; the API persists it through Application and Infrastructure/EF Core.
2. The WebApp or n8n creates a `Music` job through `POST /api/v1/generation-jobs`; the API returns 202 after PostgreSQL commits the row. `requestedParameters.useAI` defaults to `false`. Reuse `Idempotency-Key` on transport retries.
3. The Worker claims supported work with PostgreSQL row locks and renewable leases, then applies bounded retries and cancellation. It registers the music handler when the SoundFont/license reference and audio executables are configured; a local model is needed only for jobs with `useAI: true`.
4. With the checkbox clear, C# builds a deterministic arrangement. If selected, local **LM Studio** supplies the arrangement proposal; the same C# MIDI composer and audio renderer then continue the pipeline.
5. FluidSynth renders WAV from `FluidR3_GM.sf2`, FFmpeg encodes MP3, and FFprobe records the measured duration. PostgreSQL stores track metadata and a relative `StorageKey`; audio files stay in the configured media folder.
6. `GET /api/v1/generation-jobs/{id}/track` returns the track and its audio route; `GET /api/v1/music-tracks/{id}/audio` streams the MP3.
7. n8n coordinates schedules/status/notifications through the local API. n8n is not the media renderer and must not connect to PostgreSQL directly.
8. YouTube publishing later uses official OAuth 2.0 and the YouTube Data API; uploads private first for review.

## Non-goals and explicit decisions

- Never call LM Studio a DAW or assume it synthesizes audio. **LMMS** is a separate optional workstation for manual editing and is not required by the automated render path.
- For music intended for YouTube monetization, verify current commercial-use terms for each concrete tool, model, SoundFont and other resource before selection/use and again before publication; preserve and follow all required attribution and notices. A license is terms of use, not a software version or a key to enter. Prefer free options without assuming zero total cost or commercial permission.
- For tracks whose metadata records `usedAI: true`, follow YouTube's current AI disclosure rules and channel monetization policies. Licensing or disclosure does not guarantee monetization eligibility; originality, authenticity and reused or mass-produced content also matter.
- No unverified MCP requirement. MCP may become an adapter/tool interface in a later phase, but **REST/CLI are sufficient for the first automated pipeline**.
- `MelodyFlowAPI` is the only ASP.NET Core host in this phase and composes Application/Infrastructure in process. Do not add a duplicate backend host. The local API has no user login; its launch profiles bind to localhost, CORS is restricted and requests are rate-limited and validated.

## Local prerequisites and status

.NET 10 SDK, Visual Studio 2026, Docker Desktop, PostgreSQL, FluidSynth, FFmpeg/FFprobe, and a locally obtained SoundFont with its license notice. LM Studio and a loaded model are optional unless `useAI` is enabled. Keep local binaries and credentials out of Git.

## Where to start

1. Read [`docs/DOCUMENTATION-MAP.md`](docs/DOCUMENTATION-MAP.md), [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), and [`docs/DECISIONS.md`](docs/DECISIONS.md).
2. The solution includes WebApp, API, Application, Domain, Infrastructure, Contracts, Worker and Tests; see each project README for its current state.
3. Review [`docs/DATABASE-EFCORE.md`](docs/DATABASE-EFCORE.md) for the local database state and connection setup. All four current migrations are applied locally. Inspect migration history before updating a different database.
4. Review `docs/SETUP-DEPLOYMENT.md` to configure the shared media storage root, local planner model and audio executables. Apply migrations only after reviewing their SQL and deciding to update your database.
5. Use the project, generation-job and music-track API contracts to prepare the local n8n workflow. The API has no authentication and must stay local.

## Security & contributions

Follow [`AGENTS.md`](AGENTS.md), [`docs/SECURITY-AND-RIGHTS.md`](docs/SECURITY-AND-RIGHTS.md), [`docs/TESTING.md`](docs/TESTING.md) and [`docs/SETUP-DEPLOYMENT.md`](docs/SETUP-DEPLOYMENT.md). For every change: inspect existing code; propose a plan and diff; never overwrite user files or apply schema changes without approval; run relevant tests and report results honestly.
