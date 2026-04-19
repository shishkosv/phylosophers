#!/usr/bin/env python3
import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    print('Missing dependency: pyyaml', file=sys.stderr)
    sys.exit(1)


def usage():
    print('Usage: python3 scripts/generate_systemd_service.py <agent-workspace>', file=sys.stderr)
    sys.exit(1)


def main():
    if len(sys.argv) < 2:
        usage()

    workspace = Path(sys.argv[1]).expanduser().resolve()
    assistant_yaml = workspace / 'assistant.yaml'
    if not assistant_yaml.exists():
        print(f'Missing assistant.yaml in {workspace}', file=sys.stderr)
        sys.exit(1)

    data = yaml.safe_load(assistant_yaml.read_text()) or {}
    runtime = data.get('runtime', {})

    profile = runtime.get('profile')
    port = runtime.get('gatewayPort')
    service_name = runtime.get('serviceName')

    if not profile or not port or not service_name:
        print('assistant.yaml runtime must include profile, gatewayPort, and serviceName', file=sys.stderr)
        sys.exit(1)

    home = Path.home()
    unit_path = home / '.config/systemd/user' / service_name

    content = f'''[Unit]
Description=OpenClaw Gateway ({profile})
After=network-online.target
Wants=network-online.target
StartLimitBurst=5
StartLimitIntervalSec=60

[Service]
ExecStart=/usr/bin/node {home}/.npm-global/lib/node_modules/openclaw/dist/index.js --profile {profile} gateway run --port {port}
Restart=always
RestartSec=5
RestartPreventExitStatus=78
TimeoutStopSec=30
TimeoutStartSec=30
SuccessExitStatus=0 143
KillMode=control-group
Environment=HOME={home}
Environment=TMPDIR=/tmp
Environment=PATH=/usr/bin:{home}/.local/bin:{home}/.npm-global/bin:{home}/bin:{home}/.volta/bin:{home}/.asdf/shims:{home}/.bun/bin:{home}/.nvm/current/bin:{home}/.fnm/current/bin:{home}/.local/share/pnpm:/usr/local/bin:/bin
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

    print(f'Wrote systemd unit: {unit_path}')
    print('Next commands:')
    print('  systemctl --user daemon-reload')
    print(f'  systemctl --user enable --now {service_name}')
    print(f'  systemctl --user status {service_name} --no-pager')


if __name__ == '__main__':
    main()
