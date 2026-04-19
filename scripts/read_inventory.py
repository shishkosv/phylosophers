#!/usr/bin/env python3
import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    print('Missing dependency: pyyaml', file=sys.stderr)
    sys.exit(1)

ROOT = Path(__file__).resolve().parents[1]
INVENTORY = ROOT / 'agents-inventory.yaml'


def main():
    if not INVENTORY.exists():
        print(f'Inventory not found: {INVENTORY}', file=sys.stderr)
        sys.exit(1)

    data = yaml.safe_load(INVENTORY.read_text()) or {}
    agents = data.get('agents', [])

    if not agents:
        print('No agents in inventory.')
        return

    print('Agent Inventory')
    print('===============')
    for i, agent in enumerate(agents, 1):
        print(f"{i}. {agent.get('name', 'Unknown')}")
        print(f"   Profile:   {agent.get('profile', '')}")
        print(f"   Purpose:   {agent.get('purpose', '')}")
        print(f"   Workspace: {agent.get('workspace', '')}")
        print(f"   Telegram:  {agent.get('telegramBot', '')}")
        print(f"   Port:      {agent.get('gatewayPort', '')}")
        print(f"   Service:   {agent.get('service', '')}")
        print(f"   Preset:    {agent.get('preset', '')}")
        print(f"   Status:    {agent.get('status', '')}")
        print()


if __name__ == '__main__':
    main()
