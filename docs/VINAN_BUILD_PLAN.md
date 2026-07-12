# VINAN Build Plan

## Completed Foundation

- Integrated ASP.NET Core API and browser control center
- Durable SQLite memory, reminders, pending actions, audit events, and conversation history
- Permission levels and deterministic Level 1-4 risk classification
- Approval and denial recording without false execution claims
- Provider-neutral AI boundary with local fallback
- Optional OpenAI Responses API provider
- Voice input and spoken replies where supported by the browser
- GitHub Actions build and automated safety tests

## Current Contracts

```text
GET    /api/health
POST   /api/conversation/message
GET    /api/conversations
GET    /api/conversations/{id}/messages
GET    /api/memory
POST   /api/memory
DELETE /api/memory/{id}
GET    /api/reminders
POST   /api/reminders
POST   /api/reminders/{id}/complete
GET    /api/audit
GET    /api/permissions
GET    /api/actions
POST   /api/actions/{id}/approve
POST   /api/actions/{id}/deny
```

## Next Milestone: Trusted Personal Gateway

1. Owner authentication and device enrollment.
2. Application-level encryption for personal records and secrets.
3. Database migrations and encrypted backup/export.
4. SignalR response streaming and richer conversation controls.
5. Tool registry with scoped credentials and revocable grants.
6. Calendar, notes, weather, and task connectors in read/prepare modes.
7. Structured telemetry that excludes personal content by default.

## Build Rule

Every capability must pass through:

```text
Intent -> Permission Check -> Risk Level -> Confirmation if Needed -> Audit Event
```

Models may suggest and explain. Only VINAN's deterministic tool layer may authorize or execute an external action.
