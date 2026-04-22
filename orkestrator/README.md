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

This environment does not have `dotnet` installed, so the code could not be compiled here.
The source tree is complete, but you must build and run it on a machine with .NET 8 SDK.

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

## Production adjustments still recommended

Before deploying, I recommend these upgrades:

1. replace `TelegramPublisher` placeholder with real Telegram Bot API publishing
2. replace `OpenClawAgentInvoker` endpoint assumptions with your actual OpenClaw session/message API
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
