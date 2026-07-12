using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoolerSystemUI.Services;

namespace CoolerSystemUI.ViewModels
{
    /// <summary>
    /// サーキュレーター操作。送信できる IR コードは
    /// power(電源) / timer(タイマー) / swing(首振り) / mode(モード) / plus(風量up) / minus(風量down) の 6 種。
    /// 人がサーキュレーター本体を見ながら押す前提のため、状態は保持せず単発送信のみ。
    /// ID はサーバー側 circulator-ir.json の commands キーと一致させること。
    /// </summary>
    public partial class CirculatorViewModel : ViewModelBase
    {
        private readonly IAircondApiClient _api;

        public CirculatorViewModel(IAircondApiClient api) => _api = api;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [RelayCommand]
        private Task Power() => SendAsync("power", "電源");

        [RelayCommand]
        private Task Timer() => SendAsync("timer", "タイマー");

        [RelayCommand]
        private Task Swing() => SendAsync("swing", "首振り");

        [RelayCommand]
        private Task Mode() => SendAsync("mode", "モード");

        [RelayCommand]
        private Task SpeedUp() => SendAsync("plus", "風量+");

        [RelayCommand]
        private Task SpeedDown() => SendAsync("minus", "風量-");

        private async Task SendAsync(string id, string label)
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await _api.SendCirculatorAsync(id);
                StatusMessage = $"{label} を送信しました";
            }
            catch (Exception ex)
            {
                StatusMessage = $"送信に失敗しました: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
