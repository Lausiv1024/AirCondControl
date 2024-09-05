using AEHAFmtSender.Client.Pages;
using AEHAFmtSender.Components;
using AEHAFmtSender.IRFormats;
using System.Diagnostics;
const int TICK = 425;
string RPiLircPath = "/etc/lirc/lircd.conf.d";
string ConfigFileBaseFmt = "begin remote\nname aircond\nflags RAW_CODES\neps 30\naeps 100\ngap 200000\ntoggle_bit_mask 0x0\n\nbegin raw_codes\nname aircond\n";
string ConfigFileExt = "\nend raw_codes\nend remote";
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
app.MapPost("/apiac", async (NP081 data) =>
{
    byte[] signalData = data.GetCurrentSignal();
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
        return Results.Ok(Environment.OSVersion);
    }
});

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AEHAFmtSender.Client._Imports).Assembly);

app.Run();
