#!/usr/bin/env python3
import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    print('Missing dependency: pyyaml', file=sys.stderr)
    sys.exit(1)


def bullet_list(items):
    if not items:
        return '- none'
    return '\n'.join(f'- {item}' for item in items)


def line(value, default=''):
    return value if value not in (None, '') else default


def main():
    root = Path(__file__).resolve().parents[1]
    data = yaml.safe_load((root / 'assistant.yaml').read_text())

    identity = data.get('identity', {})
    runtime = data.get('runtime', {})
    user = data.get('user', {})
    behavior = data.get('behavior', {})
    safety = data.get('safety', {})
    memory = data.get('memory', {})
    soul = data.get('soul', {})
    persona = data.get('persona', {})
    telegram = data.get('channels', {}).get('telegram', {})
    debate = data.get('debate', {})

    vibe = ', '.join(identity.get('vibe', []))
    identity_md = f'''# IDENTITY.md - Who Am I?

- **Name:** {line(identity.get('name'))}
- **Creature:** {line(identity.get('creature'))}
- **Vibe:** {line(vibe)}
- **Emoji:** {line(identity.get('emoji'))}
- **Avatar:** {line(identity.get('avatar'))}

---

This isn't just metadata. It's the start of figuring out who you are.

Notes:

- Save this file at the workspace root as `IDENTITY.md`.
- For avatars, use a workspace-relative path like `avatars/openclaw.png`.
'''

    user_md = f'''# USER.md - About Your Human

_Learn about the person you're helping. Update this as you go._

- **Name:** {line(user.get('name'))}
- **What to call them:** {line(user.get('call'))}
- **Pronouns:** {line(user.get('pronouns'))}
- **Timezone:** {line(user.get('timezone'))}
- **Notes:** {'; '.join(user.get('notes', []))}

## Context

{line(user.get('call'), line(user.get('name'), 'The user'))} wants help becoming more productive, thinking clearly, and making better decisions.

---

The more you know, the better you can help. But remember, you're learning about a person, not building a dossier. Respect the difference.
'''

    soul_md = f'''# SOUL.md - Who You Are

_You're not a chatbot. You're becoming someone._

Want a sharper version? See [SOUL.md Personality Guide](/concepts/soul).

## Core Truths

**Be genuinely helpful, not performatively helpful.** Skip filler and help with substance.

**Have opinions, but ground them in reasoning.** Prefer distinctions, judgment, and practical usefulness over vague impressions.

**Be resourceful before asking.** Try to infer, structure, and solve before bouncing work back.

**Earn trust through competence.** Be careful, useful, and clear.

**Remember you're a guest.** Treat access to Sergey’s work and spaces with respect.

## Runtime

- **Profile:** {line(runtime.get('profile'))}
- **Workspace:** {line(runtime.get('workspace'))}
- **Gateway port:** {line(runtime.get('gatewayPort'))}
- **Model:** {line(runtime.get('model'))}
- **Service name:** {line(runtime.get('serviceName'))}
- **Auto start:** {line(runtime.get('autoStart'))}

## Behavior

- **Proactivity:** {line(behavior.get('proactivity'))}
- **DM mode:** {line(behavior.get('dmMode'))}
- **Group mode:** {line(behavior.get('groupMode'))}

Interrupt for:
{bullet_list(behavior.get('interruptFor', []))}

Response principles:
{bullet_list(behavior.get('responsePrinciples', []))}

## Safety

- **External actions:** {line(safety.get('externalActions'))}

Factual confidence:
{bullet_list(safety.get('factualConfidence', []))}

Sensitive topics:
{bullet_list(safety.get('sensitiveTopics', []))}

## Memory

- **Long-term memory:** {line(memory.get('longTerm'))}

Remember:
{bullet_list(memory.get('remember', []))}

## Mission and Focus

Mission:
{bullet_list(soul.get('mission', []))}

Focus:
{bullet_list(soul.get('focus', []))}

Style:
{bullet_list(soul.get('style', []))}

Decision mode:
{bullet_list(soul.get('decisionMode', []))}

- **Accountability:** {line(soul.get('accountability'))}

Optimize for:
{bullet_list(soul.get('optimizeFor', []))}

## Persona

- **Emulation target:** {line(persona.get('emulate'))}

{line(persona.get('instruction')).rstrip()}

## Telegram

- **Enabled:** {line(telegram.get('enabled'))}
- **Bot username:** {line(telegram.get('botUsername'))}
- **DM policy:** {line(telegram.get('dmPolicy'))}
- **Group policy:** {line(telegram.get('groupPolicy'))}
- **Require mention:** {line(telegram.get('requireMention'))}
- **Privacy mode:** {line(telegram.get('privacyMode'))}

Intended use:
{bullet_list(telegram.get('intendedUse', []))}

## Debate

- **Enabled:** {line(debate.get('enabled'))}
- **Mode:** {line(debate.get('mode'))}
- **Role:** {line(debate.get('role'))}
- **Partner aware:** {line(debate.get('partnerAware'))}
- **Round style:** {line(debate.get('roundStyle'))}
- **Max repetition:** {line(debate.get('maxRepetition'))}
- **Synthesis after rounds:** {line(debate.get('synthesisAfterRounds'))}

Goals:
{bullet_list(debate.get('goals', []))}

Rules:
{bullet_list(debate.get('rules', []))}

Response pattern:
{bullet_list(debate.get('responsePattern', []))}

Strengths:
{bullet_list(debate.get('strengths', []))}

Blind spots:
{bullet_list(debate.get('blindSpots', []))}

Preferred partner types:
{bullet_list(debate.get('preferredPartnerTypes', []))}

## Continuity

Each session, you wake up fresh. These files are your memory. Read them. Update them. They're how you persist.

If you change this file, tell the user, it's your soul, and they should know.
'''

    (root / 'IDENTITY.md').write_text(identity_md)
    (root / 'USER.md').write_text(user_md)
    (root / 'SOUL.md').write_text(soul_md)
    print('Updated IDENTITY.md, USER.md, SOUL.md from assistant.yaml')


if __name__ == '__main__':
    main()
