# VINAN

VINAN (Virtual Intelligent Neural Assistant) is a secure, voice-first personal AI operating layer. This repository contains a working private control center and .NET API with owner authentication, encrypted personal data, streamed contextual conversation, adaptive memory, real productivity and information tools, approval-gated actions, an audit trail, and a provider-neutral intelligence layer.

## Run VINAN

Requirements: [.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) or newer.

```bash
dotnet run --project src/Vinan.Api/Vinan.Api.csproj --urls http://127.0.0.1:5017
```

Open [http://127.0.0.1:5017](http://127.0.0.1:5017).

On first launch, create the VINAN owner with a passphrase of at least 12 characters. Each browser is enrolled as a device and can later be revoked from **Permissions > Enrolled Devices**.

In VS Code, open this repository, reload the window once after installing the recommended extensions, and press `F5`. The included `VINAN: Run and Debug` profile starts the app at [http://127.0.0.1:5019](http://127.0.0.1:5019).

## What Works Today

- Streamed multi-turn conversation with saved context and local fallback
- Owner passphrase setup, sign-in, lock, and per-browser device enrollment
- Revocable device access with protected API routes
- Application-level encryption for personal database fields
- Versioned database migrations, including legacy local-database adoption
- SQLite-backed approved memory that survives restarts
- Relevance-ranked memory context for each model request
- Saved conversation sessions that can be reopened
- Reminder creation and completion
- Encrypted persistent notes and priority-aware tasks
- Live current weather and three-day forecasts through Open-Meteo
- Local calculator, clock, task optimizer, and tool execution receipts
- Tool registry with readiness, provider, and permission visibility
- Voice input where the browser supports speech recognition
- Permission visibility for connected tools
- Approval queue for sensitive and high-risk actions
- Audit history and local data export
- Optional GPT-5.6 Responses API reasoning, current web search, and automatic local fallback
- Owner-only AI settings with encrypted API-key storage and removal
- Automated tests for safety, streaming, context, tools, encryption, persistence, and provider isolation
- API documentation at `/swagger` in development

## Optional AI Provider

VINAN's deterministic tools work without a cloud model. For advanced reasoning and current web research, open **Intelligence** and connect an OpenAI API key. VINAN encrypts the saved credential before writing it to SQLite and never stores it in browser storage.

The environment-variable path is also supported:

```bash
export OPENAI_API_KEY="your-key"
dotnet run --project src/Vinan.Api/Vinan.Api.csproj --urls http://127.0.0.1:5017
```

The default flagship model is `gpt-5.6-sol`, with medium reasoning and response verbosity. Select Sol, Terra, or Luna in the control center, or override the default with `Models__Model`. Never commit an API key to this repository.

Deterministic VINAN rules process memory, notes, tasks, reminders, weather, calculations, optimization, and Level 3/4 actions before the model layer. High-risk requests never reach the model.

## Test

```bash
dotnet test Vinan.sln
```

## Safety Model

Every capability follows the same path:

```text
Intent -> Permission Check -> Risk Level -> Confirmation if Needed -> Audit Event
```

Level 1 requests are informational. Level 2 changes local state. Level 3 prepares external actions and waits for confirmation. Level 4 covers financial, production, security, and physical-world actions and requires strong confirmation.

## Project Shape

```text
src/Vinan.Api/          ASP.NET Core API and control-center web app
tests/Vinan.Api.Tests/  Safety, intent, persistence, and routing tests
docs/VINAN_BUILD_PLAN.md Product and engineering sequence
```

Local data is stored under `src/Vinan.Api/.data` by default. Change it with `Storage__DataDirectory=/absolute/path`. Personal text and identity fields are encrypted at the application boundary, and the database, data-protection key ring, and data directory are restricted to the current operating-system user on Unix-like systems.

The local key ring is intentionally portable with the data directory and is not encrypted by an OS keychain. A remote deployment must protect it with a managed key service such as Azure Key Vault and must use HTTPS.

See [the build plan](docs/VINAN_BUILD_PLAN.md) and [architecture guide](docs/ARCHITECTURE.md) for the staged roadmap and current trust boundaries.
