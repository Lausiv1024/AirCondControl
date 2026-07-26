#!/usr/bin/env bash
# Svelte をビルドして ASP.NET の wwwroot に入れ、self-contained でない発行物を publish/ に作る。
# 配置先サーバに .NET 10 ランタイム (ASP.NET Core Runtime) が必要。
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RID="${1:-linux-x64}"
OUT="$ROOT/publish"

echo "==> フロントエンドをビルド (出力先: server/CoolerSystemWebApi/wwwroot)"
cd "$ROOT/web"
npm ci
npm run build

echo "==> バックエンドを発行 ($RID)"
cd "$ROOT/server/CoolerSystemWebApi"
dotnet publish -c Release -r "$RID" --self-contained false -o "$OUT"

echo
echo "完了: $OUT"
echo "配置例:"
echo "  rsync -a --delete $OUT/ user@server:/opt/coolersystemwebui/"
echo "  sudo cp $ROOT/deploy/coolersystemwebui.service /etc/systemd/system/"
echo "  sudo systemctl daemon-reload && sudo systemctl enable --now coolersystemwebui"
