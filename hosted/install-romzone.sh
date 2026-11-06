#!/bin/bash
set -euo pipefail
ROOT=/www/wwwroot/powss.xyz/modules
BASE="https://raw.githubusercontent.com/zjh0708/FlashToolbox/modules-host/hosted"
mkdir -p "$ROOT/rom-zone/1.0.1"
cd /tmp
curl -fsSL "$BASE/index.json" -o index.json
curl -fsSL "$BASE/rom-zone/manifest.json" -o rom-zone-manifest.json
curl -fsSL "$BASE/rom-zone/1.0.1/manifest.json" -o rom-zone-1.0.1-manifest.json
curl -fsSL "$BASE/rom-zone/1.0.1/module.zip.b64" -o module.zip.b64
base64 -d module.zip.b64 > module.zip
echo "22ed0c03e3621baa5b3d8fc2df94a9b33e4eeea780e5aa3922d1861ab16207d6  module.zip" | sha256sum -c -
install -m 644 index.json "$ROOT/index.json"
install -m 644 rom-zone-manifest.json "$ROOT/rom-zone/manifest.json"
install -m 644 rom-zone-1.0.1-manifest.json "$ROOT/rom-zone/1.0.1/manifest.json"
install -m 644 module.zip "$ROOT/rom-zone/1.0.1/module.zip"
ls -la "$ROOT/index.json" "$ROOT/rom-zone/1.0.1/"
echo DEPLOY_OK
