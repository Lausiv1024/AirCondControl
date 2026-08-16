using System.Reflection;
using System.Text.Json;
using AEHAFmtSender.Shared.Models;

namespace AEHAFmtSender.Automation;

/// <summary>
/// ScheduleConfig.json の読み書き。AutomationConfigManager と同じ作り。
/// </summary>
public sealed class ScheduleConfigManager
{
    private readonly string _path;

    public ScheduleConfig Config { get; set; }

    public ScheduleConfigManager()
    {
        var programDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location)!.FullName;
        _path = Path.Combine(programDirectory, "ScheduleConfig.json");

        if (File.Exists(_path))
        {
            try
            {
                Config = JsonSerializer.Deserialize<ScheduleConfig>(File.ReadAllText(_path)) ?? new ScheduleConfig();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Loading schedule config {0}", ex.Message);
            }
        }
        Config = new ScheduleConfig();
    }

    public void Save()
    {
        if (Config == null)
            return;
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Config));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error Saving schedule config {0}", ex.Message);
        }
    }
}
