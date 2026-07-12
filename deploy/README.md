# ラズパイ・キオスク デプロイ手順

AEHAFmtSender(エアコン制御 API)と CoolerSystemUI(Avalonia タッチ UI)を
同一のラズパイ上で動かし、UI を Linux DRM のキオスクモードで全画面表示する。

```
CoolerSystemUI (Avalonia / DRM, tty1 全画面)
      │ HTTP  http://127.0.0.1:5195
      ▼
AEHAFmtSender (ASP.NET Core API)  ──► LIRC(lircd) / irsend ──► IR LED
```

## 前提条件(ラズパイ側)

- 64bit OS（`linux-arm64`）。
- **LIRC が構成済み**で、以下の 2 つのリモートが登録されていること:
  - `aircond`（エアコン本体。API が `/etc/lirc/lircd.conf.d/aircond.conf` を生成・更新する）
  - `circulator`（サーキュレーター。`power` / `plus` / `minus` のキーを持つこと）
  - 確認: `irsend LIST circulator ""` でキー一覧が出る。
- DRM が使えること（`/dev/dri/card0` が存在）。X11 / Wayland は不要。
- キオスク実行用ユーザ（例 `cooler`）を作成し、`video` / `render` / `input` グループに所属させる:
  ```bash
  sudo useradd -r -s /usr/sbin/nologin cooler || true
  sudo usermod -aG video,render,input cooler
  ```

## 1. publish（開発機 or ラズパイ上で実行）

.NET 10 SDK が入った環境で:

```bash
./deploy/publish-pi.sh
# 生成物:
#   artifacts/aehafmtsender/   (API + Blazor WASM 静的ファイル)
#   artifacts/coolersystemui/  (Avalonia キオスク UI)
```

Blazor WASM のビルドには wasm ワークロードが必要（`dotnet workload install wasm-tools`）。

## 2. 配置(ラズパイ)

```bash
sudo mkdir -p /opt/aehafmtsender /opt/coolersystemui
sudo cp -r artifacts/aehafmtsender/*  /opt/aehafmtsender/
sudo cp -r artifacts/coolersystemui/* /opt/coolersystemui/
sudo chmod +x /opt/aehafmtsender/AEHAFmtSender /opt/coolersystemui/CoolerSystemUI
sudo chown -R cooler:cooler /opt/coolersystemui
```

接続先 URL を変えたい場合は `/opt/coolersystemui/appsettings.json` の `Api:BaseUrl`、
または systemd unit の `COOLER_API_BASEURL` を編集する。

## 2-1. バックライト消灯の権限付与(udev)

画面の自動消灯 / 手動消灯ボタンで**バックライトを実際に OFF**にするには、非 root の
`cooler` ユーザが sysfs のバックライトを操作できる必要がある。同梱の udev ルールを配置する:

```bash
sudo cp deploy/99-backlight.rules /etc/udev/rules.d/
sudo udevadm control --reload-rules
sudo udevadm trigger --subsystem-match=backlight
# 反映確認(video グループに g+w が付いていること):
ls -l /sys/class/backlight/*/bl_power
```

- この権限が無い場合でもアプリは落ちず、黒オーバーレイで画面を覆う「見かけ上の消灯」にフォールバックする
  (ただしバックライトは点いたままなので省電力・焼け防止効果は弱い)。
- アプリは消灯時に `brightness=0` と `bl_power=1` を両方書く(同ルールで両方に権限付与済み)。

### ラズパイ公式タッチディスプレイ(DSI)の注意

公式ディスプレイは DSI ボード上の I2C(アドレス `0x45`)経由でバックライトを制御するため、
sysfs のノード名がカーネル世代・機種で変わる:

