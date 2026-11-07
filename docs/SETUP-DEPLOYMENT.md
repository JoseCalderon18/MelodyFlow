# Local development, secrets and deployment

1. `MelodyFlow.slnx` contains WebApp, API, Application, Domain, Infrastructure, Contracts, Worker and Tests. The API is the ASP.NET Core host; the class libraries are not processes. There is no `Backend.Host` project.
2. Check `dotnet --list-sdks`; target .NET 10 where intended. Restore and build each project before adding new packages.
3. Start the dedicated MelodyFlow PostgreSQL container after verifying its version; do not modify n8n/Odoo instances or delete volumes. Read `DATABASE-EFCORE.md`.
4. For local Development, use the ignored `appsettings.Local.json` in both API and Worker; the hosts load it automatically. Environment variables and command-line values override it. Alternatively use .NET User Secrets; never track credentials.
5. The local API listens on localhost via its launch profiles. It has no user login; configure only the WebApp origins in `Cors:AllowedOrigins`, keep it off public interfaces, and do not add an internal Backend URL.
6. The PostgreSQL `generation_jobs` table is the queue. The API and Worker use the same MelodyFlow connection string; no Redis/RabbitMQ service is required. From Windows use `localhost:5433` (Docker maps this to container port `5432`).
7. Optional: install LM Studio and load a local model whose license fits your use. Start its local server and note the model identifier if you plan to check “Usar IA para proponer los acordes”; the unchecked default does not call LM Studio.
8. Install FluidSynth, FFmpeg and FFprobe; place a legally obtained `FluidR3_GM.sf2` and its license notice on the machine. FluidR3 GM is MIT-licensed with attribution; LM Studio has no per-call charge for local inference, while model terms vary.
9. Configure the Worker audio settings below and set the same `Media:StorageRoot` for API and Worker. These audio prerequisites register the music handler. Configure the planner settings only if you will submit a job with `useAI: true`; if those settings are missing, an explicitly AI-enabled job will fail rather than silently run without AI.
10. All four current migrations are applied to the local development database. For another database, review its migration history and generated SQL before applying anything.

## Environment key names (no real values)

| Key | Consumer | Purpose |
|---|---|---|
| `ConnectionStrings__MelodyFlow` | API | PostgreSQL connection |
| `ConnectionStrings__MelodyFlow` | Worker | PostgreSQL connection for polling and job-state updates |
| `Cors__AllowedOrigins__0` | API | Allowed local WebApp origin (set additional indices as needed) |
| `MusicGeneration__Planner__BaseUrl` | Worker | Optional; local LM Studio OpenAI-compatible base URL, default `http://localhost:1234/v1`; used only when `useAI` is true |
| `MusicGeneration__Planner__Model` | Worker | Optional; identifier of the model loaded in LM Studio, required only when `useAI` is true |
| `MusicGeneration__Planner__ApiKey` | Worker | Optional local-server token; if used, store in User Secrets, never appsettings |
| `MusicGeneration__Planner__TimeoutSeconds` | Worker | HTTP planner timeout, 30–3600 seconds |
| `MusicGeneration__SoundFontPath` | Worker | Local `FluidR3_GM.sf2` path |
| `MusicGeneration__SoundFontLicenseEvidenceKey` | Worker | License reference stored with each generated track |
| `MusicGeneration__FluidSynthPath` | Worker | FluidSynth executable name or path |
| `MusicGeneration__FfmpegPath` | Worker | FFmpeg executable name or path |
| `MusicGeneration__FfprobePath` | Worker | FFprobe executable name or path |
| `MusicGeneration__ProcessTimeoutSeconds` | Worker | Maximum time for each audio process |
| `Media__StorageRoot` | API and Worker | Same absolute local folder for generated audio; blank uses the current account's LocalAppData folder |
| `N8N__WebhookSecret` | API/n8n | Future remote integration only; the local API does not use this setting |

These environment variable names map to the current Worker/API configuration sections. Never put credentials in README/AGENTS/SKILL files.

The ignored local file is `MelodyFlowAPI/appsettings.Local.json` or `MelodyFlow.Worker/appsettings.Local.json`, depending on the process. Start from the small connection-string example in [DATABASE-EFCORE.md](DATABASE-EFCORE.md). The Worker file can also hold `MusicGeneration:Planner:Model`, `MusicGeneration:SoundFontPath`, and `Media:StorageRoot`; use the same media path in both files if you choose a custom one. `Planner:ApiKey` is optional for local LM Studio and should remain absent unless that server requires a token.

The API has no user/service authentication and listens on localhost in its development profiles. Run local n8n on the same machine for direct `localhost` calls. A Dockerized n8n container needs an explicitly configured host-gateway route; only enable that with a local firewall rule because the API is not authenticated. Generated tracks are served by the API from `Media:StorageRoot`, so set an absolute path on both processes if you change the default folder.
