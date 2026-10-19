# Delivery roadmap (features, not calendar promises)

| Phase | Deliverable | Acceptance |
|---|---|---|
| 0 | Inventory actual solution and reconcile docs | **Complete:** eight agreed projects are in `.slnx`; API-host topology and docs reconciled |
| 1 | MusicProject + GenerationJob + EF Core + project API | **Implemented:** create/list/get project endpoints, validated domain and mappings, generated migration, tests pass; migration not applied |
| 2 | PostgreSQL queue and job HTTP contract | **Implemented:** durable 202, GET status, idempotency and cancellation routes; migration generated, not applied |
| 3 | Queue and independent Worker | **Queue lifecycle implemented:** atomic claim, lease renewal/recovery, progress, cancellation and MaxAttempts=3; no music handler is registered yet |
| 4 | Planner JSON→MIDI→WAV→MP3 | One playable track via licensed SoundFont; provenance recorded |
| 5 | Manual Google Flow visual + FFmpeg | MP4 from audio + image/clip; video asset metadata only in DB |
| 6 | n8n schedule and notification | Local HTTP workflow can create jobs and poll/cancel status; duplicates prevented with the Idempotency-Key header |
| 7 | YouTube draft upload + analytics | Verified OAuth, resumable private upload, explicit approval |
| 8 | Blazor dashboard + operational quality | End-to-end state, cancellation and observability |

Each phase: learn → implement → test → document → review before next. Prioritize first complete, demonstrable pipeline over adding integrations early.
