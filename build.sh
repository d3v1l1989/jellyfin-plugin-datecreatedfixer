#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PLUGIN_NAME="Jellyfin.Plugin.DateCreatedFixer"
DEPLOY_DIR="/opt/jellyfin/data/plugins/DateCreatedFixer"

echo "=== Building ${PLUGIN_NAME} ==="
cd "$SCRIPT_DIR"
dotnet publish "$SCRIPT_DIR/Jellyfin.Plugin.DateCreatedFixer.csproj" -c Release -o "$SCRIPT_DIR/bin/publish"

echo ""
echo "=== Deploying to ${DEPLOY_DIR} ==="
sudo mkdir -p "$DEPLOY_DIR"
sudo cp "$SCRIPT_DIR/bin/publish/${PLUGIN_NAME}.dll" "$DEPLOY_DIR/"
sudo cp "$SCRIPT_DIR/meta.json" "$DEPLOY_DIR/"

echo ""
echo "=== Deployed files ==="
ls -la "$DEPLOY_DIR/"

echo ""
echo "=== Done! Restart Jellyfin to load the plugin ==="
echo "  sudo systemctl restart jellyfin"
