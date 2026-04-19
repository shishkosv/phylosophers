#!/usr/bin/env python3
import json
import secrets
import shutil
import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    print('Missing dependency: pyyaml', file=sys.stderr)
    sys.exit(1)

ROOT = Path(__file__).resolve().parents[1]
PRESETS = ROOT / 'agent-presets'
TEMPLATE = ROOT / 'persona-template.yaml'
INVENTORY = ROOT / 'agents-inventory.yaml'
HOME = Path.home()
SYSTEMD_DIR = HOME / '.config/systemd/user'
OPENCLAW_MODULE = HOME / '.npm-global/lib/node_modules/openclaw/dist/index.js'
DEFAULT_ALLOW_FROM_SOURCE = HOME / '.openclaw/credentials/telegram-default-allowFrom.json'
DEFAULT_PAIRING_SOURCE = HOME / '.openclaw/credentials/telegram-pairing.json'
DEFAULT_AUTH_SOURCE = HOME / '.openclaw/agents/main/agent/auth-profiles.json'
DEFAULT_HEARTBEAT = """# HEARTBEAT.md Template

```markdown
# Keep this file empty (or with only comments) to skip heartbeat API calls.

# Add tasks below when you want the agent to check something periodically.
```
"""
DEFAULT_TOOLS = """# TOOLS.md - Local Notes

Skills define _how_ tools work. This file is for _your_ specifics — the stuff that's unique to your setup.

## What Goes Here

Things like:

- Camera names and locations
- SSH hosts and aliases
- Preferred voices for TTS
- Speaker/room names
- Device nicknames
- Anything environment-specific

## Examples

```markdown
### Cameras

- living-room → Main area, 180° wide angle
- front-door → Entrance, motion-triggered

### SSH

- home-server → 192.168.1.100, user: admin

### TTS

- Preferred voice: \"Nova\" (warm, slightly British)
- Default speaker: Kitchen HomePod
```

## Why Separate?

Skills are shared. Your setup is yours. Keeping them apart means you can update skills without losing your notes, and share skills without leaking your infrastructure.

---

Add whatever helps you do your job. This is your cheat sheet.
"""


def usage(exit_code=1, stream=None):
    stream = stream or (sys.stdout if exit_code == 0 else sys.stderr)
    print('Usage:', file=stream)
    print('  python3 scripts/create_agent.py <profile-root-or-workspace> [preset] [--from-auth-profile <profile>] [--from-auth-path <path>] [--bot-token <token>] [--allow-group <id>]...', file=stream)
    print('', file=stream)
    print('Examples:', file=stream)
    print('  python3 scripts/create_agent.py /home/sergiy_shyshko/.openclaw-socrates/workspace philosopher', file=stream)
    print('  python3 scripts/create_agent.py /home/sergiy_shyshko/.openclaw-jung freud --from-auth-profile arist --allow-group -1003963621579', file=stream)
    sys.exit(exit_code)


def load_yaml(path):
    if not path.exists():
        return {}
    return yaml.safe_load(path.read_text()) or {}


def save_yaml(path, data):
    path.write_text(yaml.safe_dump(data, sort_keys=False, allow_unicode=True))


def parse_args(argv):
    if len(argv) < 2:
        usage()
    if argv[1] in ('-h', '--help'):
        usage(exit_code=0)

    target = Path(argv[1]).expanduser()
    preset_name = None
    idx = 2
    if idx < len(argv) and not argv[idx].startswith('--'):
        preset_name = argv[idx]
        idx += 1

    options = {
        'from_auth_profile': 'main',
        'from_auth_path': None,
        'bot_token': None,
        'allow_groups': [],
        'bot_username': None,
        'write_service': True,
    }

    while idx < len(argv):
        arg = argv[idx]
        if arg == '--from-auth-profile':
            idx += 1
            options['from_auth_profile'] = argv[idx]
        elif arg == '--from-auth-path':
            idx += 1
            options['from_auth_path'] = Path(argv[idx]).expanduser()
        elif arg == '--bot-token':
            idx += 1
            options['bot_token'] = argv[idx]
        elif arg == '--bot-username':
            idx += 1
            options['bot_username'] = argv[idx]
        elif arg == '--allow-group':
            idx += 1
            options['allow_groups'].append(argv[idx])
        elif arg == '--no-service':
            options['write_service'] = False
        else:
            print(f'Unknown option: {arg}', file=sys.stderr)
            usage()
        idx += 1

    return target, preset_name, options


def normalize_target(target: Path):
    target = target.resolve()
    if target.name == 'workspace':
        profile_root = target.parent
        workspace = target
    else:
        profile_root = target
        workspace = profile_root / 'workspace'
    return profile_root, workspace


def infer_profile_from_workspace(workspace: Path) -> str:
    parent = workspace.parent.name
    if parent.startswith('.openclaw-'):
        return parent[len('.openclaw-'):]
    return workspace.name


