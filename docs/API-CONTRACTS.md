# API contracts v1 — implemented and proposed routes

## Boundaries

Web App → `MelodyFlowAPI` via versioned HTTP JSON (HTTPS profile available in local development). The API is the host and composes Application/Infrastructure in process; there is no internal API→Backend HTTP hop. Neither DTOs nor Google's Video resource are EF entities. `MelodyFlow.Contracts` holds versioned public DTOs shared at build time; HTTP remains the runtime transport. This local phase has no user authentication.

## Proposed public endpoints

| Method | Route | Request DTO | Response | Notes |
|---|---|---|---|---|
| GET | `/api/health` | — | `200` / `503 ProblemDetails` | PostgreSQL connectivity; does not reveal connection details |
| POST | `/api/v1/projects` | `CreateMusicProjectRequest` | `201 ProjectResponse` | Implemented; title, genre, mood?, duration |
| GET | `/api/v1/projects/{id}` | — | `200 ProjectResponse` / `404 ProblemDetails` | Implemented |
| GET | `/api/v1/projects?page=1&pageSize=20` | Paging query | `200 PagedResponse<ProjectResponse>` | Implemented; page size 1–100 and unsafe offsets return `400` |
| POST | `/api/v1/generation-jobs` | `CreateGenerationJobRequest` + optional `Idempotency-Key` header | `202 GenerationJobResponse`; `200` for an identical replay | Implemented; durable PostgreSQL insert before acceptance |
| GET | `/api/v1/generation-jobs/{id}` | — | `200 GenerationJobResponse` / `404 ProblemDetails` | Implemented |
| POST | `/api/v1/generation-jobs/{id}/cancellation` | — | `200` if cancelled immediately; `202` while Worker stops; `409` if terminal | Implemented |
| POST | `/api/v1/visual-assets/import` | Multipart upload | `201 VisualAssetResponse` | Later phase; secure file handling |
| POST | `/api/v1/rendered-videos/{id}/publications` | YouTube publication request | `202` | Publishing phase |

### Request example

```json
{"title":"Late Night Lofi","genre":"lofi","mood":"calm","targetDurationSeconds":3600}
```

### Create a music job

```json
{
  "musicProjectId": "b130a72a-9fd1-46f0-a552-c0cebbcfdd37",
  "jobType": "Music",
  "requestedParameters": { "variationSeed": 12345 }
}
```

Send `Idempotency-Key: <stable-key>` on scheduled n8n calls. The key is optional and unique within a project and job type. Replaying the same key with equivalent JSON returns the existing job with 200; changing its parameters returns 409. A new job returns 202 only after the PostgreSQL row commits and includes a `Location` header for status polling.

Validation failures return `ValidationProblemDetails`; missing projects and jobs return 404, key conflicts and invalid terminal cancellation return 409, and rate limits return Problem Details with 429. Health uses a sanitized 503 when PostgreSQL is unreachable. The API is configured for the local Web App origin and applies a per-client-IP limit of 120 requests/minute. Only `Music` jobs are currently accepted. The Worker queue lifecycle exists, but no music/audio handler is registered, so jobs remain `Queued` until that pipeline is implemented.

## Contract rules

`CreateMusicProjectRequest` intentionally omits Id/Status/CreatedAt; server determines these. Do not expose database entities as HTTP response schemas or pass unrestricted client-side StorageKey. Validate all public input even when Blazor performs client-side checks.

## Tests

Tests cover project request validation, create/list/get, durable job acceptance, canonical idempotency, changed-payload conflict, queued cancellation, 404, paging, CORS and 429 rate limiting. PostgreSQL queue concurrency and lease recovery still need a live PostgreSQL integration test. The local API intentionally has no unauthorized-access test because user authentication is not enabled.
