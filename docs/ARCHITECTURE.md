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
 Planner + deterministic risk routing
       |                         |
       v                         v
Permission-aware tools      Model router (Level 1 only)
       |                         |
       +-- Memory / notes        +-- GPT-5.6 + web search
       +-- Tasks / reminders     +-- Local honest fallback
       +-- Weather / clock       |
       +-- Optimization          v
       v                    Streamed response events
Encrypted SQLite + audit
```

## Trust Boundaries

- First-run owner setup is single-use and stores only a password hash.
- All personal APIs require an authenticated owner cookie tied to a non-revoked device.
- Unsafe API requests require VINAN's same-origin request header in addition to the strict cookie.
- Memory, notes, tasks, and reminders are written only after an explicit matching request.
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
- private notes and priority-aware tasks
- encrypted AI provider credentials

Personal text, action summaries, audit descriptions, owner names, and device names are encrypted through ASP.NET Core Data Protection before SQLite writes them. Passwords use the ASP.NET Core Identity password hasher and are never stored reversibly. Legacy plaintext rows are upgraded after migrations run.

On Unix-like systems, VINAN restricts the data directory, database file, and persistent key ring to the current OS user. The key-ring files are not themselves encrypted by an OS keychain, so remote hosting requires HTTPS plus a managed key protector such as Azure Key Vault. `Storage__DataDirectory` relocates the database and key ring together.

## Intelligence And Streaming

`IAssistantModel` is the stable provider boundary. `ModelRouter` selects OpenAI only when configured and otherwise uses local intelligence. Provider failures before output fall back locally without bypassing the risk engine. Recent encrypted conversation turns and relevance-ranked approved memories are supplied as context.

The OpenAI implementation uses the Responses API with `store: false`, GPT-5.6 reasoning controls, optional built-in web search, a stable privacy-preserving safety identifier, and semantic output events. The API streams deltas to the browser as newline-delimited JSON and persists the final assistant message only after completion. The configured default is `gpt-5.6-sol`.

## Adaptive And Quantum Systems

`MemoryRetrievalService` creates local text vectors and ranks approved memories by similarity plus recency. It is the current adaptive retrieval baseline and can later be replaced or augmented by learned embeddings after representative owner data and retrieval evaluations exist.

`ITaskOptimizationEngine` separates optimization policy from its provider. The working provider is a deterministic classical engine that ranks due dates and owner priorities. Azure Quantum/Q# is represented in the tool registry as a restricted, disconnected research provider. It must beat the classical baseline on a suitable measured workload before VINAN may route an optimization job to simulation or quantum hardware.
