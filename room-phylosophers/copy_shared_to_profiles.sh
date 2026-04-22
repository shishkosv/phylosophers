#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BASE="${BASE:-$SCRIPT_DIR}"

copy_if_exists() {
  local src="$1"
  local dst="$2"
  if [ -f "$src" ]; then
    mkdir -p "$(dirname "$dst")"
    cp "$src" "$dst"
    echo "copied: $src -> $dst"
  fi
}

copy_dir_if_exists() {
  local src="$1"
  local dst="$2"
  if [ -d "$src" ]; then
    mkdir -p "$dst"
    cp -R "$src"/. "$dst"/
    echo "copied dir: $src -> $dst"
  fi
}

copy_role_back() {
  local role="$1"
  local dstroot="$2"
  local src="$BASE/$role"

  copy_if_exists "$src/SOUL.md" "$dstroot/SOUL.md"
  copy_if_exists "$src/AGENTS.md" "$dstroot/AGENTS.md"
  copy_if_exists "$src/IDENTITY.md" "$dstroot/IDENTITY.md"
  copy_if_exists "$src/USER.md" "$dstroot/USER.md"
  copy_if_exists "$src/HEARTBEAT.md" "$dstroot/HEARTBEAT.md"
  copy_if_exists "$src/TOOLS.md" "$dstroot/TOOLS.md"
  copy_if_exists "$src/assistant.yaml" "$dstroot/assistant.yaml"
  copy_dir_if_exists "$src/scripts" "$dstroot/scripts"
}

copy_role_back aristotle /home/sergiy_shyshko/.openclaw-arist/workspace
copy_role_back freud /home/sergiy_shyshko/.openclaw-freud/workspace
copy_role_back marcus /home/sergiy_shyshko/.openclaw-marcus/workspace
copy_role_back moderator /home/sergiy_shyshko/.openclaw-moderator/workspace
copy_role_back debatemoderator /home/sergiy_shyshko/.openclaw-debatemoderator/workspace

copy_if_exists "$BASE/shared-routing/philosophers-room.yaml" /home/sergiy_shyshko/.openclaw/workspace/routing/philosophers-room.yaml
copy_if_exists "$BASE/shared-routing/main-USER.md" /home/sergiy_shyshko/.openclaw/workspace/USER.md
copy_if_exists "$BASE/shared-routing/main-HEARTBEAT.md" /home/sergiy_shyshko/.openclaw/workspace/HEARTBEAT.md
copy_if_exists "$BASE/shared-routing/main-AGENTS.md" /home/sergiy_shyshko/.openclaw/workspace/AGENTS.md
copy_if_exists "$BASE/shared-routing/main-SOUL.md" /home/sergiy_shyshko/.openclaw/workspace/SOUL.md
copy_if_exists "$BASE/shared-routing/main-IDENTITY.md" /home/sergiy_shyshko/.openclaw/workspace/IDENTITY.md
copy_if_exists "$BASE/shared-routing/main-assistant.yaml" /home/sergiy_shyshko/.openclaw/workspace/assistant.yaml

echo "done: copied shared folder back to profiles"
