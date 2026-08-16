using System.Reflection;
using System.Text.Json;

namespace AEHAFmtSender.Automation;

/// <summary>
/// サーバー側でカウントしているタイマーの実行状態。
/// 残り分数ではなく発火時刻そのものを持つので、再起動しても延びない。
/// </summary>
public class ScheduleState
{
    /// <summary>電源を切る時刻 (UTC)。null なら切タイマーは動いていない。</summary>
    public DateTime? OffAtUtc { get; set; }

    /// <summary>電源を入れる時刻 (UTC)。null なら入タイマーは動いていない。</summary>
    public DateTime? OnAtUtc { get; set; }

    /// <summary>最後にスケジュールを評価した時刻 (UTC)。取りこぼし判定の起点。</summary>
    public DateTime? LastTickUtc { get; set; }
}

/// <summary>
/// <see cref="ScheduleState"/> の読み書き。
///
/// 実行状態をユーザーが編集する ScheduleConfig と別ファイルに分けているのは、
/// 設定の保存が丸ごと置き換えなので、同居させると UI から設定を保存した瞬間に
/// 進行中のカウントダウンが消えてしまうため。
/// </summary>
public sealed class ScheduleStateManager
{
    private readonly string _path;
    private readonly object _lock = new();

    public ScheduleState State { get; private set; }

    public ScheduleStateManager()
    {
        var programDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName;
        _path = Path.Combine(programDirectory, "ScheduleState.json");

        if (File.Exists(_path))
        {
            try
            {
                State = JsonSerializer.Deserialize<ScheduleState>(File.ReadAllText(_path)) ?? new ScheduleState();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Loading schedule state {0}", ex.Message);
            }
        }
        State = new ScheduleState();
    }

    /// <summary>状態を書き換えて保存する。HTTP とバックグラウンドの両方から呼ばれる。</summary>
    public void Update(Action<ScheduleState> mutate)
    {
        lock (_lock)
        {
            mutate(State);
            try
            {
                File.WriteAllText(_path, JsonSerializer.Serialize(State));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Saving schedule state {0}", ex.Message);
            }
        }
    }
}
