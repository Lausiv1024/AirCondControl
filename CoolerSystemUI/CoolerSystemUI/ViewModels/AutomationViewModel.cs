using System;
using System.Threading.Tasks;
using AEHAFmtSender.Shared;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoolerSystemUI.Services;

namespace CoolerSystemUI.ViewModels
{
    /// <summary>
    /// 自動化設定 (エアコンとサーキュレーターの電源連動)。
    /// </summary>
    public partial class AutomationViewModel : ViewModelBase
    {
        private readonly IAircondApiClient _api;

        public AutomationViewModel(IAircondApiClient api) => _api = api;

        [ObservableProperty]
        private bool aircondPwrLink;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var cfg = await _api.GetAutomationAsync();
                AircondPwrLink = cfg.AircondPwrLink;
                StatusMessage = "設定を取得しました";
            }
            catch (Exception ex)
            {
                StatusMessage = $"接続できません: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ApplyAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await _api.SetAutomationAsync(new AutomationConfig
                {
                    AircondPwrLink = AircondPwrLink,
                });
                StatusMessage = "保存しました";
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存に失敗しました: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
