using System.Reflection;
using System.Text.Json;

namespace AEHAFmtSender.Automation;

public class AutomationConfigManager
{
    readonly string ProgramDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;

    public AutomationConfig? Config { get; set; }

    public AutomationConfigManager()
    {
        var fPath = Path.Combine(ProgramDirectory, "AutomationConfig.json");

        if (File.Exists(fPath))
        {
            using var sr = new StreamReader(fPath);
            try
            {
                Config = JsonSerializer.Deserialize<AutomationConfig>(sr.ReadToEnd());
            }catch (Exception ex)
            {
                Console.WriteLine("Error Loading config {0}", ex.Message);
            }
        }
    }

    public void Save()
    {
        if (Config == null)
            return;
        using var sw = new StreamWriter(Path.Combine(ProgramDirectory, "AutomationConfig.json"));
        sw.Write(JsonSerializer.Serialize(Config));
    }
}

public class AutomationConfig
{
    public bool AircondPwrLink = false;
    public bool AircondAutoPower = false;
}