def add_to_inventory(name, profile, workspace, bot, port, service, purpose, preset):
    inventory = load_yaml(INVENTORY)
    agents = inventory.setdefault('agents', [])

    existing = None
    for agent in agents:
        if agent.get('profile') == profile or agent.get('workspace') == workspace:
            existing = agent
            break

    record = {
        'name': name,
        'profile': profile,
        'workspace': workspace,
        'telegramBot': bot,
        'gatewayPort': port,
        'service': service,
        'purpose': purpose,
        'preset': preset or 'custom',
        'status': 'scaffolded',
    }

    if existing:
        existing.update(record)
    else:
        agents.append(record)

    save_yaml(INVENTORY, inventory)


def copy_if_exists(source: Path, dest: Path):
    if source.exists():
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, dest)
        return True
    return False


def ensure_file(path: Path, content: str):
    if not path.exists():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)


def auth_source_from_profile(profile: str) -> Path:
    return HOME / f'.openclaw-{profile}' / 'agents/main/agent/auth-profiles.json' if profile != 'main' else DEFAULT_AUTH_SOURCE


def allow_from_source_from_profile(profile: str) -> Path:
    return HOME / f'.openclaw-{profile}' / 'credentials/telegram-default-allowFrom.json' if profile != 'main' else DEFAULT_ALLOW_FROM_SOURCE


def pairing_source_from_profile(profile: str) -> Path:
    return HOME / f'.openclaw-{profile}' / 'credentials/telegram-pairing.json' if profile != 'main' else DEFAULT_PAIRING_SOURCE


def find_next_free_port(start=18790):
    inventory = load_yaml(INVENTORY)
    used = set()
    for agent in inventory.get('agents', []):
        port = agent.get('gatewayPort')
        if isinstance(port, int):
            used.add(port)
        elif isinstance(port, str) and port.isdigit():
            used.add(int(port))
    candidate = start
    while candidate in used:
        candidate += 1
    return candidate


def make_openclaw_config(profile_root: Path, workspace: Path, profile: str, port: int, bot_token: str | None, allow_groups: list[str]):
    token = secrets.token_hex(24)
    groups = {'*': {'requireMention': True}}
    for group in allow_groups:
        groups[group] = {'requireMention': False}

    config = {
        'gateway': {
            'mode': 'local',
            'port': port,
            'bind': 'loopback',
            'auth': {
                'mode': 'token',
                'token': token,
            },
            'tailscale': {
                'mode': 'off',
                'resetOnExit': False,
            },
        },
        'agents': {
            'defaults': {
                'workspace': str(workspace),
                'models': {
                    'openai-codex/gpt-5.4': {}
                },
                'model': {
                    'primary': 'openai-codex/gpt-5.4'
                },
            }
        },
        'channels': {
            'telegram': {
                'enabled': True,
                'groups': groups,
                'botToken': bot_token or 'replace-me-bot-token',
                'dmPolicy': 'pairing',
                'groupPolicy': 'allowlist' if allow_groups else 'open',
            }
        },
        'auth': {
            'profiles': {
                'openai-codex:shishkosv@gmail.com': {
                    'provider': 'openai-codex',
                    'mode': 'oauth',
                    'email': 'shishkosv@gmail.com',
                }
            }
        },
        'plugins': {
            'entries': {
                'openai': {
                    'enabled': True
                }
            }
        }
    }
    path = profile_root / 'openclaw.json'
    path.write_text(json.dumps(config, indent=2) + '\n')
    return token, path


def write_systemd_unit(profile: str, port: int, service_name: str):
    unit_path = SYSTEMD_DIR / service_name
    content = f'''[Unit]
Description=OpenClaw Gateway ({profile})
After=network-online.target
Wants=network-online.target
StartLimitBurst=5
StartLimitIntervalSec=60

[Service]
ExecStart=/usr/bin/node {OPENCLAW_MODULE} --profile {profile} gateway run --port {port}
Restart=always
RestartSec=5
RestartPreventExitStatus=78
TimeoutStopSec=30
TimeoutStartSec=30
SuccessExitStatus=0 143
KillMode=control-group
Environment=HOME={HOME}
Environment=TMPDIR=/tmp
Environment=PATH=/usr/bin:{HOME}/.local/bin:{HOME}/.npm-global/bin:{HOME}/bin:{HOME}/.volta/bin:{HOME}/.asdf/shims:{HOME}/.bun/bin:{HOME}/.nvm/current/bin:{HOME}/.fnm/current/bin:{HOME}/.local/share/pnpm:/usr/local/bin:/bin
Environment=OPENCLAW_GATEWAY_PORT={port}
Environment=OPENCLAW_SYSTEMD_UNIT={service_name}
Environment=OPENCLAW_SERVICE_MARKER=openclaw
Environment=OPENCLAW_SERVICE_KIND=gateway
Environment=OPENCLAW_SERVICE_VERSION=2026.4.15

[Install]
WantedBy=default.target
'''
    unit_path.parent.mkdir(parents=True, exist_ok=True)
    unit_path.write_text(content)
    return unit_path


