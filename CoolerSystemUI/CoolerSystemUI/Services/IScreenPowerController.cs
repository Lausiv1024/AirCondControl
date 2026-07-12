using System;
using System.IO;

namespace CoolerSystemUI.Services
{
    /// <summary>画面(バックライト)の消灯・点灯を制御する。</summary>
    public interface IScreenPowerController
    {
        /// <summary>sysfs バックライトが利用可能か(false の場合は黒オーバーレイのみで運用)。</summary>
        bool IsSupported { get; }

        /// <summary>バックライトを消灯する。</summary>
        void Sleep();

        /// <summary>バックライトを点灯する。</summary>
        void Wake();
    }

    /// <summary>
    /// Linux sysfs バックライト (/sys/class/backlight/&lt;dev&gt;) を制御して画面を消灯/点灯する。
    /// 書き込めない環境(開発用デスクトップ等)では何もしない no-op として振る舞うため、
    /// ロジック自体は PC でも動作する(視覚的な消灯は黒オーバーレイ側が担う)。
    /// </summary>
    public sealed class ScreenPowerController : IScreenPowerController
    {
        // bl_power: 0=点灯 / 1=消灯(FB_BLANK_POWERDOWN)。
        private readonly string? _blPowerPath;
        // brightness: 0–255。消灯時は 0 を書き、点灯時に元の値へ復元する。
        private readonly string? _brightnessPath;
        private string? _savedBrightness;

        public bool IsSupported => _blPowerPath != null || _brightnessPath != null;

        /// <param name="backlightDir">
        /// バックライトの sysfs ディレクトリ。null/空なら /sys/class/backlight 配下を自動検出する。
        /// </param>
        public ScreenPowerController(string? backlightDir)
        {
            var dir = ResolveDir(backlightDir);
            if (dir == null)
                return;

            var bl = Path.Combine(dir, "bl_power");
            if (File.Exists(bl))
                _blPowerPath = bl;

            var br = Path.Combine(dir, "brightness");
            if (File.Exists(br))
                _brightnessPath = br;
        }

        private static string? ResolveDir(string? configured)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(configured))
                    return Directory.Exists(configured) ? configured : null;

                const string root = "/sys/class/backlight";
                if (!Directory.Exists(root))
                    return null;

                // 最初に見つかった 1 台を採用する(複数パネルは想定しない)。
                foreach (var d in Directory.EnumerateDirectories(root))
                    return d;
                return null;
            }
            catch
            {
                return null;
            }
        }

        public void Sleep()
        {
            // ラズパイ公式 DSI ディスプレイはファーム/世代差で bl_power だけでは消えきらない例があるため、
            // 明るさを退避して 0 にしたうえで bl_power=1 も書く(どちらか一方しか効かない環境でも確実に消灯)。
            if (_brightnessPath != null)
            {
                _savedBrightness ??= TryRead(_brightnessPath);
                TryWrite(_brightnessPath, "0");
            }
            TryWrite(_blPowerPath, "1");
        }

        public void Wake()
        {
            // 先に電源を入れてから明るさを復元する。
            TryWrite(_blPowerPath, "0");
            if (_brightnessPath != null)
            {
                TryWrite(_brightnessPath, _savedBrightness ?? "255");
                _savedBrightness = null;
            }
        }

        private static bool TryWrite(string? path, string value)
        {
            if (path == null)
                return false;
            try
            {
                File.WriteAllText(path, value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string? TryRead(string path)
        {
            try
            {
                return File.ReadAllText(path).Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
