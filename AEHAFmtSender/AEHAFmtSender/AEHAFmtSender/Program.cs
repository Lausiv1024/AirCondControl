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
using AEHAFmtSender.SensorData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
const int TICK = 425;
string RPiLircPath = "/etc/lirc/lircd.conf.d";
string ConfigFileBaseFmt = "begin remote\nname aircond\nflags RAW_CODES\neps 30\naeps 100\ngap 200000\ntoggle_bit_mask 0x0\n\nbegin raw_codes\nname aircond\n";
string ConfigFileExt = "\nend raw_codes\nend remote";
string ProgramDirectory = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
var configManager = new AircondConfigManager<NP081>("NP081");
var circulatorConfigManager = new CirculatorConfigManager();
var builder = WebApplication.CreateBuilder(args);
using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = factory.CreateLogger("Program");
// Add services to the container.
builder.Services.AddHttpClient().AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

string SensorDbPath = Path.Combine(ProgramDirectory, "sensordata.db");
builder.Services.AddDbContext<SensorDbContext>(options => options.UseSqlite($"Data Source={SensorDbPath}"));

// 赤外線送信はすべて AircondService 経由にして直列化する。スケジューラからも使えるよう
// DI にも登録しておく。
var automationConfig = new AutomationConfigManager();
var aircondService = new AircondService(configManager, automationConfig, factory.CreateLogger<AircondService>());
builder.Services.AddSingleton(aircondService);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<SensorDbContext>().Database.EnsureCreated();
}

// サーキュレーターの LIRC 設定(circulator.conf)を起動時に生成・配置しておく
await aircondService.EnsureCirculatorConfAsync(circulatorConfigManager.Config);

// TODO: このタイマーはスケジュール機能の実装時に ScheduleService へ置き換える。
//       現状はエアコンへ OFF 信号を送っていない (内部状態の更新のみ)。
Observable.Interval(TimeSpan.FromSeconds(10))
    .Select(u => configManager.controller ?? new NP081())
    .Where((c) => c.Power)
    .Where((c) => c.TimerMode != TimerMode.NONE)
    .Where(c => DateTime.Now.Hour == aircondService.TimerStarted.AddMinutes(c.TimerLength).Hour)
    .Where(c => DateTime.Now.Minute == aircondService.TimerStarted.AddMinutes(c.TimerLength).Minute)
    .Subscribe(async(c) =>
{
    if (c.TimerMode == TimerMode.OFFTIMER)
        c.Power = false; //オフタイマならエアコンの電源ごと切れる
    c.TimerMode = TimerMode.NONE;
    if (automationConfig.Config.AircondPwrLink)
        await aircondService.SendCirculatorAsync("power");
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
    await aircondService.ApplyAsync(data);
    return Results.Ok(Environment.OSVersion);
});

app.MapPost("/simplecode", async (SimpleIRCode code) =>
{
    await aircondService.SendCirculatorAsync(code.Id);
    return Results.Ok("OK");
});

app.MapGet("/circulatorconfig", () => circulatorConfigManager.Config);

app.MapPost("/circulatorconfig", async (CirculatorConfig cfg) =>
{
    circulatorConfigManager.Config = cfg;
    circulatorConfigManager.Save();
    await aircondService.EnsureCirculatorConfAsync(cfg); // 即座に circulator.conf を再生成
    return Results.Ok("OK");
});

app.MapPost("/circulatorconfig/reload", async () =>
{
    circulatorConfigManager.Reload(); // ディスク上で編集した JSON を反映
    await aircondService.EnsureCirculatorConfAsync(circulatorConfigManager.Config);
    return Results.Ok("OK");
});

app.MapGet("/automationconfig", () =>
{
    if (automationConfig.Config == null)
        return new AutomationConfig();
    return automationConfig.Config;
});

app.MapPost("/automationconfig", (AutomationConfig cfg) =>
{
    automationConfig.Config = cfg;
    logger.LogInformation("Automation Config Updated");
    automationConfig.Save();
});

app.MapSensorEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AEHAFmtSender.Client._Imports).Assembly);


app.Run();
