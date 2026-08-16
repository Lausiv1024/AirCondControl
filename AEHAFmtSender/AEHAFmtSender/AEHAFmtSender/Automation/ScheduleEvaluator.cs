using System.Globalization;
using AEHAFmtSender.Shared.Models;

namespace AEHAFmtSender.Automation;

/// <summary>発火するルールと、その発火予定時刻 (ローカル)。</summary>
public readonly record struct ScheduledFire(ScheduleRule Rule, DateTime FiresAt);

/// <summary>
/// スケジュールの発火判定。副作用を持たない純粋関数にしてあるので、
/// 実機なしで日跨ぎ・取りこぼし・二重発火を検証できる。
/// </summary>
public static class ScheduleEvaluator
{
    /// <summary>
    /// 半開区間 (from, to] に発火するルールを、発火時刻の昇順で返す。
    ///
    /// 「今が発火時刻ちょうどか」ではなく区間で判定するので、処理が詰まって
    /// ティックが飛んでも取りこぼさず、同じ発火を二度拾うこともない。
    /// </summary>
    /// <param name="from">前回評価した時刻 (ローカル)。この時刻自体は含まない。</param>
    /// <param name="to">今回の評価時刻 (ローカル)。この時刻は含む。</param>
    public static IReadOnlyList<ScheduledFire> DueRules(
        IEnumerable<ScheduleRule> rules, DateTime from, DateTime to)
    {
        var fires = new List<ScheduledFire>();
        if (to <= from)
            return fires;

        var firstDate = DateOnly.FromDateTime(from);
        var lastDate = DateOnly.FromDateTime(to);

        foreach (var rule in rules)
        {
            if (!rule.Enabled)
                continue;

            // 時刻が壊れているルールは黙って無視する (UI 側の入力ミス対策)。
            if (!TryParseTime(rule.At, out var at))
                continue;

            // 区間が日を跨ぐことがあるので、範囲内の日付をすべて候補にする。
            for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
            {
                if (!MatchesDate(rule, date))
                    continue;

                var firesAt = date.ToDateTime(at);
                if (firesAt > from && firesAt <= to)
                    fires.Add(new ScheduledFire(rule, firesAt));
            }
        }

        fires.Sort((a, b) => a.FiresAt.CompareTo(b.FiresAt));
        return fires;
    }

    /// <summary>
    /// <paramref name="from"/> より後で最も早く発火するルールを返す。予定がなければ null。
    /// 表示用なので副作用はなく、DueRules とは独立に「先を見る」ためのもの。
    /// </summary>
    public static ScheduledFire? NextFire(IEnumerable<ScheduleRule> rules, DateTime from)
    {
        ScheduledFire? best = null;

        foreach (var rule in rules)
        {
            if (!rule.Enabled)
                continue;
            if (!TryParseTime(rule.At, out var at))
                continue;

            var candidate = NextFireOf(rule, at, from);
            if (candidate is DateTime c && (best is null || c < best.Value.FiresAt))
                best = new ScheduledFire(rule, c);
        }

        return best;
    }

    private static DateTime? NextFireOf(ScheduleRule rule, TimeOnly at, DateTime from)
    {
        // 単発予約: その日時が未来ならそれ、過ぎていればもう発火しない。
        if (!string.IsNullOrEmpty(rule.Date))
        {
            if (!DateOnly.TryParse(rule.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return null;
            var once = date.ToDateTime(at);
            return once > from ? once : null;
        }

        if (rule.Days == DayFlags.None)
            return null;

        // 週次なので、今日から 7 日先までに必ず該当曜日がある。
        var today = DateOnly.FromDateTime(from);
        for (var i = 0; i <= 7; i++)
        {
            var date = today.AddDays(i);
            if (!rule.Days.HasFlag((DayFlags)(1 << (int)date.DayOfWeek)))
                continue;

            var firesAt = date.ToDateTime(at);
            if (firesAt > from)
                return firesAt;
        }

        return null;
    }

    /// <summary>"HH:mm" (秒付きも許容) を解釈する。</summary>
    public static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private static bool MatchesDate(ScheduleRule rule, DateOnly date)
    {
        // 単発予約は曜日を見ない。
        if (!string.IsNullOrEmpty(rule.Date))
        {
            return DateOnly.TryParse(rule.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var single)
                && single == date;
        }

        if (rule.Days == DayFlags.None)
            return false; // 曜日も日付も指定がないルールは発火させない

        return rule.Days.HasFlag((DayFlags)(1 << (int)date.DayOfWeek));
    }
}
