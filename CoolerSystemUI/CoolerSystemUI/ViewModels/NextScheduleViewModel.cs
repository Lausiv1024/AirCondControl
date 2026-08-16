using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AEHAFmtSender.Shared.Models;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoolerSystemUI.Services;

namespace CoolerSystemUI.ViewModels
{
    /// <summary>
    /// 次に実行される予定をタブバー右に 1 行で出すだけの読み取り専用 ViewModel。
    /// キオスク端末ではスケジュールの編集はしないので、取得と整形しか持たない。
    /// 発火時刻の計算はサーバー (GET /schedule/next) に任せる。
    /// </summary>
    public partial class NextScheduleViewModel : ViewModelBase
    {
        private readonly IAircondApiClient _api;
        private readonly DispatcherTimer? _poll;

        /// <summary>「次 23:00 冷房 28℃」のような表示文字列。</summary>
        [ObservableProperty]
        private string text = "";

        /// <summary>予定があるか。false のときは表示ごと隠す。</summary>
        [ObservableProperty]
        private bool hasNext;

        /// <param name="pollIntervalSeconds">再取得の間隔 (秒)。0 以下でポーリングなし。</param>
        public NextScheduleViewModel(IAircondApiClient api, int pollIntervalSeconds)
        {
            _api = api;

            if (pollIntervalSeconds > 0)
            {
                _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(pollIntervalSeconds) };
                _poll.Tick += async (_, _) => await LoadAsync();
                _poll.Start();
            }
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            try
            {
                var next = await _api.GetNextScheduleAsync();
                if (next == null)
                {
                    HasNext = false;
                    Text = "";
                    return;
                }

                Text = Describe(next);
                HasNext = true;
            }
            catch (Exception ex)
            {
                // 補助表示なので画面には出さない (接続エラーの通知はエアコンタブの
                // ステータス行が担当)。ただし黙って消えると原因が追えないので、
                // サーバーが古くて 404 のようなケースに備えてログには残す。
                Console.Error.WriteLine($"[NextSchedule] 取得に失敗しました: {ex.Message}");
                HasNext = false;
                Text = "";
            }
        }

        private static string Describe(NextScheduleDto next)
        {
            var at = next.FiresAtUtc.ToLocalTime();
            // 日付が変わる予定は「明日以降」と分かるように日付を添える。
            var when = at.Date == DateTime.Now.Date ? at.ToString("HH:mm") : at.ToString("M/d HH:mm");

            var body = next.Kind switch
            {
                NextScheduleKind.OffTimer => "電源OFF",
                NextScheduleKind.OnTimer => "電源ON",
                _ => DescribeSchedule(next),
            };

            return string.IsNullOrEmpty(body) ? $"次 {when}" : $"次 {when} {body}";
        }

        private static string DescribeSchedule(NextScheduleDto next)
        {
            var parts = new List<string>();

            if (next.Power == true) parts.Add("電源ON");
            else if (next.Power == false) parts.Add("電源OFF");

            if (next.OperationMode is OperationMode mode) parts.Add(ModeLabel(mode));
            if (next.Degrees is int degrees) parts.Add($"{degrees}℃");
            if (next.Dehumidification is DehumidificationAdjustments d)
            {
                // モード表示で既に「除湿」と出ているときは強さだけでよい。
                var strength = DehumidLabel(d);
                parts.Add(next.OperationMode == OperationMode.DEHUMIDIFICATION ? strength : $"除湿{strength}");
            }
            if (next.OffAfterMinutes is int minutes) parts.Add($"{minutes}分後OFF");

            // 動作が何も指定されていないルールは名前で見分けるしかない。
            return parts.Count > 0 ? string.Join(" ", parts) : next.Name;
        }

        private static string ModeLabel(OperationMode mode) => mode switch
        {
            OperationMode.COOLING => "冷房",
            OperationMode.DEHUMIDIFICATION => "除湿",
            OperationMode.HEATING => "暖房",
            OperationMode.VENTILATION => "送風",
            _ => "",
        };

        private static string DehumidLabel(DehumidificationAdjustments d) => d switch
        {
            DehumidificationAdjustments.STRONG => "強",
            DehumidificationAdjustments.NORMAL => "標準",
            DehumidificationAdjustments.WEAK => "弱",
            _ => "",
        };
    }
}
