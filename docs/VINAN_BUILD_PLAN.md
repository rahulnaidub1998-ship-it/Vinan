# VINAN Build Plan

## Completed Foundation

- Integrated ASP.NET Core API and browser control center
- Durable SQLite memory, reminders, pending actions, audit events, and conversation history
- Permission levels and deterministic Level 1-4 risk classification
- Approval and denial recording without false execution claims
- Provider-neutral AI boundary with local fallback
- Optional OpenAI Responses API provider
- Voice input and spoken replies where supported by the browser
- Single-owner passphrase authentication and owner-only API boundary
- Enrolled browser devices with revocation and lock controls
- Encrypted personal fields with persistent local keys and legacy-data upgrade
- Versioned Entity Framework database migrations
- One-click VS Code build and debug profile
- Streamed contextual GPT-5.6 conversation with current web search
- Encrypted notes, priority tasks, live weather, and tool execution receipts
- Permission-aware tool registry and owner-managed AI credential
- Adaptive memory retrieval and provider-neutral optimization boundary
- GitHub Actions build and automated safety tests

## Current Contracts

```text
GET    /api/health
GET    /api/auth/status
POST   /api/auth/setup
POST   /api/auth/login
POST   /api/auth/logout
GET    /api/devices
POST   /api/devices/{id}/revoke
POST   /api/conversation/message
POST   /api/conversation/stream
GET    /api/conversations
GET    /api/conversations/{id}/messages
GET    /api/ai/status
PUT    /api/ai/provider
DELETE /api/ai/provider
GET    /api/tools
GET    /api/memory
POST   /api/memory
DELETE /api/memory/{id}
GET    /api/reminders
POST   /api/reminders
POST   /api/reminders/{id}/complete
GET    /api/notes
POST   /api/notes
DELETE /api/notes/{id}
GET    /api/tasks
POST   /api/tasks
POST   /api/tasks/{id}/complete
GET    /api/audit
GET    /api/permissions
GET    /api/actions
POST   /api/actions/{id}/approve
POST   /api/actions/{id}/deny
```

## Completed Milestone: Trusted Personal Gateway

1. Owner authentication and device enrollment.
2. Application-level encryption for personal records.
3. Database migrations and legacy plaintext upgrade.

## Completed Milestone: Live Intelligence and Tools

1. Real response streaming and multi-turn model context.
2. Tool registry with readiness and permission visibility.
3. Encrypted notes, tasks, live weather, adaptive memory, and task optimization.
4. Owner-managed encrypted AI credentials and built-in web search.

## Next Milestone: Connected Productivity

1. OAuth credential grants with explicit scopes and revocation.
2. Google and Microsoft calendar read/prepare connectors.
3. Gmail and Outlook search, summary, and draft connectors.
4. Local and cloud file ingestion with document question answering.
5. Encrypted backup and restore with owner-controlled recovery.
6. Structured telemetry that excludes personal content by default.
7. ML retrieval evaluations and optional learned embedding provider.
8. Azure Quantum simulation only for benchmarked optimization research.

## Build Rule

Every capability must pass through:

```text
Intent -> Permission Check -> Risk Level -> Confirmation if Needed -> Audit Event
```

Models may suggest and explain. Only VINAN's deterministic tool layer may authorize or execute an external action.
