using System;
using System.Collections.Generic;

namespace AEHAFmtSender.Shared.Models
{
    /// <summary>
    /// 時刻起動でエアコンの状態を変更するスケジュール。
    /// </summary>
    public class ScheduleConfig
    {
        /// <summary>スケジュール全体の有効/無効。個々のルールとは別のマスタースイッチ。</summary>
        public bool Enabled { get; set; } = true;

        public List<ScheduleRule> Rules { get; set; } = new List<ScheduleRule>();
    }

    /// <summary>
    /// スケジュール 1 件。指定した項目だけを上書きするパッチとして扱い、
    /// null の項目は現在の設定をそのまま引き継ぐ。
    /// </summary>
    public class ScheduleRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public bool Enabled { get; set; } = true;
        public string Name { get; set; } = "";

        /// <summary>
        /// 発火時刻 (ローカル時刻)。"HH:mm" 形式。
        /// このプロジェクトは netstandard2.1 で TimeOnly が使えないため文字列で持つ。
        /// HTML の input[type=time] がそのまま出す形式に合わせてある。
        /// </summary>
        public string At { get; set; } = "00:00";

        /// <summary>繰り返す曜日。None の場合は <see cref="Date"/> の単発予約として扱う。</summary>
        public DayFlags Days { get; set; } = DayFlags.None;

        /// <summary>
        /// 単発予約の対象日。"yyyy-MM-dd" 形式 (input[type=date] と同じ)。
        /// 発火すると Enabled が false になる。
        /// </summary>
        public string? Date { get; set; }

        // --- ここから下がパッチ本体 ---

        public bool? Power { get; set; }
        public OperationMode? OperationMode { get; set; }

        /// <summary>設定温度。冷房・暖房以外の運転モードでは適用されない。</summary>
        public int? Degrees { get; set; }

        public DehumidificationAdjustments? Dehumidification { get; set; }

        /// <summary>
        /// 発火してから何分後に電源を切るか。null なら現在のタイマー設定を引き継ぐ。
        /// カウントダウンはサーバー側で行うので、以後の設定変更では延びない。
        /// </summary>
        public int? OffAfterMinutes { get; set; }
    }

    /// <summary>
    /// 繰り返す曜日。ビット位置は <see cref="System.DayOfWeek"/> に合わせてある
    /// (日曜が 0)。
    /// </summary>
    [Flags]
    public enum DayFlags
    {
        None = 0,
        Sunday = 1 << 0,
        Monday = 1 << 1,
        Tuesday = 1 << 2,
        Wednesday = 1 << 3,
        Thursday = 1 << 4,
        Friday = 1 << 5,
        Saturday = 1 << 6,
        Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
        Weekend = Sunday | Saturday,
        EveryDay = Weekdays | Weekend,
    }

    /// <summary>
    /// 次に実行される予定 (GET /schedule/next)。予定が何もなければサーバーは 204 を返す。
    /// スケジュールとサーバー側タイマーのうち、先に発火する方を表す。
    /// </summary>
    public class NextScheduleDto
    {
        public NextScheduleKind Kind { get; set; }

        /// <summary>発火予定時刻 (UTC)。</summary>
        public DateTime FiresAtUtc { get; set; }

        /// <summary>スケジュールの場合のルール名。タイマーでは空。</summary>
        public string Name { get; set; } = "";

        // --- 以下はスケジュールのときだけ入る。null は「変更しない」。 ---

        public bool? Power { get; set; }
        public OperationMode? OperationMode { get; set; }
        public int? Degrees { get; set; }
        public DehumidificationAdjustments? Dehumidification { get; set; }
        public int? OffAfterMinutes { get; set; }
    }

    public enum NextScheduleKind
    {
        Schedule = 0,
        OffTimer = 1,
        OnTimer = 2,
    }

    /// <summary>
    /// サーバー側でカウントしているタイマーの状態 (GET /timer)。
    /// </summary>
    public class TimerStatus
    {
        /// <summary>電源を切る時刻 (UTC)。null なら切タイマーは動いていない。</summary>
        public DateTime? OffAtUtc { get; set; }

        /// <summary>電源を入れる時刻 (UTC)。null なら入タイマーは動いていない。</summary>
        public DateTime? OnAtUtc { get; set; }
    }
}
