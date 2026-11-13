# MelodyFlow

> **Status — local music/video studio:** The API supports projects, queued music generation, song playback/download, manual MP4 clip import and one-clip/one-song video renders. The WebApp shows saved songs, polls active job progress every two seconds, and includes clip import, render selection and MP4 playback/download. It also lets users delete songs, clips and rendered videos after confirmation; tracks and clips used by a render are protected. The Worker defaults to deterministic C# composition and offers an opt-in local LM Studio arrangement proposal. The MIDI composer varies form, chord progressions, genre grooves and up to eight GM palettes, develops a motif, resolves the final bar to the tonic and adds bounded seeded timing/velocity variation. The advanced mixer renders separate harmony, bass, melody and drum stems and gives each a gain and pan control before FFmpeg creates the MP3. When a render title is omitted, the API builds one from the song title, project mood and genre. This is a practical offline stem mixer, not a full DAW or mastering suite. Migration `AddVisualAssetsAndRenderedVideos` is applied locally; a real imported-clip render is still to be verified.

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
4. With the checkbox clear, C# builds a deterministic arrangement. Optional form, groove, texture-density and mix-profile controls can refine it. If selected, local **LM Studio** supplies the arrangement proposal; the same C# MIDI composer and audio renderer then continue the pipeline.
5. The composer creates separate harmony, bass, lead, percussion and conductor MIDI tracks, with voice-leading, motif development, genre-specific grooves, section energy, eight instrument palette variations and a tonic ending. FluidSynth renders four temporary WAV stems from `FluidR3_GM.sf2`; the WebApp's per-part gain/pan values are applied by FFmpeg before the stems are summed, peak-limited and encoded to MP3. FFprobe records the measured duration. Reverb/chorus remain global mix-profile effects; this is a practical offline stem mixer, not a full DAW, per-stem EQ or mastering suite. PostgreSQL stores track metadata and a relative `StorageKey`; audio files stay in the configured media folder and temporary stems are removed.
6. `GET /api/v1/generation-jobs/{id}/track` returns the track and its audio route; `GET /api/v1/music-tracks?page=1&pageSize=20` supplies the saved-song library; `GET /api/v1/music-tracks/{id}/audio` streams the MP3. `DELETE /api/v1/music-tracks/{id}` removes an unused track and its MP3; it returns `409` if a rendered video still uses it.
7. The user can import a Google Flow MP4 through the API. The clip is saved in `video-clips/`; PostgreSQL stores its metadata and relative key only. The WebApp uses the matching HTTP or HTTPS launch profile so a streamed upload is not redirected mid-request. `DELETE /api/v1/visual-assets/{id}` removes an unused clip and its MP4; it returns `409` if a render uses it.
8. The user selects one saved song and one imported clip. The API queues a Render job; if no custom title is entered, Application builds a title from the song title plus its project's mood and genre (for example, `Rainy Rooftop | Relaxing Ambient Music`). The Worker reports FFmpeg progress, combines/loops the MP4 to the song duration, stores the output under `videos/`, and saves render metadata with that title. The WebApp polls the job and plays/downloads the finished MP4 using a filename-safe version of the title. `DELETE /api/v1/rendered-videos/{id}` removes the result and its MP4.
9. n8n coordinates schedules/status/notifications through the local API. n8n is not the media renderer and must not connect to PostgreSQL directly.
10. YouTube publishing later uses official OAuth 2.0 and the YouTube Data API; uploads private first for review.

## Non-goals and explicit decisions

- Never call LM Studio a DAW or assume it synthesizes audio. **LMMS** is a separate optional workstation for manual editing and is not required by the automated render path.
- For music intended for YouTube monetization, verify current commercial-use terms for each concrete tool, model, SoundFont and other resource before selection/use and again before publication; preserve and follow all required attribution and notices. A license is terms of use, not a software version or a key to enter. Prefer free options without assuming zero total cost or commercial permission.
- For tracks whose metadata records `usedAI: true`, follow YouTube's current AI disclosure rules and channel monetization policies. Licensing or disclosure does not guarantee monetization eligibility; originality, authenticity and reused or mass-produced content also matter.
- No unverified MCP requirement. MCP may become an adapter/tool interface in a later phase, but **REST/CLI are sufficient for the first automated pipeline**.
- `MelodyFlowAPI` is the only ASP.NET Core host in this phase and composes Application/Infrastructure in process. Do not add a duplicate backend host. The local API has no user login; its launch profiles bind to localhost, CORS is restricted and requests are rate-limited and validated.

## Local prerequisites and status

.NET 10 SDK, Visual Studio 2026, Docker Desktop, PostgreSQL, FluidSynth, FFmpeg/FFprobe, and a locally obtained SoundFont with its license notice. LM Studio and a loaded model are optional unless `useAI` is enabled. Keep local binaries and credentials out of Git.

Configure PostgreSQL, media storage, temporary files, audio tools and the optional local planner through each app's local launch profile. Keep machine-specific paths, credentials and internal setup notes out of the public repository. The video metadata migration is applied to the current local database.

## Where to start

1. Read `AGENTS.md` before changing the code.
2. The solution includes WebApp, API, Application, Domain, Infrastructure, Contracts, Worker and Tests; their responsibilities are summarized above.
3. Configure local secrets and machine-specific paths in ignored launch profiles. Review migration SQL and database history before updating another database.
4. The API has no authentication and must stay local. Use its project, generation-job and music-track endpoints to connect an optional local n8n workflow.

## Security & contributions

Follow [`AGENTS.md`](AGENTS.md). For every change: inspect existing code, preserve user files, review schema changes before applying them, run relevant tests and report results honestly. Verify current licenses for the selected model, SoundFont and media tools before publication.
