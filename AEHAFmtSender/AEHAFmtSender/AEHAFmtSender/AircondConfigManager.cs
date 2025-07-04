using AEHAFmtSender.IRFormats;
using System.Reflection;
using System.Text.Json;

namespace AEHAFmtSender;

public class AircondConfigManager<TAircondControll> where TAircondControll : RemoteControlBase
{
    string ProgramDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
    public TAircondControll? controller { get; set; }
    string controllerName;
    public AircondConfigManager(string controllerName)
    {
        this.controllerName = controllerName;
        var fPath = Path.Combine(ProgramDirectory, controllerName + ".json");
        if (File.Exists(fPath))
        {
            using var sr = new StreamReader(fPath);
            try
            {
                controller = JsonSerializer.Deserialize<TAircondControll>(sr.ReadToEnd());
            } catch(Exception ex) 
            {
                Console.WriteLine("Error loading config : {0}", ex.Message);
            }
            
        }
    }

    public void Save()
    {
        using var sw = new StreamWriter(Path.Combine(ProgramDirectory, controllerName + ".json"));
        sw.Write(JsonSerializer.Serialize(controller));
    }
}
