namespace AEHAFmtSender.Shared.Models
{
    // 注意: サーバの Program.cs は AEHAFmtSender.IRFormats と AEHAFmtSender.Shared を同時に using しており、
    // OperationMode / TimerMode を修飾なしで使用している。型名衝突 (CS0104) を避けるため、
    // これらの enum と DTO は AEHAFmtSender.Shared 直下ではなく Models サブ名前空間に置く。
    /// <summary>
    /// API (/acget, /apiac) とやり取りするエアコン状態の転送オブジェクト。
    /// サーバ側 <c>AEHAFmtSender.IRFormats.NP081</c> とワイヤー互換（プロパティ名・enum 数値が一致）。
    /// System.Text.Json の既定では enum は数値でシリアライズされるため、サーバ側 enum と同じ数値を保持する。
    /// </summary>
    public class NP081Dto
    {
        public bool Power { get; set; } = false;
        public int CoolingDegrees { get; set; } = 28;
        public int HeatingDegrees { get; set; } = 20;

        /// <summary>
        /// 現在のモードでの設定温度（表示用）。サーバ側は CoolingDegrees / HeatingDegrees から再計算するため受信時は無視される。
        /// </summary>
        public int Degree { get; set; }

        public OperationMode OperationMode { get; set; } = OperationMode.COOLING;
        public TimerMode TimerMode { get; set; } = TimerMode.NONE;

        /// <summary>
        /// 除湿強度。除湿運転時のみ信号 (byte8 下位4bit) に反映される。
        /// </summary>
        public DehumidificationAdjustments Dehumidification { get; set; } = DehumidificationAdjustments.NORMAL;

        /// <summary>
        /// タイマー時間（単位：分）。
        /// </summary>
        public int TimerLength { get; set; } = 0;

        public NP081Dto() { }
    }

    public enum OperationMode
    {
        COOLING = 0x58,
        DEHUMIDIFICATION = 0x50,
        HEATING = 0x48,
        VENTILATION = 0x38
    }

    public enum TimerMode
    {
        NONE = 0x00,
        OFFTIMER = 0x03,
        ONTIMER = 0x05
    }

    /// <summary>
    /// 除湿強度 (信号 byte8 下位4bit)。数値はサーバ側 <c>AEHAFmtSender.IRFormats.DehumidificationAdjustments</c> と一致させること。
    /// </summary>
    public enum DehumidificationAdjustments
    {
        STRONG = 0x0,
        NORMAL = 0x2,
        WEAK = 0x4
    }
}
