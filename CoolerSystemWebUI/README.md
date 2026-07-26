# CoolerSystemWebUI

ラズパイ (RPi) 上の **AEHAFmtSender** API を、外部から操作するための Web アプリ。
RPi とは別のサーバーで動かし、外部公開は Cloudflare Tunnel、認証は Cloudflare Access に任せる。

```
ブラウザ ──HTTPS──> Cloudflare (Tunnel + Access)
                        │  認証はここで完結する
                        ▼
              [ 別サーバー ] cloudflared ──> ASP.NET (127.0.0.1:5080)
                                              ├─ /            Svelte SPA (wwwroot)
                                              └─ /api/{path}  RPi へそのまま中継
                                                      │
                                                      ▼  LAN / VPN
                                         [ RPi ] AEHAFmtSender (:5195) ──> LIRC / irsend
```

- **フロント**: Svelte 5 (runes) + Vite + TypeScript (`web/`)
- **バックエンド**: ASP.NET Core 10 Minimal API (`server/CoolerSystemWebApi/`)
  受け取ったリクエストを RPi へ素通しするだけで、業務ロジックは持たない。
- **認証コードは無し**。Cloudflare Access の内側に置くことが前提。

## 構成

| パス | 役割 |
| --- | --- |
| `web/` | Svelte SPA。ビルド成果物は `server/CoolerSystemWebApi/wwwroot/` に出力される |
| `server/CoolerSystemWebApi/RpiProxy.cs` | `/api/{**path}` を RPi の同名パスへ中継 |
| `server/CoolerSystemWebApi/RpiProxyOptions.cs` | 中継先 URL・タイムアウト・許可パス |
| `deploy/` | systemd unit、cloudflared 設定例、発行スクリプト |

SPA と API を**同一オリジン**で配信するので CORS 設定は不要、Cloudflare Tunnel も 1 ポート
(`127.0.0.1:5080`) だけ向ければよい。

## 中継しているエンドポイント

`/api/*` は AEHAFmtSender の同名パスにそのまま届く (ボディ・クエリ無変換)。

| ブラウザ | RPi | 用途 |
| --- | --- | --- |
| `GET /api/acget` | `GET /acget` | エアコン現在状態 |
| `POST /api/apiac` | `POST /apiac` | エアコン設定を送信 (IR 送信) |
| `POST /api/simplecode` | `POST /simplecode` | サーキュレーターの単発 IR |
| `GET/POST /api/circulatorconfig` | 同 | サーキュレーター IR 設定 |
| `GET/POST /api/automationconfig` | 同 | 電源連動などの自動化設定 |
| `GET /api/sensordata/latest` | `GET /sensordata/latest` | 直近の環境センサー値 (温度/湿度/気圧/CO2) |
| `GET /api/sensordata/history` | `GET /sensordata/history` | 環境センサーの履歴 (`?hours=24&limit=500`) |

センサー値の書き込み (`POST /sensordata`) は ESP32 (`SensorNode/`) が RPi の AEHAFmtSender へ
LAN経由で直接送信するため、この中継には含まれない (詳細は `SensorNode/README.md`)。

`RpiProxy:AllowedRoutes` に載っていないメソッド/パスは 404 で弾く (空配列にすると全通し)。
JSON の enum は AEHAFmtSender と同じ**数値**でやり取りする (`web/src/lib/types.ts` に定義)。

## 開発

前提: Node.js 20+ / .NET 10 SDK

```bash
# 1) バックエンド (RPi の実機 or 検証機を向ける)
cd server/CoolerSystemWebApi
dotnet run -- --RpiProxy:BaseUrl=http://<rpi-host>:5195     # http://localhost:5080

# 2) フロント (別ターミナル) — /api は 5080 にプロキシされる
cd web
npm install
npm run dev                                                  # http://localhost:5173
```

`npm run check` で Svelte + TypeScript の型チェック。

## デプロイ

```bash
./deploy/publish.sh                 # web をビルド → wwwroot に入れて dotnet publish
rsync -a --delete publish/ user@server:/opt/coolersystemwebui/
sudo cp deploy/coolersystemwebui.service /etc/systemd/system/
sudo systemctl daemon-reload && sudo systemctl enable --now coolersystemwebui
```

`coolersystemwebui.service` の `RpiProxy__BaseUrl` を RPi の IP に合わせて書き換えること。
Cloudflare Tunnel 側は `deploy/cloudflared-config.example.yml` を参照 (ingress を
`http://127.0.0.1:5080` に向けるだけ)。

### RPi 側で必要な変更

`deploy/aehafmtsender.service` (リポジトリ直下) は既定で **`127.0.0.1:5195`** にしか
listen していないため、このままでは別サーバーから届かない。LAN 経由で叩くなら RPi 側を
`ASPNETCORE_URLS=http://0.0.0.0:5195` に変更する。RPi を LAN に晒したくない場合は、
Web サーバー ↔ RPi 間を VPN (Tailscale 等) か SSH ポートフォワードで繋ぎ、
`RpiProxy__BaseUrl` にそのアドレスを指定する。

## 動作確認済みの範囲

- `/api/*` の中継 (状態取得 → 送信 → 再取得の往復、ボディ素通し)、許可外パスの 404、
  未許可メソッドの 405、RPi 不達時の 502 / タイムアウト時の 504
- SPA の 4 タブ (エアコン / サーキュレーター / 自動化 / センサー) の描画と、取得した状態の反映

実機の RPi (LIRC / irsend) には未接続でのテスト。実機接続時は `RpiProxy:BaseUrl` を実 IP に。
