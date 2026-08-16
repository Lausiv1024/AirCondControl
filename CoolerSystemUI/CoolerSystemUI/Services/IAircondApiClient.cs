using System.Threading;
using System.Threading.Tasks;
using AEHAFmtSender.Shared;
using AEHAFmtSender.Shared.Models;

namespace CoolerSystemUI.Services
{
    /// <summary>
    /// AEHAFmtSender の REST API を叩くクライアント。
    /// </summary>
    public interface IAircondApiClient
    {
        /// <summary>現在のエアコン状態を取得する (GET /acget)。</summary>
        Task<NP081Dto> GetStateAsync(CancellationToken ct = default);

        /// <summary>エアコン状態を送信し IR を発射させる (POST /apiac)。</summary>
        Task ApplyAsync(NP081Dto state, CancellationToken ct = default);

        /// <summary>サーキュレーターの単純コードを送信する (POST /simplecode)。id は power / plus / minus。</summary>
        Task SendCirculatorAsync(string id, CancellationToken ct = default);

        /// <summary>自動化設定を取得する (GET /automationconfig)。</summary>
        Task<AutomationConfig> GetAutomationAsync(CancellationToken ct = default);

        /// <summary>自動化設定を保存する (POST /automationconfig)。</summary>
        Task SetAutomationAsync(AutomationConfig config, CancellationToken ct = default);

        /// <summary>
        /// 次に実行される予定を取得する (GET /schedule/next)。予定がなければ null。
        /// 発火時刻の計算はサーバー側に任せる (キオスクは表示のみ)。
        /// </summary>
        Task<NextScheduleDto?> GetNextScheduleAsync(CancellationToken ct = default);
    }
}
