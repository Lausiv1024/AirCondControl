#include <Arduino.h>
#include <WiFi.h>
#include <HTTPClient.h>
#include <Wire.h>
#include <Adafruit_BME280.h>
#include <MHZ19.h>
#include <ArduinoJson.h>

#include "secrets.h"

// BME280 (I2C)
constexpr uint8_t BME280_I2C_ADDRESS = 0x76;
Adafruit_BME280 bme;
bool bmeReady = false;

// MH-Z19C (UART2: RX=16, TX=17)
constexpr int MHZ19_RX_PIN = 16;
constexpr int MHZ19_TX_PIN = 17;
HardwareSerial mhzSerial(2);
MHZ19 mhz19;

// 起動直後は MH-Z19C の暖機(約3分)が終わるまで値が不安定なため、その間は送信をスキップする。
constexpr unsigned long WARMUP_MS = 3UL * 60 * 1000;
constexpr unsigned long READ_INTERVAL_MS = 60UL * 1000;

unsigned long lastReadMs = 0;
unsigned long bootMs = 0;

void connectWiFi() {
  if (WiFi.status() == WL_CONNECTED) return;

  Serial.printf("Connecting to WiFi \"%s\"...\n", WIFI_SSID);
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  unsigned long start = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - start < 20000) {
    delay(250);
    Serial.print(".");
  }
  Serial.println();

  if (WiFi.status() == WL_CONNECTED) {
    Serial.printf("WiFi connected, IP=%s\n", WiFi.localIP().toString().c_str());
  } else {
    Serial.println("WiFi connect timed out, will retry later");
  }
}

void postSensorReading(float temperatureC, float humidityPercent, float pressureHpa, int co2Ppm) {
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("Skip POST: WiFi not connected");
    return;
  }

  JsonDocument doc;
  doc["deviceId"] = DEVICE_ID;
  doc["temperatureC"] = temperatureC;
  doc["humidityPercent"] = humidityPercent;
  doc["pressureHpa"] = pressureHpa;
  doc["co2Ppm"] = co2Ppm;

  String body;
  serializeJson(doc, body);

  String url = String("http://") + SERVER_HOST + ":" + SERVER_PORT + "/sensordata";

  HTTPClient http;
  http.begin(url);
  http.addHeader("Content-Type", "application/json");
  http.setTimeout(5000);

  int status = http.POST(body);
  if (status > 0) {
    Serial.printf("POST %s -> %d\n", url.c_str(), status);
  } else {
    Serial.printf("POST %s failed: %s\n", url.c_str(), http.errorToString(status).c_str());
  }
  http.end();
}

void setup() {
  Serial.begin(115200);
  delay(200);

  Wire.begin();
  bmeReady = bme.begin(BME280_I2C_ADDRESS);
  if (!bmeReady) {
    Serial.println("BME280 not found - check wiring/address");
  }

  mhzSerial.begin(9600, SERIAL_8N1, MHZ19_RX_PIN, MHZ19_TX_PIN);
  mhz19.begin(mhzSerial);
  // ABC(自動基線補正)はOFFにする。ONのままだと24時間窓内の最小値を屋外基準(400ppm)とみなして
  // 自動的に再校正してしまい、常時換気されない室内では実際より低く出続ける原因になる。
  mhz19.autoCalibration(false);

  connectWiFi();

  bootMs = millis();
  lastReadMs = 0; // 起動後すぐ最初の判定を行う
}

// シリアルモニタから "ZEROCAL" を送るとゼロ点校正を実行する。誤発火を避けるため自動では
// 絶対に呼ばない。実行前に、屋外など安定した外気(約400ppm)に最低20分は置いておくこと
// (Winsen MH-Z19データシート推奨)。室内 (CO2濃度が高い環境) で実行すると、その濃度を
// 400ppmとして誤って校正してしまい、今回のように値が低く張り付く原因になる。
void handleSerialCommands() {
  if (!Serial.available()) return;
  String cmd = Serial.readStringUntil('\n');
  cmd.trim();
  if (cmd == "ZEROCAL") {
    Serial.println("Performing MH-Z19C zero-point calibration (assuming ~400ppm ambient)...");
    mhz19.calibrateZero();
    Serial.println("Done.");
  }
}

void loop() {
  connectWiFi();
  handleSerialCommands();

  unsigned long now = millis();
  if (now - lastReadMs < READ_INTERVAL_MS && lastReadMs != 0) {
    delay(200);
    return;
  }
  lastReadMs = now;

  if (now - bootMs < WARMUP_MS) {
    Serial.println("MH-Z19C warming up, skipping this cycle");
    return;
  }

  float temperatureC = NAN, humidityPercent = NAN, pressureHpa = NAN;
  if (bmeReady) {
    temperatureC = bme.readTemperature();
    humidityPercent = bme.readHumidity();
    pressureHpa = bme.readPressure() / 100.0F;
  }

  // isUnLimited=false : コマンド0x86(通常/上限あり)を使う。既定のコマンド0x85(Unlimited)は
  // 多くのMH-Z19C個体で応答は正常(errorCode=RESULT_OK)なのに値が常に0になる互換性問題があるため。
  int co2Ppm = mhz19.getCO2(false);
  if (mhz19.errorCode != RESULT_OK) {
    // RESULT_TIMEOUT: センサーから無応答(配線/共通GND/ボーレートを疑う)。
    // RESULT_CRC/RESULT_MATCH: 応答はあるがデータが壊れている(配線のノイズ/ボーレート不一致)。
    Serial.printf("MH-Z19C read failed, errorCode=%d\n", mhz19.errorCode);
  }

  Serial.printf("T=%.1fC H=%.1f%% P=%.1fhPa CO2=%dppm\n",
                temperatureC, humidityPercent, pressureHpa, co2Ppm);

  postSensorReading(temperatureC, humidityPercent, pressureHpa, co2Ppm);
}
