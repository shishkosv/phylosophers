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

copy_role() {
  local role="$1"
  local srcroot="$2"
  local dst="$BASE/$role"

  copy_if_exists "$srcroot/SOUL.md" "$dst/SOUL.md"
  copy_if_exists "$srcroot/AGENTS.md" "$dst/AGENTS.md"
  copy_if_exists "$srcroot/IDENTITY.md" "$dst/IDENTITY.md"
  copy_if_exists "$srcroot/USER.md" "$dst/USER.md"
  copy_if_exists "$srcroot/HEARTBEAT.md" "$dst/HEARTBEAT.md"
  copy_if_exists "$srcroot/TOOLS.md" "$dst/TOOLS.md"
  copy_if_exists "$srcroot/assistant.yaml" "$dst/assistant.yaml"
  copy_dir_if_exists "$srcroot/scripts" "$dst/scripts"
}

mkdir -p "$BASE"/{aristotle,freud,marcus,moderator,debatemoderator,shared-routing}

copy_role aristotle /home/sergiy_shyshko/.openclaw-arist/workspace
copy_role freud /home/sergiy_shyshko/.openclaw-freud/workspace
copy_role marcus /home/sergiy_shyshko/.openclaw-marcus/workspace
copy_role moderator /home/sergiy_shyshko/.openclaw-moderator/workspace
copy_role debatemoderator /home/sergiy_shyshko/.openclaw-debatemoderator/workspace

copy_if_exists /home/sergiy_shyshko/.openclaw/workspace/routing/philosophers-room.yaml "$BASE/shared-routing/philosophers-room.yaml"
copy_if_exists /home/sergiy_shyshko/.openclaw/workspace/USER.md "$BASE/shared-routing/main-USER.md"
copy_if_exists /home/sergiy_shyshko/.openclaw/workspace/HEARTBEAT.md "$BASE/shared-routing/main-HEARTBEAT.md"
copy_if_exists /home/sergiy_shyshko/.openclaw/workspace/AGENTS.md "$BASE/shared-routing/main-AGENTS.md"
copy_if_exists /home/sergiy_shyshko/.openclaw/workspace/SOUL.md "$BASE/shared-routing/main-SOUL.md"
copy_if_exists /home/sergiy_shyshko/.openclaw/workspace/IDENTITY.md "$BASE/shared-routing/main-IDENTITY.md"
copy_if_exists /home/sergiy_shyshko/.openclaw/workspace/assistant.yaml "$BASE/shared-routing/main-assistant.yaml"

echo "done: copied profiles to shared folder $BASE"
