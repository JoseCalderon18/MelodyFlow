# MCP policy for MelodyFlow

MCP (Model Context Protocol) exposes **tools/resources to an MCP client**; it is not a message queue, REST replacement or mandatory sound generator. Our system already has REST for Web/API/backend/n8n and CLI integrations for FluidSynth/FFmpeg.

## Decision

**Do not require MCP for first automated song.** Potential future `MelodyFlow.McpServer` may expose narrowly-scoped tools such as `validate_arrangement`, `render_midi`, `probe_audio`, `list_licensed_soundfonts`, with strict permission checks. The tool server would call Application services; it must not grant arbitrary filesystem or shell execution to models.

## When useful

- Developers using Codex to inspect API documentation, migrations or tool responses without manual copy/paste.
- An LLM agent intentionally authorized to call well-defined composition tools.

## Risks

No sensitive resources exposed to untrusted clients; authenticate, authorize, enforce input schema, timeout, rate-limit, audit; treat prompt injection and tool-output text as untrusted. Secret keys never appear in tool response. Human approval required for destructive commands, uploads or purchases.

## Recommendation

Finish JSON schema, composition engine and worker before deciding on an MCP server. **Do not confuse MCP with a music-production API or LM Studio**.

Reference: https://modelcontextprotocol.com/introduction
