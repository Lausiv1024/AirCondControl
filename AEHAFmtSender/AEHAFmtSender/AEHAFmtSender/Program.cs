using AEHAFmtSender.Client.Pages;
using AEHAFmtSender.Components;
using AEHAFmtSender.IRFormats;
using System.Text.Json;
using System.Diagnostics;
using System.Reflection;
using AEHAFmtSender;
using R3;
using AEHAFmtSender.Shared;
using AEHAFmtSender.Shared.Models;
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
var scheduleConfig = new ScheduleConfigManager();
var scheduleState = new ScheduleStateManager();
var aircondService = new AircondService(
    configManager, automationConfig, scheduleState, factory.CreateLogger<AircondService>());

builder.Services.AddSingleton(aircondService);
builder.Services.AddSingleton(scheduleConfig);
builder.Services.AddSingleton(scheduleState);
builder.Services.AddHostedService(sp => new ScheduleService(
    aircondService, scheduleConfig, scheduleState, sp.GetRequiredService<ILogger<ScheduleService>>()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<SensorDbContext>().Database.EnsureCreated();
}

// サーキュレーターの LIRC 設定(circulator.conf)を起動時に生成・配置しておく
await aircondService.EnsureCirculatorConfAsync(circulatorConfigManager.Config);

// タイマーとスケジュールの処理は ScheduleService (BackgroundService) が担当する。

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

app.MapGet("/scheduleconfig", () => scheduleConfig.Config);

app.MapPost("/scheduleconfig", (ScheduleConfig cfg) =>
{
    scheduleConfig.Config = cfg;
    logger.LogInformation("Schedule Config Updated ({Count} rules)", cfg.Rules.Count);
    scheduleConfig.Save();
});

// サーバー側でカウントしているタイマーの残り。UI の残り時間表示用。
app.MapGet("/timer", () => new TimerStatus
{
    OffAtUtc = scheduleState.State.OffAtUtc,
    OnAtUtc = scheduleState.State.OnAtUtc,
});

// 次に実行される予定。スケジュールとタイマーのうち先に発火する方を返す。
// キオスク端末 (CoolerSystemUI) の常時表示用で、編集はしないので読み取り専用。
app.MapGet("/schedule/next", () =>
{
    var candidates = new List<NextScheduleDto>();

    if (scheduleConfig.Config.Enabled
        && ScheduleEvaluator.NextFire(scheduleConfig.Config.Rules, DateTime.Now) is ScheduledFire fire)
    {
        candidates.Add(new NextScheduleDto
        {
            Kind = NextScheduleKind.Schedule,
            // FiresAt はローカルの壁時計時刻 (Kind は Unspecified)。UTC に直して返す。
            FiresAtUtc = DateTime.SpecifyKind(fire.FiresAt, DateTimeKind.Local).ToUniversalTime(),
            Name = fire.Rule.Name,
            Power = fire.Rule.Power,
            OperationMode = fire.Rule.OperationMode,
            Degrees = fire.Rule.Degrees,
            Dehumidification = fire.Rule.Dehumidification,
            OffAfterMinutes = fire.Rule.OffAfterMinutes,
        });
    }

    if (scheduleState.State.OffAtUtc is DateTime offAt)
        candidates.Add(new NextScheduleDto { Kind = NextScheduleKind.OffTimer, FiresAtUtc = offAt });

    if (scheduleState.State.OnAtUtc is DateTime onAt)
        candidates.Add(new NextScheduleDto { Kind = NextScheduleKind.OnTimer, FiresAtUtc = onAt });

    if (candidates.Count == 0)
        return Results.NoContent();

    return Results.Ok(candidates.OrderBy(c => c.FiresAtUtc).First());
});

app.MapSensorEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AEHAFmtSender.Client._Imports).Assembly);


app.Run();
