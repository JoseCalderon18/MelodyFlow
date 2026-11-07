# Delivery roadmap (features, not calendar promises)

| Phase | Deliverable | Acceptance |
|---|---|---|
| 0 | Inventory actual solution and reconcile docs | **Complete:** eight agreed projects are in `.slnx`; API-host topology and docs reconciled |
| 1 | MusicProject + GenerationJob + EF Core + project API | **Implemented:** create/list/get project endpoints, validated domain and mappings, generated migration, tests pass; migration applied locally |
| 2 | PostgreSQL queue and job HTTP contract | **Implemented:** durable 202, GET status, idempotency and cancellation routes; migration generated and applied locally |
| 3 | Queue and independent Worker | **Implemented:** atomic claim, lease renewal/recovery, progress, cancellation, MaxAttempts=3 and a conditionally registered Music handler |
| 4 | Planner JSON→MIDI→WAV→MP3 | **Code implemented:** deterministic C# arrangement by default, optional local LM Studio planner, FluidSynth, FFmpeg/FFprobe, track metadata and API download. Local prerequisites and an end-to-end render still need setup/verification |
| 5 | Manual Google Flow visual + FFmpeg | MP4 from audio + image/clip; video asset metadata only in DB |
| 6 | n8n schedule and notification | Local HTTP workflow can create jobs, poll/cancel status and retrieve generated audio; duplicates prevented with the Idempotency-Key header. Workflow setup/run remains with the user |
| 7 | YouTube draft upload + analytics | Verified OAuth, resumable private upload, explicit approval |
| 8 | Blazor dashboard + operational quality | End-to-end state, cancellation and observability |

Each phase: learn → implement → test → document → review before next. The current implementation builds; focused music-pipeline tests and a local end-to-end render remain pending.
