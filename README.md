# VINAN

VINAN (Virtual Intelligent Neural Assistant) is a secure, voice-first personal AI operating layer. This repository contains the first working foundation: a browser control center and a .NET API that share memory, reminders, permissions, approval-gated actions, and an audit trail.

## Run VINAN

Requirements: [.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) or newer.

```bash
dotnet run --project src/Vinan.Api/Vinan.Api.csproj --urls http://127.0.0.1:5017
```

Open [http://127.0.0.1:5017](http://127.0.0.1:5017).

## What Works Today

- Conversation shell with local fallback
- Approved memory creation and deletion
- Reminder creation and completion
- Voice input where the browser supports speech recognition
- Permission visibility for connected tools
- Approval queue for sensitive and high-risk actions
- Audit history and local data export
- API documentation at `/swagger` in development

## Safety Model

Every capability follows the same path:

```text
Intent -> Permission Check -> Risk Level -> Confirmation if Needed -> Audit Event
```

Level 1 requests are informational. Level 2 changes local state. Level 3 prepares external actions and waits for confirmation. Level 4 covers financial, production, security, and physical-world actions and requires strong confirmation.

## Project Shape

```text
src/Vinan.Api/          ASP.NET Core API and control-center web app
docs/VINAN_BUILD_PLAN.md Product and engineering sequence
```

The current state is intentionally local and in-memory. The next milestone adds durable encrypted storage, tests around the risk engine, identity and device authorization, and a provider-neutral AI model gateway.

See [the build plan](docs/VINAN_BUILD_PLAN.md) for the staged roadmap.
