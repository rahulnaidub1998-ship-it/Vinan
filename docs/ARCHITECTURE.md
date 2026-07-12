# VINAN Architecture

## Request Path

```text
Browser / future device clients
              |
              v
 Owner cookie + enrolled device
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

- First-run owner setup is single-use and stores only a password hash.
- All personal APIs require an authenticated owner cookie tied to a non-revoked device.
- Unsafe API requests require VINAN's same-origin request header in addition to the strict cookie.
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
- owner identity and enrolled devices

Personal text, action summaries, audit descriptions, owner names, and device names are encrypted through ASP.NET Core Data Protection before SQLite writes them. Passwords use the ASP.NET Core Identity password hasher and are never stored reversibly. Legacy plaintext rows are upgraded after migrations run.

On Unix-like systems, VINAN restricts the data directory, database file, and persistent key ring to the current OS user. The key-ring files are not themselves encrypted by an OS keychain, so remote hosting requires HTTPS plus a managed key protector such as Azure Key Vault. `Storage__DataDirectory` relocates the database and key ring together.

## Model Routing

`IAssistantModel` is the stable provider boundary. `ModelRouter` selects OpenAI only when configured and otherwise uses local intelligence. Provider failures fall back locally without bypassing the risk engine.

The OpenAI implementation uses the Responses API and aggregates only `output_text` content from assistant message items. The configured default is `gpt-5.6-terra`, and it can be changed without altering conversation or safety services.
