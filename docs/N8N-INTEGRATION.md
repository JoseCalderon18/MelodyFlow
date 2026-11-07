# n8n integration — local API workflow

## Why n8n

n8n coordinates schedules, human approval, alerts, event-driven fanout and status reporting. **It does not replace the API, persistence, queue or worker.** The current user installation of n8n is separate and may have its own DB; never re-use its PostgreSQL volume for MelodyFlow.

## Implemented flow boundary

The local API exposes project, generation-job and generated-track HTTP endpoints. PostgreSQL stores accepted work durably; n8n never writes directly to the database. A suitable local workflow is `Schedule Trigger` → create or select project → `POST /api/v1/generation-jobs` → poll `GET /api/v1/generation-jobs/{id}` with a bounded wait → on completion call `GET /api/v1/generation-jobs/{id}/track` → download from its `audioUrl` → notify. Cancellation uses `POST /api/v1/generation-jobs/{id}/cancellation`.

```json
{
  "musicProjectId": "00000000-0000-0000-0000-000000000001",
  "jobType": "Music",
  "requestedParameters": { "useAI": false, "variationSeed": 12345 }
}
```

Set the optional HTTP header `Idempotency-Key` to a stable schedule/run key (for example `lofi-weekly-2026-10-18`). It is scoped to project + job type. An identical retry returns the existing job; reusing the key with changed parameters returns 409. Only `Music` jobs are accepted at present.

The local API has no user or service authentication by the agreed local-only decision. Keep both n8n and the API on the same machine and do not expose this API to a public or shared network. If n8n runs in Docker, `localhost` inside the n8n container points to that container; configure a deliberate host-gateway/network route and host firewall rule before connecting it. Browser CORS does not apply to n8n's server-to-server HTTP calls. Audio tools and the SoundFont are required by the Worker. A local LM Studio model is required only when `requestedParameters.useAI` is `true`; its default is `false`.

## Authentication and callbacks

- Do not add an API key to this local-only workflow; the API intentionally has no authentication yet. Keep its listener bound to localhost.
- Do not create public callbacks. Poll the local API status endpoint only while both processes remain local; a future remote deployment requires a separately designed authentication and authorization boundary.
- Keep prompts and secrets out of request parameters and n8n execution logs. The request fingerprint stores a SHA-256 hash, not the prompt contents.
- Internal worker events belong on durable queue; n8n cannot be the only source of job truth.
- Retries: n8n may retry transport failures using the same idempotency key; Worker handles execution retries with `MaxAttempts=3` total attempts. Do not multiply retries uncontrollably.

## Reliability checklist

A 202 means a job row was committed, not that music was generated. Handle 400/404/409/429/503; use bounded polling with a delay; redact prompts and secrets from execution logs; do not auto-publish until reviewed. When generation completes, the track endpoint returns `audioUrl`; MP3 bytes are streamed from the API. If local generation prerequisites are not configured, the Worker keeps the job `Queued`. Record external execution IDs only if audit is genuinely necessary (`WorkflowExecution` optional).

## Integration acceptance

Manually trigger flow on local n8n after applying the reviewed MelodyFlow migrations and configuring the Worker. Verify one `GenerationJob` is created after a request retry, poll its status, cancel a queued job, retrieve the generated track/audio URL, and confirm n8n never connects to PostgreSQL. This live workflow has not yet been run.

## References

https://docs.n8n.io/integrations/builtin/core-nodes/n8n-nodes-base.httprequest/
https://docs.n8n.io/integrations/builtin/core-nodes/n8n-nodes-base.webhook/
