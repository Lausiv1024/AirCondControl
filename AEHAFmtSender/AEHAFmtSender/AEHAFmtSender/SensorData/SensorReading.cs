namespace AEHAFmtSender.SensorData
{
    /// <summary>
    /// sensordata.db に保存される1回分のセンサー計測値。
    /// </summary>
    public class SensorReading
    {
        public int Id { get; set; }
        public string DeviceId { get; set; } = "";
        public DateTime TimestampUtc { get; set; }
        public float TemperatureC { get; set; }
        public float HumidityPercent { get; set; }
        public float PressureHpa { get; set; }
        public int Co2Ppm { get; set; }
    }
}
