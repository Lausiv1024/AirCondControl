using System.Collections.Generic;
using AEHAFmtSender.IRFormats.Nec;

namespace AEHAFmtSender
{
    /// <summary>
    /// サーキュレーター用 IR 設定。circulator-ir.json に保存され、実行時に LIRC の
    /// circulator.conf(RAW_CODES) を生成する元データになる。
    /// 買い替え・機種変更時はこの JSON を書き換えるだけで再設定できる（再コンパイル不要）。
    /// </summary>
    public class CirculatorConfig
    {
        /// <summary>LIRC 上のリモコン名。irsend SEND_ONCE {RemoteName} {id} に対応。</summary>
        public string RemoteName { get; set; } = "circulator";

        /// <summary>NEC 変調タイミング（既定は NEC 標準）。</summary>
        public NecTiming Timing { get; set; } = new();

        /// <summary>
        /// コマンド ID → NEC バイト列（16 進, 空白/カンマ区切り）。
        /// ID は UI/API が送る文字列（power/plus/minus など）と一致させること。
        /// </summary>
        public Dictionary<string, string> Commands { get; set; } = new();

        /// <summary>
        /// 現行サーキュレーターの既定設定。circulator-ir.json が無い場合に生成される。
        /// 仕様: 32 00 EF &lt;data&gt; &lt;~data&gt;（40bit フレーム, 冪等性なし）。
        /// </summary>
        public static CirculatorConfig CreateDefault() => new()
        {
            RemoteName = "circulator",
            Timing = new NecTiming(),
            Commands = new Dictionary<string, string>
            {
                ["power"] = "32 00 EF 05 FA", // ON/OFF
                ["timer"] = "32 00 EF 06 F9", // TIMER
                ["swing"] = "32 00 EF 0D F2", // 首振り
                ["mode"]  = "32 00 EF 0A F5", // モード
                ["plus"]  = "32 00 EF 0E F1", // 風量+
                ["minus"] = "32 00 EF 16 E9", // 風量-
            }
        };
    }
}
