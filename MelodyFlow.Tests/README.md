# MelodyFlow.Tests

> **Status:** Present in the solution; covers domain leases/retries, EF model metadata and project/job API contracts.

## Template / runtime

**xUnit Test Project** (.NET 10 where applicable). Shared library or workflow folder; not automatically a process.

## Responsibility

Tests for Domain, Application, HTTP, PostgreSQL, tools.

## Planned files and features

Domain validation and job lifecycle tests, Npgsql model/index metadata checks without a database connection, and API project/job validation, idempotency and cancellation tests using in-memory repositories.

## Must not

No claim that tests pass without running them.

## Dependencies / collaboration

See [global architecture](../docs/ARCHITECTURE.md) and [testing strategy](../docs/TESTING.md). The tests reference Domain, Application, Infrastructure and API. API integration tests replace persistence and do not need PostgreSQL. Live PostgreSQL concurrency and n8n workflow tests remain future work.

## Implementation order

Inventory actual code → agree interface/schema → implement minimal vertical slice → tests → update docs and review diff. Consult `../AGENTS.md` and appropriate `.agents/skills` before changes.

## Definition of done

Project builds; relevant tests pass; dependency boundaries are respected; secrets absent from source; documentation matches behavior; source and migration diffs reviewed.
