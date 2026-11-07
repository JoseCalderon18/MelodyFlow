# Test strategy

`MelodyFlow.Tests` (xUnit) is a dedicated test project, not a service. Testing is part of acceptance, not optional busywork.

After the music-track and generation path changes, `dotnet build MelodyFlow.slnx --no-restore` succeeded with 0 warnings and 0 errors. No test command was run in that implementation turn. The new MIDI/audio process, track endpoint and music metadata behavior remain without focused tests or local end-to-end verification.

The current suite covers project validation and listing, generation-job retry/lease/cancellation transitions, EF model metadata without a database connection, and API create/list/get/validation plus durable-job contract behavior using an in-memory repository. These tests do not prove PostgreSQL row-lock behavior or connectivity to the configured container.

Latest local run: `dotnet test MelodyFlow.Tests\MelodyFlow.Tests.csproj --no-restore` — 25 passed, 0 failed. `dotnet build MelodyFlow.slnx --no-restore` — succeeded with 0 warnings and 0 errors. API contract tests use in-memory repositories and the EF model only; PostgreSQL was not contacted.

| Area | Test | Expected | Current status |
|---|---|---|---|
| Domain | Invalid project details and job transitions | Invalid input rejected; retry/cancel states enforced | Implemented and tested |
| GenerationJob | Attempts 1/2/3, transient retry, lease expiry, cancellation | MaxAttempts=3; cancel retry immediately; running cancel is cooperative; expired workers are recovered | Implemented and tested |
| Application | Create project, bounded paging | Repository port called; overflowing page offset rejected | Implemented and tested |
| Infrastructure | Npgsql model metadata (no database connection) | Tables, jsonb, constraints and indexes match mappings | Implemented and tested; live DB integration remains future |
| HTTP | DTO validation, create/get, 404, paging, CORS and 429 | Project contract and local protections behave as configured | Implemented and tested |
| Worker | handler registration, lease renewal, process crash/cancellation | API remains running; expired job can be recovered; unsupported job types are not claimed | Queue lifecycle and conditional music handler implemented; music failure/retry cases need focused tests |
| Music | Deterministic arrangement JSON → MIDI | Valid MIDI with tempo/instrument/notes | Code implemented; focused test pending |
| FluidSynth + FFmpeg | Integration with local SF2 and short file | Outputs playable WAV/MP3; media probe succeeds | Code implemented; local integration pending |
| Media storage | Forbidden traversal/path | Reject unsafe input; no files stored in DB | Code implemented; focused test pending |
| n8n | Repeat workflow trigger with same Idempotency-Key | No duplicate GenerationJob; payload mismatch returns 409 | API contract implemented; live n8n smoke test pending |
| Publishing | Mocked YouTube upload | Private draft + no token leaks | Future |

No test is considered passed until a tool/CI run reports results. Report commands and failures; avoid test claims based on looking at code.
