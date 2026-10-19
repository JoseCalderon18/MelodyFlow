# MelodyFlow.WebApp

> **Status:** Present in the solution as the Blazor starter app; it is not yet connected to the API. The API project and generation-job endpoints are available to local clients such as n8n.

## Template / runtime

**Blazor Web App** (.NET 10 where applicable). Independent deployment.

## Responsibility

User-facing Blazor interface; create projects, check status, upload visual clips, approve publishing.

## Planned files and features

The current template has no MelodyFlow API client or project-management screens. A typed API client, forms, upload progress and dashboards remain planned. The API contract is documented for local n8n integration.

## Must not

No direct DB access or embedded secret API keys in UI.

## Dependencies / collaboration

See [global architecture](../docs/ARCHITECTURE.md) and [API contracts](../docs/API-CONTRACTS.md). The WebApp will call the API over HTTP; it must not reference Infrastructure or access PostgreSQL directly. Local development CORS currently allows `http://localhost:5073` and `https://localhost:7277`.

## Implementation order

Inventory actual code → agree interface/schema → implement minimal vertical slice → tests → update docs and review diff. Consult `../AGENTS.md` and appropriate `.agents/skills` before changes.

## Definition of done

Project builds; relevant tests pass; dependency boundaries are respected; secrets absent from source; documentation matches behavior; source and migration diffs reviewed.
