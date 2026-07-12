# VINAN Architecture

## Request Path

```text
Browser / future device clients
              |
              v
        ASP.NET Core gateway
              |
              v
   Deterministic intent + risk routing
       |                    |
       v                    v
Local tools and state   Model router (Level 1 only)
       |                    |
       v                    +-- OpenAI when configured
 SQLite + audit trail       +-- Local fallback
```

## Trust Boundaries

- Memory and reminders are written only after an explicit matching request.
- Level 3 external actions are prepared and paused for confirmation.
- Level 4 financial, production, security, and physical actions require strong confirmation.
- Approving an action records intent; it does not imply execution when no authorized connector exists.
- High-risk action requests are resolved before the model router and are never sent to a model provider.
- Approved memories are included as context only for ordinary conversation and cannot override VINAN's safety instructions.

## Persistence

The local development database is SQLite at `src/Vinan.Api/.data/vinan.db`. It stores:

- approved memories
- reminders and completion state
- pending-action decisions
- audit events
- conversation sessions and messages

On Unix-like systems, VINAN restricts the default data directory and database file to the current OS user. This is access control, not encryption. Application-level encryption and managed keys are required before hosting VINAN remotely.

## Model Routing

`IAssistantModel` is the stable provider boundary. `ModelRouter` selects OpenAI only when configured and otherwise uses local intelligence. Provider failures fall back locally without bypassing the risk engine.

The OpenAI implementation uses the Responses API and aggregates only `output_text` content from assistant message items. The configured default is `gpt-5.6-terra`, and it can be changed without altering conversation or safety services.
