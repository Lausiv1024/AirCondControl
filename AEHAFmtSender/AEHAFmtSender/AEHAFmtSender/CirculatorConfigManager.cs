using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace AEHAFmtSender
{
    /// <summary>
    /// circulator-ir.json の読み書きを担う。AircondConfigManager に倣い、実行アセンブリと同じ
    /// ディレクトリにファイルを置く。初回（ファイル不在）は既定設定を生成して保存する。
    /// </summary>
    public class CirculatorConfigManager
    {
        static readonly string ProgramDirectory =
            Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName;
        const string FileName = "circulator-ir.json";
        static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        string FilePath => Path.Combine(ProgramDirectory, FileName);

        public CirculatorConfig Config { get; set; }

        public CirculatorConfigManager()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    Config = JsonSerializer.Deserialize<CirculatorConfig>(File.ReadAllText(FilePath))
                             ?? CirculatorConfig.CreateDefault();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error loading circulator config : {0}", ex.Message);
                    Config = CirculatorConfig.CreateDefault();
                }
            }
            else
            {
                // 編集の起点として既定 JSON を書き出しておく
                Config = CirculatorConfig.CreateDefault();
                Save();
            }
        }

        /// <summary>ディスク上の JSON を再読込する（/circulatorconfig/reload 用）。</summary>
        public void Reload()
        {
            if (File.Exists(FilePath))
            {
                Config = JsonSerializer.Deserialize<CirculatorConfig>(File.ReadAllText(FilePath))
                         ?? CirculatorConfig.CreateDefault();
            }
        }

        public void Save() => File.WriteAllText(FilePath, JsonSerializer.Serialize(Config, JsonOpts));
    }
}
