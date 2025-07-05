using System.Diagnostics;
using System.Reflection;

namespace AEHAFmtSender;

public class IrSending
{
    const string ConfigFileBaseFmt = "begin remote\nname aircond\nflags RAW_CODES\neps 30\naeps 100\ngap 200000\ntoggle_bit_mask 0x0\n\nbegin raw_codes\nname aircond\n";
    const string ConfigFileExt = "\nend raw_codes\nend remote";
    const string RPiLircDirPath = "/etc/lirc/lircd.conf.d";
    const string LircFileName = "aircond.conf";
    const int TICK = 425;

/// <summary>
/// Sends a byte array to the appropriate platform-specific signal handler.
/// </summary>
/// <remarks>This method determines the platform at runtime and routes the data to the appropriate handler: <see
/// cref="SendByteWin"/> for Windows platforms and <see cref="SendByteRPi"/> for Unix-based platforms.</remarks>
/// <param name="data">The byte array to be sent. Must not be null or empty.</param>
/// <returns></returns>
    public static async Task SendByte(byte[] data)
    {
        var d = CreateSignalData(data);
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            SendByteWin(d);
        } else if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            await SendByteRPi(d);
        }
    }

    /// <summary>
    /// Sends the specified string data to the debug output on Windows platforms.
    /// </summary>
    /// <remarks>This method is intended for use in Windows environments where debug output is available. The
    /// method writes the provided data to the debug output stream for diagnostic purposes.</remarks>
    /// <param name="data">The string data to be written to the debug output. Cannot be null.</param>
    private static void SendByteWin(string data)
    {
        Debug.WriteLine(data); //Windowsの場合デバッグ出力する
    }

    /// <summary>
    /// Sends a command to the Raspberry Pi's LIRC (Linux Infrared Remote Control) system to transmit an infrared
    /// signal.
    /// </summary>
    /// <remarks>This method writes the specified data to the LIRC configuration file, restarts the LIRC
    /// daemon,  and then sends an infrared signal using the `irsend` command.  It assumes that the LIRC system is
    /// properly configured on the Raspberry Pi.  The method introduces a small delay after writing the data to ensure
    /// the LIRC daemon processes the changes correctly.</remarks>
    /// <param name="data">The data to be written to the LIRC configuration file. This typically represents the command or signal to be
    /// sent.</param>
    /// <returns></returns>
    private static async Task SendByteRPi(string data)
    {
        using var sw = new StreamWriter(Path.Combine(RPiLircDirPath, LircFileName));
        sw.Write(data);
        await Task.Delay(5);
        var psi2 = new ProcessStartInfo()
        {
            FileName = "systemctl",
            UseShellExecute = true,
            Arguments = "restart lircd"
        };
        await Process.Start(psi2).WaitForExitAsync();
        var psi = new ProcessStartInfo();
        psi.FileName = "irsend";
        psi.UseShellExecute = true;
        psi.Arguments = $"SEND_ONCE aircond aircond";
        var p = Process.Start(psi);
        await p.WaitForExitAsync();
    }

    /// <summary>
    /// Generates a formatted signal data string based on the provided byte array.
    /// </summary>
    /// <remarks>The returned string is formatted to include timing intervals and configuration markers based
    /// on the input signal data. This method processes each byte in the array, applying specific timing calculations
    /// for each bit, and appends the results to the output string.</remarks>
    /// <param name="signalData">An array of bytes representing the signal data to be processed.</param>
    /// <returns>A string containing the formatted signal data, including timing and configuration details.</returns>
    private static string CreateSignalData(byte[] signalData)
    {
        string conf = ConfigFileBaseFmt;
        conf += TICK * 8 + " ";
        conf += TICK * 4 + " ";
        for (int j = 0; j < 2; j++)
        {
            foreach (var signal in signalData)
            {
                for (int i = 0; i < 8; i++)
                {
                    byte b = (byte)Math.Pow(2, i);
                    byte c = (byte)(signal & b);
                    int space = c != 0 ? TICK * 3 : TICK;
                    conf += TICK + " ";
                    conf += space + " ";
                }
                conf += "\n";
            }
            conf += TICK + " ";
            if (j == 0)
                conf += "13000\n";
        }
        conf += ConfigFileExt;
        return conf;
    }
}
