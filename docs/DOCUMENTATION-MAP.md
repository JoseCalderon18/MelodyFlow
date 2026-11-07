# Documentation map

| File | Source of truth for | NOT responsible for |
|---|---|---|
| `CONTROL-MELODYFLOW.md` | Current implementation status, local configuration checklist and recommended next steps | Detailed architecture, schema and API specifications |
| `README.md` | Project overview, quickstart and topology | Exhaustive schema |
| `AGENTS.md` | Agent behavior, permissions, review gates | Business field lists |
| `docs/ARCHITECTURE.md` | Processes, dependency rules and traffic flows | API property definitions |
| `docs/ENTITY-CATALOG.md` | Entity fields, relations, constraints and implementation status | Already-applied DB migrations |
| `docs/API-CONTRACTS.md` | HTTP DTOs/endpoints/versioning/error semantics and implemented/deferred status | Persistence schema |
| `docs/MUSIC-PIPELINE.md` | Deterministic-by-default MIDI generation, optional AI planning and audio pipeline | Video publishing policy |
| `docs/N8N-INTEGRATION.md` | Workflow triggers, callbacks, retry boundaries | Implementation of music engine |
| `docs/DATABASE-EFCORE.md` | PostgreSQL mapping, migrations and local setup | Secrets |
| `docs/TESTING.md` | Test plan and acceptance criteria | Claims tests already passed |
| `docs/THIRD-PARTY-NOTICES.md` | External tool/model/SoundFont license references and attribution | License of a user-downloaded model or binary without its source notice |
| `docs/MCP-GUIDE.md` | Optional tooling and security boundaries | Required infrastructure |
| `.agents/skills/*/SKILL.md` | HOW agents carry out a scoped task | Duplicate catalog definitions |

**Document precedence:** approved decision records and actual source/migrations take precedence over unverified drafts. If sources conflict, STOP and flag discrepancy before changes. Field requirements in the entity catalog are **proposals until approved**. Update affected docs in the same PR as implementation.
