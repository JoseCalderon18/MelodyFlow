# Local development, secrets and deployment

1. `MelodyFlow.slnx` contains WebApp, API, Application, Domain, Infrastructure, Contracts, Worker and Tests. The API is the ASP.NET Core host; the class libraries are not processes. There is no `Backend.Host` project.
2. Check `dotnet --list-sdks`; target .NET 10 where intended. Restore and build each project before adding new packages.
3. Start the dedicated MelodyFlow PostgreSQL container after verifying its version; do not modify n8n/Odoo instances or delete volumes. Read `DATABASE-EFCORE.md`.
4. Use .NET User Secrets in the API project locally; environment variables/secrets manager in containers. `.env` with real credentials must not be tracked. Set `ConnectionStrings:MelodyFlow` before starting the API.
5. The local API listens on localhost via its launch profiles. It has no user login; configure only the WebApp origins in `Cors:AllowedOrigins`, keep it off public interfaces, and do not add an internal Backend URL.
6. The PostgreSQL `generation_jobs` table is the queue. The API and Worker use the MelodyFlow connection string; no Redis/RabbitMQ service is required. Set the connection string in User Secrets for both projects before running them.
7. Validate binary prerequisites only when needed: `ffmpeg -version`, `ffprobe -version`, `fluidsynth --version`; ensure licensed SoundFont exists.
8. Verify a single project build/test and smoke check before multi-start debugging.

## Environment key names (no real values)

| Key | Consumer | Purpose |
|---|---|---|
| `ConnectionStrings__MelodyFlow` | API | PostgreSQL connection |
| `ConnectionStrings__MelodyFlow` | Worker | PostgreSQL connection for polling and job-state updates |
| `Cors__AllowedOrigins__0` | API | Allowed local WebApp origin (set additional indices as needed) |
| `Arrangement__Provider` | worker | OpenRouter or LMStudio |
| `OpenRouter__ApiKey` | worker | Remote model API key if chosen |
| `LMStudio__BaseUrl` | worker | Local model endpoint |
| `Media__StorageRoot` | worker/backend | Managed storage root |
| `FluidSynth__ExecutablePath` | worker | FluidSynth executable |
| `FluidSynth__SoundFontPath` | worker | Licensed .sf2 path |
| `FFmpeg__ExecutablePath` | worker | FFmpeg executable |
| `N8N__WebhookSecret` | API/n8n | Future remote integration only; the local API does not use this setting |

These names are **proposed conventions**, not confirmed app configuration. Never put credentials in README/AGENTS/SKILL files.

The API has no user/service authentication and listens on localhost in its development profiles. Run local n8n on the same machine for direct `localhost` calls. A Dockerized n8n container needs an explicitly configured host-gateway route; only enable that with a local firewall rule because the API is not authenticated.
