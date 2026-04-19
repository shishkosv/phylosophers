# Persona YAML Schema

Use `assistant.yaml` in each agent workspace as the source of truth for identity, behavior, channel settings, debate posture, and operations metadata.

## Goals

This schema is designed for running multiple agents with different personalities, bots, ports, and debate roles.
It supports:
- identity and soul generation
- Telegram setup notes
- runtime/service metadata
- debate-specific role differentiation
- future automation for agent creation

## Top-level sections

### `identity`
Visible agent identity.
- `name`
- `creature`
- `vibe` (list)
- `emoji`
- `avatar`

### `runtime`
Operational metadata for running the agent.
- `profile`: OpenClaw profile name
- `workspace`: absolute workspace path
- `gatewayPort`: gateway port for the profile/service
- `model`: default model id
- `serviceName`: custom systemd unit name if used
- `autoStart`: whether the agent should auto-start

### `user`
Information about the human the agent serves.
- `name`
- `call`
- `pronouns`
- `timezone`
- `notes` (list)

### `behavior`
Message handling and interruption behavior.
- `proactivity`
- `dmMode`
- `groupMode`
- `interruptFor` (list)
- `responsePrinciples` (list)

### `safety`
Operational and epistemic constraints.
- `externalActions`
- `factualConfidence` (list)
- `sensitiveTopics` (list)

### `memory`
Memory preferences.
- `longTerm`: true/false
- `remember` (list)

### `soul`
Core purpose and style.
- `mission` (list)
- `focus` (list)
- `style` (list)
- `decisionMode` (list)
- `accountability`
- `optimizeFor` (list)

### `persona`
Persona/emulation settings.
- `emulate`
- `instruction` (multiline)

### `channels.telegram`
Telegram-specific settings.
- `enabled`
- `botUsername`
- `dmPolicy`
- `groupPolicy`
- `requireMention`
- `privacyMode`
- `intendedUse` (list)

### `debate`
Settings for multi-agent debate and complementary reasoning.
- `enabled`
- `mode`
- `role`
- `goals` (list)
- `rules` (list)
- `responsePattern` (list)
- `strengths` (list)
- `blindSpots` (list)
- `partnerAware`
- `preferredPartnerTypes` (list)
- `roundStyle`
- `maxRepetition`
- `synthesisAfterRounds`

## Debate design recommendation

For agents meant to debate each other, give them clearly different epistemic roles.

Example:
- Philosopher: definitions, categories, logic, ethics, internal consistency
- Psychologist: behavior, motive, bias, evidence, context, emotional reality

Good debate agents should:
- steelman before critique
- avoid strawmanning
- avoid repeating the same point in new words
- identify the strongest unresolved disagreement
- move toward synthesis after a few rounds

## Suggested values

### Telegram
- `dmPolicy: pairing`
- `groupPolicy: open` for practical group usage
- `groupPolicy: allowlist` for stricter access control
- `requireMention: true` to prevent noisy unsolicited replies
- `privacyMode: off` when mention-based group behavior should work reliably

### Runtime
Use a unique port and unique service name per agent.
Example:
- Neo: `18789`
- Aristotle: `18790`

## Suggested workflow

1. Copy `persona-template.yaml` into a new agent workspace as `assistant.yaml`
2. Fill in identity, runtime, soul, debate role, and Telegram settings
3. Run the sync script in that workspace
4. Verify generated `IDENTITY.md`, `USER.md`, `SOUL.md`
5. If needed, create a custom systemd user service using `runtime.serviceName`

## Recommendation

Keep `assistant.yaml` as the source of truth.
If you edit generated markdown directly, the next sync may overwrite it.