def main():
    target, preset_name, options = parse_args(sys.argv)
    profile_root, workspace = normalize_target(target)

    workspace.mkdir(parents=True, exist_ok=True)
    (workspace / 'scripts').mkdir(parents=True, exist_ok=True)

    source_yaml = TEMPLATE
    if preset_name:
        candidate = PRESETS / f'{preset_name}.yaml'
        if not candidate.exists():
            print(f'Preset not found: {candidate}', file=sys.stderr)
            sys.exit(1)
        source_yaml = candidate

    shutil.copy2(source_yaml, workspace / 'assistant.yaml')
    shutil.copy2(ROOT / 'scripts' / 'sync_persona.py', workspace / 'scripts' / 'sync_persona.py')

    data = load_yaml(workspace / 'assistant.yaml')
    runtime = data.setdefault('runtime', {})
    identity = data.setdefault('identity', {})
    soul = data.setdefault('soul', {})
    channels = data.setdefault('channels', {})
    telegram = channels.setdefault('telegram', {})

    profile = infer_profile_from_workspace(workspace)
    runtime['profile'] = profile
    runtime['workspace'] = str(workspace)
    runtime['serviceName'] = f'openclaw-gateway-{profile}.service' if profile != 'main' else 'openclaw-gateway.service'
    runtime['gatewayPort'] = find_next_free_port(18789 if profile == 'main' else 18790)
    identity['name'] = identity.get('name') or profile.capitalize()
    if options['bot_username']:
        telegram['botUsername'] = options['bot_username']

    save_yaml(workspace / 'assistant.yaml', data)

    ensure_file(workspace / 'HEARTBEAT.md', DEFAULT_HEARTBEAT)
    ensure_file(workspace / 'TOOLS.md', DEFAULT_TOOLS)

    sync_script = workspace / 'scripts' / 'sync_persona.py'
    if sync_script.exists():
        import subprocess
        subprocess.run([sys.executable, str(sync_script)], cwd=workspace, check=False)

    runtime = load_yaml(workspace / 'assistant.yaml').get('runtime', {})
    port = int(runtime.get('gatewayPort', 18789))
    service_name = runtime.get('serviceName', f'openclaw-gateway-{profile}.service')

    token, config_path = make_openclaw_config(
        profile_root=profile_root,
        workspace=workspace,
        profile=profile,
        port=port,
        bot_token=options['bot_token'],
        allow_groups=options['allow_groups'],
    )

    auth_source = options['from_auth_path'] or auth_source_from_profile(options['from_auth_profile'])
    auth_dest = profile_root / 'agents/main/agent/auth-profiles.json'
    auth_copied = copy_if_exists(auth_source, auth_dest)

    allow_from_source = allow_from_source_from_profile(options['from_auth_profile'])
    allow_from_dest = profile_root / 'credentials/telegram-default-allowFrom.json'
    allow_from_copied = copy_if_exists(allow_from_source, allow_from_dest)

    pairing_source = pairing_source_from_profile(options['from_auth_profile'])
    pairing_dest = profile_root / 'credentials/telegram-pairing.json'
    pairing_copied = copy_if_exists(pairing_source, pairing_dest)
    if not pairing_copied:
        ensure_file(pairing_dest, '{\n  "version": 1,\n  "requests": []\n}\n')

    service_path = None
    if options['write_service']:
        service_path = write_systemd_unit(profile=profile, port=port, service_name=service_name)

    add_to_inventory(
        name=identity.get('name', profile),
        profile=profile,
        workspace=str(workspace),
        bot=telegram.get('botUsername', ''),
        port=port,
        service=service_name,
        purpose='; '.join(soul.get('mission', [])[:2]),
        preset=preset_name,
    )

    print(f'Created agent scaffold in {workspace}')
    print(f'Profile root: {profile_root}')
    print(f'OpenClaw config: {config_path}')
    print(f'Gateway token generated: {token}')
    print(f'Auth bootstrap copied: {auth_copied} ({auth_source})')
    print(f'Telegram allowFrom copied: {allow_from_copied} ({allow_from_source})')
    print(f'Telegram pairing file ready: {pairing_dest}')
    if service_path:
        print(f'Systemd unit written: {service_path}')
    print(f'Updated inventory: {INVENTORY}')
    print('Next steps:')
    print(f'1. Review {workspace / "assistant.yaml"}')
    print(f'2. Review {config_path}')
    if options['bot_token'] is None:
        print('3. Set channels.telegram.botToken in openclaw.json')
    if service_path:
        print('4. Run: systemctl --user daemon-reload')
        print(f'5. Run: systemctl --user enable --now {service_name}')
    else:
        print(f'4. Run: cd {profile_root} && openclaw gateway start')


if __name__ == '__main__':
    main()
