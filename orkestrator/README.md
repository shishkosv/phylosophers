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
- the previously assumed `/api/sessions/send` endpoint does not exist on the local gateway tested here
- `OpenClawAgentInvoker` was updated to use an explicit configurable HTTP endpoint instead of hard-coding a guessed route
- if no real endpoint is configured, the project can still use a prompt-echo fallback for smoke tests

## Build

```bash
cd /home/sergiy_shyshko/.openclaw/workspace/orkestrator
dotnet restore
dotnet build -c Release
```

## Run

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
      "EndpointPath": "",
      "BearerToken": "",
      "EnablePromptEchoFallback": true,
      "TimeoutSeconds": 60
    }
  }
}
```

Meaning:
- `EndpointPath`: real HTTP path for your own OpenClaw-facing adapter or gateway extension
- `SessionKey`: target OpenClaw session key
- `BearerToken`: optional bearer token if your adapter requires auth
- `EnablePromptEchoFallback`: keep `true` for local smoke tests, set `false` for strict production mode

## Production adjustments still recommended

Before deploying, I recommend these upgrades:

1. replace `TelegramPublisher` placeholder with real Telegram Bot API publishing
2. provide a real OpenClaw adapter endpoint and set `OpenClaw.EndpointPath` to it
3. harden moderator JSON parsing with a dedicated DTO and validation
4. add structured prompt builders per profile instead of the simple generic prompt composer
5. add inbound webhook controller or polling worker
6. add per-room lock to avoid concurrent turn collisions
7. add backup/rotation for `memory/room-state.json`
8. add automated tests for routing, contrast, and repetition suppression

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
