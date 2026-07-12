# VINAN Build Plan

## Current Prototype

The current local prototype is a browser-based VINAN control center with:

- Conversation shell
- Approved memory storage
- Local reminders
- Local notes
- Approval queue for sensitive actions
- Permission model display
- Audit trail
- Memory export
- Browser speech recognition hook where supported

## Next Engineering Milestone

Build a .NET-centered foundation behind this interface:

1. ASP.NET Core API for conversations, memories, reminders, permissions, and audit events.
2. SQLite or PostgreSQL persistence for local development.
3. Model-provider abstraction so VINAN is not locked to one AI vendor.
4. Tool registry with risk levels and permission checks.
5. SignalR streaming endpoint for assistant responses.
6. Authentication placeholder that can grow into full device authorization.

## First Backend Contracts

```text
POST /api/conversation/message
GET  /api/memory
POST /api/memory
DELETE /api/memory/{id}
GET  /api/reminders
POST /api/reminders
GET  /api/audit
GET  /api/permissions
POST /api/actions/{id}/approve
POST /api/actions/{id}/deny
```

## Build Rule

Every new capability must pass through:

```text
Intent -> Permission Check -> Risk Level -> User Confirmation if Needed -> Audit Event
```
