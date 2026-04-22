# Orkestrator

Production-oriented C# orchestration skeleton for the philosophers room.

## What it does

- accepts one incoming room message
- stores room history in memory-backed JSON state
- asks moderator logic for first-speaker selection
- generates exactly one philosopher reply by default
- optionally adds one contrasting philosopher reply
- optionally adds a short moderator summary
- stops after enough value is produced

## Structure

- `Orchestration/`
  - `RoomOrchestrator.cs`
  - `ModeratorSelector.cs`
  - `ContrastPolicy.cs`
  - `RepetitionGuard.cs`
  - `RoomRules.cs`
- `Models/`
  - `RoomState.cs`
  - `RoomMessage.cs`
  - `RouteDecision.cs`
  - `AgentProfile.cs`
  - `OrchestrationTurnResult.cs`
- `Services/`
  - `IAgentInvoker.cs`
  - `OpenClawAgentInvoker.cs`
  - `ITelegramPublisher.cs`
  - `TelegramPublisher.cs`
  - `IRoomStateStore.cs`
  - `RoomStateStore.cs`
- `Config/`
  - `OrchestratorOptions.cs`

## State storage

State is stored in:
- `memory/room-state.json`

This is file-backed durable state inside the workspace memory area.

## Current integration state

The project now builds and runs on .NET 8.

Important OpenClaw note:
- the previously assumed direct gateway route was not reliable for this project boundary
- the C# app now hosts its own internal bridge endpoint and worker
- invokers call the local internal route, and the worker centralizes translation to the actual OpenClaw gateway call
- if no real session key is configured, the bridge can still use a prompt-echo fallback for smoke tests

## Build

```bash
cd /home/sergiy_shyshko/.openclaw/workspace/orkestrator
dotnet restore
dotnet build -c Release
```

## Run

```bash
dotnet run -- serve
```

Starts the internal bridge API on the configured local URL.

For a one-shot orchestration turn:

```bash
dotnet run -- "Why do people sabotage themselves even when they know better?"
```

## OpenClaw configuration

`appsettings.json` now supports:

```json
{
  "Orchestrator": {
    "OpenClaw": {
      "BaseUrl": "http://localhost:18789",
      "SessionKey": "",
      "EndpointPath": "/internal/openclaw/bridge/invoke",
      "BearerToken": "",
      "EnablePromptEchoFallback": true,
      "TimeoutSeconds": 60,
      "InternalBridge": {
        "Url": "http://127.0.0.1:5187",
        "RoutePath": "/internal/openclaw/bridge/invoke"
      }
    }
  }
}
```

Meaning:
- `SessionKey`: target OpenClaw session key used by the bridge worker when forwarding to OpenClaw
- `BearerToken`: optional bearer token if the downstream OpenClaw gateway requires auth
- `InternalBridge.Url`: local listener URL hosted by this C# project
- `InternalBridge.RoutePath`: internal route the invoker calls inside this project
- `EndpointPath`: retained as the default internal route path value for compatibility
- `EnablePromptEchoFallback`: keep `true` for local smoke tests, set `false` for strict production mode

Telegram configuration:
- `Telegram.BotToken`: real bot token used for `sendMessage`
- `Telegram.ChatId`: target Telegram chat id
- `Telegram.DisableWebPagePreview`: passed through to Telegram Bot API

## Production adjustments still recommended

Before deploying, I recommend these upgrades:

1. add stronger validation and auth around the internal bridge endpoint
2. harden moderator JSON parsing with a dedicated DTO and validation
3. add structured prompt builders per profile instead of the simple generic prompt composer
4. add inbound webhook controller or polling worker
5. add per-room lock to avoid concurrent turn collisions
6. add backup/rotation for `memory/room-state.json`
7. add automated tests for routing, contrast, repetition suppression, and bridge API behavior

## Design choices

- hard rules are enforced in C#, not only in prompts
- max 2 philosopher replies per user turn
- same philosopher cannot speak twice in a row
- second speaker must add contrast
- silence is preferred over redundancy
- moderator is internal selector first, not default visible participant

## Notes

- folder name intentionally follows your requested spelling: `orkestrator`
- this code uses workspace memory storage rather than database state