- 旧 fkms ドライバ: `rpi_backlight`
- Bullseye+ の KMS(初代 7"): `10-0045`
- Touch Display 2: `6-0045` など(バス番号は環境で変動する `X-0045`)

アプリは `/sys/class/backlight/` を**自動検出**するので通常は設定不要。実際のノード名は次で確認できる:

```bash
ls /sys/class/backlight/
cat /sys/class/backlight/*/max_brightness   # 公式ディスプレイは 255
# 手動で消灯/点灯テスト:
echo 1 | sudo tee /sys/class/backlight/*/bl_power   # 消灯
echo 0 | sudo tee /sys/class/backlight/*/bl_power   # 点灯
```

複数の backlight デバイスが出る環境では `COOLER_BACKLIGHT=/sys/class/backlight/10-0045` のように明示する。

## 3. サービス登録

```bash
sudo cp deploy/aehafmtsender.service /etc/systemd/system/
sudo cp deploy/coolersystemui.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now aehafmtsender.service
sudo systemctl enable --now coolersystemui.service
```

起動後、`http://127.0.0.1:5195/acget` が応答し、tty1 に UI が全画面表示される。

```bash
# ログ確認
journalctl -u aehafmtsender -f
journalctl -u coolersystemui -f
```

## 設定一覧(CoolerSystemUI の環境変数)

| 変数 | 既定 | 説明 |
|------|------|------|
| `COOLER_DRM` | (未設定) | `1` で Linux DRM キオスク起動（`--drm` 引数でも可） |
| `COOLER_API_BASEURL` | `http://localhost:5195` | API 接続先。`appsettings.json` の `Api:BaseUrl` より優先 |
| `COOLER_DRI_CARD` | (自動選択) | DRM カードノード。例 `/dev/dri/card0` |
| `COOLER_DRM_SCALING` | `1.0` | 画面スケーリング倍率 |
| `COOLER_DRM_ROTATE` | `0` | 画面回転(度) `0/90/180/270`。パネルが上下逆付けなら `180`。`appsettings.json` の `Ui:Rotate` より優先 |
| `COOLER_BLANK_TIMEOUT` | `60` | 無操作で自動消灯するまでの秒数(焼け防止)。`0` で自動消灯なし。`appsettings.json` の `Ui:BlankTimeoutSeconds` より優先 |
| `COOLER_BACKLIGHT` | (自動検出) | バックライトの sysfs ディレクトリ。通常は未設定で自動検出。明示する場合の例 `/sys/class/backlight/10-0045`。`appsettings.json` の `Ui:BacklightPath` より優先 |
| `COOLER_AUTOSEND_DEBOUNCE_MS` | `800` | 操作が止まってから自動で `POST /apiac` するまでの待ち時間(ms)。`appsettings.json` の `Ui:AutoSendDebounceMs` より優先 |
| `COOLER_POLL_INTERVAL` | `5` | サーバ状態を `GET /acget` で確認する間隔(秒)。`0` でポーリングなし。`appsettings.json` の `Ui:PollIntervalSeconds` より優先 |

開発機(Windows/macOS/Linux デスクトップ)では引数なしで起動するとウィンドウ表示になる
（`--drm` を付けなければ通常のデスクトップウィンドウ）。

## 既知の注意点 / チューニング

- **DRM のグループ/シート**: 環境によっては `seatd` の導入や `udev` ルールでの権限付与が必要。
  まず `sudo` で手動起動して描画されるか確認すると切り分けやすい:
  ```bash
  sudo COOLER_DRM=1 COOLER_DRI_CARD=/dev/dri/card0 /opt/coolersystemui/CoolerSystemUI --drm
  ```
- **画面回転**: `COOLER_DRM_ROTATE`(0/90/180/270)で UI 側から回転。描画とタッチ座標の両方が補正される。
  カーネル側(`config.txt` の KMS `video=...,rotate=` 等)で回す方法もあるが、その場合タッチの座標補正は別途必要。
- **解像度/スケーリング**: パネルに合わせて `config.txt` 側、または `COOLER_DRM_SCALING` で調整。
- **タッチが効かない**: 実行ユーザが `input` グループに居るか、`/dev/input/event*` の権限を確認。
- **自動送信 / サーバ同期**: エアコン操作は値を変えると `COOLER_AUTOSEND_DEBOUNCE_MS` ミリ秒の無操作で自動送信される
  (実機リモコンに近い操作感。明示「送信」ボタンは即時送信のショートカット)。`COOLER_POLL_INTERVAL` 秒ごとに
  サーバ状態を確認し、他クライアント(Blazor 等)での変更を編集していない時だけ UI に反映する。
  IR の送信回数を抑えたい場合はデバウンスを長め(例 1500)に。ポーリングを止めたい場合は `0`。
- **画面消灯(焼け防止)**: 無操作 `COOLER_BLANK_TIMEOUT` 秒で自動消灯。UI 右上の「消灯」ボタンでも即消灯でき、
  消灯中は画面の任意の位置をタップすると点灯する(その復帰タップは背後の操作に伝わらない)。
  実消灯には上記 udev 権限が必要。`COOLER_BLANK_TIMEOUT=0` で自動消灯を無効化できる(手動消灯とタップ復帰は有効)。
- API は `lircd` 設定の書き換えと再起動を行うため root 実行。UI は非 root の `cooler` で実行する。
