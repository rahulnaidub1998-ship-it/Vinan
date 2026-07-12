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

## Completed Milestone: Trusted Personal Gateway

1. Owner authentication and device enrollment.
2. Application-level encryption for personal records.
3. Database migrations and legacy plaintext upgrade.

## Next Milestone: Live Intelligence and Tools

1. SignalR response streaming and richer conversation controls.
2. Tool registry with scoped credentials and revocable grants.
3. Calendar, notes, weather, and task connectors in read/prepare modes.
4. Encrypted backup and restore with owner-controlled recovery.
5. Structured telemetry that excludes personal content by default.

## Build Rule

Every capability must pass through:

```text
Intent -> Permission Check -> Risk Level -> Confirmation if Needed -> Audit Event
```

Models may suggest and explain. Only VINAN's deterministic tool layer may authorize or execute an external action.
