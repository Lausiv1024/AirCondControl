using AEHAFmtSender.Client.Pages;
using AEHAFmtSender.Components;
using AEHAFmtSender.IRFormats;
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;
using AEHAFmtSender;
using R3;
using AEHAFmtSender.Shared;
using AEHAFmtSender.Automation;
const int TICK = 425;
string RPiLircPath = "/etc/lirc/lircd.conf.d";
string ConfigFileBaseFmt = "begin remote\nname aircond\nflags RAW_CODES\neps 30\naeps 100\ngap 200000\ntoggle_bit_mask 0x0\n\nbegin raw_codes\nname aircond\n";
string ConfigFileExt = "\nend raw_codes\nend remote";
string ProgramDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
var configManager = new AircondConfigManager<NP081>("NP081");
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient().AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();
var automationConfig = new AutomationConfigManager();
DateTime TimerStarted = DateTime.Now;

Observable.Interval(TimeSpan.FromSeconds(10))
    .Select(u => configManager.controller ?? new NP081())
    .Where((c) => c.Power)
    .Where((c) => c.TimerMode != TimerMode.NONE)
    .Where(c => DateTime.Now.Hour == TimerStarted.AddHours(c.TimerLength / 60).Hour)
    .Where(c => DateTime.Now.Minute == TimerStarted.AddHours(c.TimerLength / 60).Minute)
    .Subscribe(async(c) =>
{
    c.TimerMode = TimerMode.NONE;
    if (c.TimerMode == TimerMode.OFFTIMER)
        c.Power = false; //オフタイマならエアコンの電源ごと切れる
    if (automationConfig.Config.AircondPwrLink)
        await sendCirculatorSignal("power");
    configManager.controller = c;
    configManager.Save();
});

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
    if (configManager.controller == null)
        return new NP081();
    return configManager.controller;
});
app.MapPost("/apiac", async (NP081 data) =>
{
    byte[] signalData = data.GetCurrentSignal();
    Debug.WriteLine(Convert.ToHexString(signalData));

    await IrSending.SendByte(signalData);

    if (configManager.controller != null)
    {
        if (automationConfig.Config.AircondPwrLink && data.PowerStateChanged(configManager.controller))
            await sendCirculatorSignal("power");
        if (data.TimerStatusChanged(configManager.controller) && data.TimerMode != TimerMode.NONE)
        {
            TimerStarted = DateTime.Now;
            if (automationConfig.Config.AircondPwrLink && (data.TimerMode == TimerMode.ONTIMER
            || (configManager.controller.TimerMode == TimerMode.ONTIMER && data.TimerMode == TimerMode.NONE)))
            {
                await sendCirculatorSignal("power");
            }
        }
    }
    configManager.controller = data;
    configManager.Save();

    return Results.Ok(Environment.OSVersion);
    
});

app.MapPost("/simplecode", async (SimpleIRCode code) =>
{
    await sendCirculatorSignal(code.Id);
    return Results.Ok("OK");
});

async Task sendCirculatorSignal(string? signal)
{
    if (signal == null)
        return;
    var psi = new ProcessStartInfo();
    psi.FileName = "irsend";
    psi.UseShellExecute = true;
    psi.Arguments = $"SEND_ONCE circulator {signal}";
    Console.WriteLine("SEND_ONCE circulator {0}", signal);
    var p = Process.Start(psi);
    await p.WaitForExitAsync();
}

app.MapGet("/automationconfig", () =>
{
    if (automationConfig.Config == null)
        return new AutomationConfig();
    return automationConfig.Config;
});

app.MapPost("/automationconfig", (AutomationConfig cfg) =>
{
    automationConfig.Config = cfg;
    automationConfig.Save();
});

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AEHAFmtSender.Client._Imports).Assembly);

app.Run();
