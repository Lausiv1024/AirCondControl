using System.Collections.Generic;

namespace AEHAFmtSender.IRFormats.Nec
{
    /// <summary>
    /// 任意長のバイト列を NEC フォーマットの pulse/space タイミング列（LIRC mode2 相当, 単位 us）へ変換する。
    /// バイト列は意味解釈せずそのまま変調するため、フレーム長（4/5 バイト等）やリモコン機種に依存しない。
    /// カスタマーコードやパリティ（反転データ）の整合は呼び出し側の責務。
    /// </summary>
    public static class NecEncoder
    {
        /// <summary>
        /// NEC 変調を行い pulse/space の長さ[us]列を返す。
        /// 並びは pulse(点灯) から始まり pulse/space が交互。
        /// リーダー(mark, space) → 各バイトを LSB ファーストで (mark, space)×8 → ストップ mark、で終わる。
        /// フレーム末尾のギャップ(space)は含めない（LIRC 側の gap 設定で付与する）。
        /// </summary>
        public static IReadOnlyList<int> Encode(byte[] data, NecTiming t)
        {
            var pulses = new List<int>();
            int u = t.UnitMicros;

            // リーダー
            pulses.Add(t.LeaderMark * u);   // pulse
            pulses.Add(t.LeaderSpace * u);  // space

            // データ（各バイト LSB ファースト）
            foreach (var b in data)
            {
                for (int i = 0; i < 8; i++)
                {
                    bool bit = (b & (1 << i)) != 0;
                    pulses.Add(t.BitMark * u);                         // pulse
                    pulses.Add((bit ? t.OneSpace : t.ZeroSpace) * u);  // space
                }
            }

            // ストップビット（末尾は必ず pulse で終わる）
            pulses.Add(t.StopMark * u);
            return pulses;
        }
    }
}
