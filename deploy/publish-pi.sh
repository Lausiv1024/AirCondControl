#!/usr/bin/env bash
#
# ラズパイ(linux-arm64)向けに AEHAFmtSender(API) と CoolerSystemUI(キオスクUI) を
# self-contained で publish する。生成物は artifacts/ 以下に出力される。
#
# 使い方:  ./deploy/publish-pi.sh
#
set -euo pipefail

RID="${RID:-linux-arm64}"
CONFIG="${CONFIG:-Release}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

API_CSPROJ="$ROOT/AEHAFmtSender/AEHAFmtSender/AEHAFmtSender/AEHAFmtSender.csproj"
UI_CSPROJ="$ROOT/CoolerSystemUI/CoolerSystemUI/CoolerSystemUI.csproj"

OUT_API="$ROOT/artifacts/aehafmtsender"
OUT_UI="$ROOT/artifacts/coolersystemui"

echo "==> Publish API  ($RID, $CONFIG)"
dotnet publish "$API_CSPROJ" -c "$CONFIG" -r "$RID" --self-contained true -o "$OUT_API"

echo "==> Publish UI   ($RID, $CONFIG)"
dotnet publish "$UI_CSPROJ" -c "$CONFIG" -r "$RID" --self-contained true -o "$OUT_UI"

echo
echo "完了:"
echo "  API: $OUT_API"
echo "  UI : $OUT_UI"
echo
echo "次の手順は deploy/README.md を参照してください。"
