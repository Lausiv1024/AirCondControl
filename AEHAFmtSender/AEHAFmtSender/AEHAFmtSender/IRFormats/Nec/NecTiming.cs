namespace AEHAFmtSender.IRFormats.Nec
{
    /// <summary>
    /// NEC フォーマットの変調タイミング。既定値は NEC 標準 (T = 562us)。
    /// <see cref="UnitMicros"/> 以外は基本単位 T の倍数で表す（<see cref="GapMicros"/> のみ絶対値[us]）。
    /// リモコンが標準と異なる場合は circulator-ir.json の timing セクションを書き換えるだけで対応できる。
    /// </summary>
    public class NecTiming
    {
        /// <summary>基本単位 T [us]。NEC 標準は 562us。</summary>
        public int UnitMicros { get; set; } = 562;

        /// <summary>リーダー部の点灯長 (T 倍)。NEC は 16T。</summary>
        public int LeaderMark { get; set; } = 16;

        /// <summary>リーダー部の消灯長 (T 倍)。NEC は 8T。</summary>
        public int LeaderSpace { get; set; } = 8;

        /// <summary>各ビットの点灯長 (T 倍)。NEC は 1T。</summary>
        public int BitMark { get; set; } = 1;

        /// <summary>ビット 0 の消灯長 (T 倍)。NEC は 1T。</summary>
        public int ZeroSpace { get; set; } = 1;

        /// <summary>ビット 1 の消灯長 (T 倍)。NEC は 3T。</summary>
        public int OneSpace { get; set; } = 3;

        /// <summary>ストップビット（末尾）の点灯長 (T 倍)。NEC は 1T。</summary>
        public int StopMark { get; set; } = 1;

        /// <summary>フレーム末尾の無信号ギャップ [us]。NEC のフレーム周期は 108ms。</summary>
        public int GapMicros { get; set; } = 108000;
    }
}
