using AEHAFmtSender.Client.Pages;
using AEHAFmtSender.Components;
using AEHAFmtSender.IRFormats;
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;
const int TICK = 425;
string RPiLircPath = "/etc/lirc/lircd.conf.d";
string ConfigFileBaseFmt = "begin remote\nname aircond\nflags RAW_CODES\neps 30\naeps 100\ngap 200000\ntoggle_bit_mask 0x0\n\nbegin raw_codes\nname aircond\n";
string ConfigFileExt = "\nend raw_codes\nend remote";
string ProgramDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;

if (args.Length > 0 &&  args[0] == "CreateRunFile")
{
    if (OperatingSystem.IsLinux())
    {
        string shellFilePath = Path.Combine(ProgramDirectory, "run.sh");
        if (File.Exists(shellFilePath)) return;
        using (var sw = new StreamWriter(shellFilePath))
        {
            sw.WriteLine("#!/bin/sh");
            sw.WriteLine("cd `dirname $0`");
            sw.WriteLine("./AEHAFmtSender &");
            sw.WriteLine("sleep 10");
            sw.WriteLine("sudo systemctl restart nginx");
        }
        FileInfo fileInfo = new FileInfo(shellFilePath);
        Console.WriteLine("Run File created.");
    } else
    {
        Console.WriteLine("このオプションはLinuxのみで有効です");
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient().AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
} else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/acget", () =>
{
    if (!File.Exists(Path.Combine(ProgramDirectory, "")))
        return new NP081();
    using (StreamReader reader = new StreamReader(Path.Combine(ProgramDirectory, "config.json")))
    {
        NP081 nP081 = JsonSerializer.Deserialize<NP081>(reader.ReadToEnd());
        return nP081;
    }
});
app.MapPost("/apiac", async (NP081 data) =>
{
    byte[] signalData = data.GetCurrentSignal();
    Debug.WriteLine(Convert.ToHexString(signalData));
    if (Environment.OSVersion.VersionString.Contains("Windows"))
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
        Debug.WriteLine(conf);
        return Results.Ok(data);
    }
    else
    {
        using (var writer = new StreamWriter(Path.Combine(RPiLircPath, "aircond.conf")))
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
                }
                conf += TICK + " ";
                if (j == 0)
                    conf += "13000\n";
            }
            conf += ConfigFileExt;
            writer.Write(conf);
        }
        await Task.Delay(10);
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

        using (var sw = new StreamWriter(Path.Combine(ProgramDirectory, "config.json")))
        {
            sw.Write(JsonSerializer.Serialize(data));
        }
        return Results.Ok(Environment.OSVersion);
    }
});

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AEHAFmtSender.Client._Imports).Assembly);

app.Run();
