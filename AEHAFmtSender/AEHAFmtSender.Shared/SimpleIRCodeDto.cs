namespace AEHAFmtSender.Shared.Models
{
    /// <summary>
    /// /simplecode へ送るサーキュレーター等の単純 IR コード。
    /// サーバ側 <c>AEHAFmtSender.IRFormats.SimpleIRCode</c> とワイヤー互換。
    /// </summary>
    public class SimpleIRCodeDto
    {
        public string? Id { get; set; }
    }
}
