using System;

namespace AEHAFmtSender.Shared.Models
{
    /// <summary>
    /// API (/sensordata) とやり取りする環境センサー値の転送オブジェクト。
    /// ESP32 (BME280 + MH-Z19C) からの POST および WebUI 向け GET の双方で使う。
    /// </summary>
    public class SensorReadingDto
    {
        public string DeviceId { get; set; } = "";

        /// <summary>UTC。POST 時は無視され、サーバがその時刻を付与する。</summary>
        public DateTime TimestampUtc { get; set; }

        public float TemperatureC { get; set; }
        public float HumidityPercent { get; set; }
        public float PressureHpa { get; set; }
        public int Co2Ppm { get; set; }
    }
}
