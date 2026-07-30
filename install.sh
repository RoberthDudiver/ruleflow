#!/usr/bin/env bash
# RuleFlow — one-command installer for Linux (evaluation build).
#
#   curl -fsSL https://raw.githubusercontent.com/RoberthDudiver/ruleflow/main/install.sh | bash
#
# Options (env vars):
#   RULEFLOW_REPO=owner/repo   public releases repo (default RoberthDudiver/ruleflow)
#   RULEFLOW_HOME=/path         install dir (default ~/ruleflow)
#   RULEFLOW_PORT=8080          HTTP port
#   RULEFLOW_SERVICE=1          also install & start a systemd service (needs root)
set -euo pipefail

REPO="${RULEFLOW_REPO:-RoberthDudiver/ruleflow}"
DEST="${RULEFLOW_HOME:-$HOME/ruleflow}"
PORT="${RULEFLOW_PORT:-8080}"

echo "▶ Installing RuleFlow into $DEST"
mkdir -p "$DEST"

echo "▶ Finding the latest linux-x64 release in $REPO …"
URL=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" \
      | grep browser_download_url | grep 'linux-x64.tar.gz' | cut -d '"' -f4 | head -1)
[ -n "$URL" ] || { echo "✗ No linux-x64 asset found in $REPO releases."; exit 1; }

echo "▶ Downloading $URL"
curl -fsSL "$URL" -o /tmp/ruleflow.tar.gz
tar -xzf /tmp/ruleflow.tar.gz -C "$DEST" --strip-components=1
chmod +x "$DEST/Dudiver.RuleFlow.Server" 2>/dev/null || true
rm -f /tmp/ruleflow.tar.gz

if [ "${RULEFLOW_SERVICE:-0}" = "1" ]; then
  [ "$(id -u)" -eq 0 ] || { echo "✗ RULEFLOW_SERVICE=1 needs root (sudo)."; exit 1; }
  cat >/etc/systemd/system/ruleflow.service <<EOF
[Unit]
Description=RuleFlow
After=network.target
[Service]
WorkingDirectory=$DEST
ExecStart=$DEST/Dudiver.RuleFlow.Server
Environment=ASPNETCORE_URLS=http://0.0.0.0:$PORT
Restart=always
[Install]
WantedBy=multi-user.target
EOF
  systemctl daemon-reload
  systemctl enable --now ruleflow
  echo "✓ Service 'ruleflow' started on http://localhost:$PORT"
else
  echo "✓ Installed. Start it with:"
  echo "    cd \"$DEST\" && ASPNETCORE_URLS=http://0.0.0.0:$PORT ./Dudiver.RuleFlow.Server"
fi

echo "→ Open http://localhost:$PORT and complete the installation wizard."
