# SensorNode

ESP32 + BME280(温度/湿度/気圧) + MH-Z19C(CO2濃度) で室内環境データを取得し、
RPi上の `AEHAFmtSender` に一定間隔でHTTP POSTするPlatformIOプロジェクト。

## 配線

### BME280 (I2C)
| BME280 | ESP32 |
| --- | --- |
| VIN | 3.3V |
| GND | GND |
| SCL | GPIO22 |
| SDA | GPIO21 |

I2Cアドレスは `0x76` を既定にしている（モジュールによっては `0x77`。その場合 `src/main.cpp` の
`BME280_I2C_ADDRESS` を変更する）。

### MH-Z19C (UART)
| MH-Z19C | ESP32 |
| --- | --- |
| Vin | 5V |
| GND | GND |
| TX | GPIO16 (RX2) |
| RX | GPIO17 (TX2) |

MH-Z19Cは5V給電が必要。電源投入後 約3分間はウォームアップのため値が安定しない
（ファームウェア側でその間は送信をスキップする）。

HDピン（ゼロ点校正トリガー、7秒以上Lowで発火）は未接続のままでよい。ファームウェア側からは
呼び出していないが、常時換気しない部屋だと自動基線補正(ABC)が誤って基準を下げてしまうため
`autoCalibration(false)` でABCを無効化している。定期的な精度確保のため、月1回程度は外気
(屋外 or 換気直後の部屋)でしばらく稼働させて手動でゼロ点校正するとよい。

## トラブルシュート

- **CO2が常に0ppm**: センサーからの応答自体は正常(`errorCode`は出ない)なのに値が0になる場合、
  個体によっては `getCO2()` の既定コマンド(Unlimited, 0x85)に対応していないことがある。
  `src/main.cpp` では `mhz19.getCO2(false)` で標準コマンド(0x86)を使うことでこれを回避している。
- **CO2が常に400ppm付近に張り付く**: 起動直後で内部フィルタが実測値に収束しきっていない
  (数分〜十数分かかることがある)か、ABCが有効なまま換気の少ない部屋で稼働させて基準が
  下がってしまった可能性が高い。センサーに直接息を吹きかけて反応するか確認するとよい
  (反応すればセンサー自体は正常)。息を吹きかけると一瞬跳ね上がってすぐ元に戻る場合、
  過去に誤った環境 (CO2濃度の高い室内など) でゼロ点校正されてしまっている可能性が高い。
  以下の手順で校正をやり直す:
  1. センサー(ESP32ごと)を屋外など安定した外気(目安400ppm程度)の場所に持って行き、
     最低20分放置する (Winsen MH-Z19データシート推奨の安定化時間)。
  2. PCと接続したまま `pio device monitor --port <ポート>` でシリアルモニタを開き、
     `ZEROCAL` と入力してEnter (`src/main.cpp` の `handleSerialCommands()` が処理する)。
  3. `Done.` と表示されれば校正完了。屋内に戻して通常運用に戻す。

  **注意**: 室内でこのコマンドを実行すると、その場の濃度を400ppmとして誤って校正してしまい
  症状が悪化するため、必ず安定した外気で行うこと。

## セットアップ

1. `include/secrets.h.example` を `include/secrets.h` にコピーし、WiFi情報とサーバーの
   LAN上のIP/ポートを設定する。
2. RPi側の `AEHAFmtSender` が `0.0.0.0:5195` でLISTENしていること（`deploy/aehafmtsender.service`
   の `ASPNETCORE_URLS` を参照）を確認する。
3. ビルド・書き込み:

```bash
pio run
pio run -t upload
pio device monitor
```

## 送信データ

60秒間隔で以下のJSONを `POST http://<SERVER_HOST>:<SERVER_PORT>/sensordata` に送信する。

```json
{
  "deviceId": "esp32-01",
  "temperatureC": 24.3,
  "humidityPercent": 45.2,
  "pressureHpa": 1013.2,
  "co2Ppm": 650
}
```
