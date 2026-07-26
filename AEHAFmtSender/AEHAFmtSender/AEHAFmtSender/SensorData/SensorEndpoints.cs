using AEHAFmtSender.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace AEHAFmtSender.SensorData
{
    /// <summary>
    /// ESP32 (BME280 + MH-Z19C) からのセンサー値受信と、WebUI 向けの参照 API。
    /// </summary>
    public static class SensorEndpoints
    {
        public static void MapSensorEndpoints(this WebApplication app)
        {
            app.MapPost("/sensordata", async (SensorReadingDto dto, SensorDbContext db) =>
            {
                var reading = new SensorReading
                {
                    DeviceId = dto.DeviceId,
                    TimestampUtc = DateTime.UtcNow,
                    TemperatureC = dto.TemperatureC,
                    HumidityPercent = dto.HumidityPercent,
                    PressureHpa = dto.PressureHpa,
                    Co2Ppm = dto.Co2Ppm,
                };
                db.SensorReadings.Add(reading);
                await db.SaveChangesAsync();
                return Results.Ok();
            });

            app.MapGet("/sensordata/latest", async (SensorDbContext db) =>
            {
                var reading = await db.SensorReadings
                    .OrderByDescending(r => r.TimestampUtc)
                    .FirstOrDefaultAsync();

                if (reading == null)
                    return Results.NoContent();

                return Results.Ok(ToDto(reading));
            });

            // 表示範囲は 30 分から選べるので、単位は時間ではなく分。
            app.MapGet("/sensordata/history", async (SensorDbContext db, int minutes = 1440, int limit = 500) =>
            {
                minutes = Math.Clamp(minutes, 1, 60 * 24 * 31);
                limit = Math.Clamp(limit, 2, 5000);
                var since = DateTime.UtcNow.AddMinutes(-minutes);

                // 必ず新しい側から取る。昇順のまま Take すると期間内の「古い方 limit 件」しか
                // 返らず、60秒間隔の計測では 8 時間ほどでグラフが打ち切られてしまう。
                var readings = await db.SensorReadings
                    .Where(r => r.TimestampUtc >= since)
                    .OrderByDescending(r => r.TimestampUtc)
                    .Take(MaxScanRows)
                    .ToListAsync();
                readings.Reverse();

                return Results.Ok(Decimate(readings, limit).Select(ToDto));
            });
        }

        /// <summary>履歴1回分で DB から読む上限 (60秒間隔なら約2週間分)。</summary>
        private const int MaxScanRows = 20000;

        /// <summary>
        /// 期間全体を保ったまま件数を limit 以下へ間引く。各区間の最後の値を採るので、
        /// 末尾は必ず最新の計測値になり、グラフの右端が現在時刻に一致する。
        /// </summary>
        private static IEnumerable<SensorReading> Decimate(List<SensorReading> readings, int limit)
        {
            if (readings.Count <= limit)
                return readings;

            var stride = (double)readings.Count / limit;
            return Enumerable.Range(1, limit)
                .Select(i => readings[Math.Min((int)(i * stride) - 1, readings.Count - 1)]);
        }

        private static SensorReadingDto ToDto(SensorReading r) => new()
        {
            DeviceId = r.DeviceId,
            // SQLite から読み戻すと Kind が Unspecified になり、JSON に "Z" が付かない。
            // クライアントが UTC と解釈できるよう明示する。
            TimestampUtc = DateTime.SpecifyKind(r.TimestampUtc, DateTimeKind.Utc),
            TemperatureC = r.TemperatureC,
            HumidityPercent = r.HumidityPercent,
            PressureHpa = r.PressureHpa,
            Co2Ppm = r.Co2Ppm,
        };
    }
}
