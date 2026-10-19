# MelodyFlowAPI

> **Status:** Present in the solution; API host, local protections, health check, v1 project endpoints and durable generation-job endpoints are implemented.

## Template / runtime

**ASP.NET Core Web API** (.NET 10). Public REST boundary and composition root; local launch profiles bind to localhost.

## Responsibility

Hosts public HTTP endpoints; composes Application and Infrastructure in process; validates DTOs; applies configured CORS, per-IP rate limiting and structured errors. No user authentication in this local phase. Not music synthesis.

## Planned files and features

`ProjectsController`, `GenerationJobsController`, `/api/health`, Contracts mapping, OpenAPI, and integration tests. Job creation is committed to PostgreSQL before 202; status and cancellation are available. Idempotency keys are scoped to project + job type.

## Must not

Do not write EF migrations or invoke FFmpeg in HTTP request. API does not directly hold secrets of external music API.

## Dependencies / collaboration

See [global architecture](../docs/ARCHITECTURE.md), [contracts](../docs/API-CONTRACTS.md) and [local setup](../docs/SETUP-DEPLOYMENT.md). API references Application, Contracts and Infrastructure and is the only application host. Set `ConnectionStrings:MelodyFlow` with User Secrets before startup. The API has no authentication in this local-only phase; do not expose it to public or shared networks. Music-job creation is available, but the Worker music handler remains to be implemented.

## Implementation order

Inventory actual code → agree interface/schema → implement minimal vertical slice → tests → update docs and review diff. Consult `../AGENTS.md` and appropriate `.agents/skills` before changes.

## Definition of done

Project builds; relevant tests pass; dependency boundaries are respected; secrets absent from source; documentation matches behavior; source and migration diffs reviewed.
