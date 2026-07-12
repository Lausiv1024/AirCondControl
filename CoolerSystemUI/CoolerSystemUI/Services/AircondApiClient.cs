using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using AEHAFmtSender.Shared;
using AEHAFmtSender.Shared.Models;

namespace CoolerSystemUI.Services
{
    /// <summary>
    /// <see cref="IAircondApiClient"/> の typed HttpClient 実装。
    /// BaseAddress と Timeout は DI 登録時 (<see cref="AppServices"/>) に設定する。
    /// JSON は System.Text.Json の Web 既定 (camelCase / 数値 enum) で、サーバ側のミニマル API とワイヤー互換。
    /// </summary>
    public sealed class AircondApiClient : IAircondApiClient
    {
        private readonly HttpClient _http;

        public AircondApiClient(HttpClient http) => _http = http;

        public async Task<NP081Dto> GetStateAsync(CancellationToken ct = default)
            => await _http.GetFromJsonAsync<NP081Dto>("acget", ct) ?? new NP081Dto();

        public async Task ApplyAsync(NP081Dto state, CancellationToken ct = default)
        {
            using var res = await _http.PostAsJsonAsync("apiac", state, ct);
            res.EnsureSuccessStatusCode();
        }

        public async Task SendCirculatorAsync(string id, CancellationToken ct = default)
        {
            using var res = await _http.PostAsJsonAsync("simplecode", new SimpleIRCodeDto { Id = id }, ct);
            res.EnsureSuccessStatusCode();
        }

        public async Task<AutomationConfig> GetAutomationAsync(CancellationToken ct = default)
            => await _http.GetFromJsonAsync<AutomationConfig>("automationconfig", ct) ?? new AutomationConfig();

        public async Task SetAutomationAsync(AutomationConfig config, CancellationToken ct = default)
        {
            using var res = await _http.PostAsJsonAsync("automationconfig", config, ct);
            res.EnsureSuccessStatusCode();
        }
    }
}
