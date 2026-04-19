#!/usr/bin/env python3
import sys
from collections import defaultdict
from pathlib import Path

try:
    import yaml
except ImportError:
    print('Missing dependency: pyyaml', file=sys.stderr)
    sys.exit(1)

ROOT = Path(__file__).resolve().parents[1]
INVENTORY = ROOT / 'agents-inventory.yaml'


def load_inventory():
    if not INVENTORY.exists():
        print(f'Inventory not found: {INVENTORY}', file=sys.stderr)
        sys.exit(1)
    return yaml.safe_load(INVENTORY.read_text()) or {}


def collect_duplicates(agents, field):
    seen = defaultdict(list)
    for agent in agents:
        value = agent.get(field)
        if value in (None, ''):
            continue
        seen[value].append(agent.get('name', 'Unknown'))
    return {k: v for k, v in seen.items() if len(v) > 1}


def main():
    data = load_inventory()
    agents = data.get('agents', [])
    if not agents:
        print('No agents in inventory.')
        return

    checks = {
        'profile': 'Duplicate profiles',
        'workspace': 'Duplicate workspaces',
        'telegramBot': 'Duplicate Telegram bots',
        'gatewayPort': 'Duplicate gateway ports',
        'service': 'Duplicate services',
    }

    problems = []
    for field, label in checks.items():
        duplicates = collect_duplicates(agents, field)
        if duplicates:
            problems.append((label, duplicates))

    if not problems:
        print('Inventory validation OK. No duplicate conflicts found.')
        return

    print('Inventory validation found conflicts:')
    for label, duplicates in problems:
        print(f'\n{label}:')
        for value, names in duplicates.items():
            print(f'  {value}: {", ".join(names)}')

    sys.exit(1)


if __name__ == '__main__':
    main()
