#!/usr/bin/env bash
set -euo pipefail

systemctl --user daemon-reload
systemctl --user restart openclaw-gateway-moderator.service
systemctl --user restart openclaw-gateway-freud.service
systemctl --user restart openclaw-gateway-marcus.service
systemctl --user restart openclaw-gateway-arist.service

systemctl --user --no-pager --full status \
openclaw-gateway-moderator.service \
openclaw-gateway-freud.service \
openclaw-gateway-marcus.service \
openclaw-gateway-arist.service
