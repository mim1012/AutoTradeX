using AutoTrader.Core.Configuration;
using AutoTrader.Core.Jobs;
using AutoTrader.Core.Services.Api;
using AutoTrader.Core.Services.Auth;
using AutoTrader.Core.Services.Realtime;
using AutoTrader.Core.Services.Stock;
using AutoTrader.Core.Services.Throttling;
using AutoTrader.Core.Services.Trading;
using AutoTrader.Core.Services.WebSocket;
using AutoTrader.Worker;
using Quartz;
using Serilog;

// Serilog 설정
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("=== AutoTrader Worker Service Starting ===");

    var builder = Host.CreateApplicationBuilder(args);

    // Serilog 사용
    builder.Services.AddSerilog();

    // Configuration 바인딩
    builder.Services.Configure<KisSettings>(builder.Configuration.GetSection("KIS"));
    builder.Services.Configure<TradingSettings>(builder.Configuration.GetSection("Trading"));
    builder.Services.Configure<SchedulerSettings>(builder.Configuration.GetSection("Scheduler"));
    builder.Services.Configure<WebSocketSettings>(builder.Configuration.GetSection("WebSocket"));
    builder.Services.Configure<ApiThrottlingSettings>(builder.Configuration.GetSection("ApiThrottling"));

    // HttpClient 등록
    builder.Services.AddHttpClient<IKisAuthService, KisAuthService>();
    builder.Services.AddHttpClient<IKisApiClient, KisApiClient>();
    builder.Services.AddHttpClient<TradeRankingApiService>();
    builder.Services.AddHttpClient<OrderApiService>();

    // Core Services 등록
    builder.Services.AddSingleton<IKisAuthService, KisAuthService>();
    builder.Services.AddSingleton<IApiThrottler, ApiThrottler>();
    builder.Services.AddSingleton<IKisApiClient, KisApiClient>();

    // Stock Services
    builder.Services.AddSingleton<TradeRankingApiService>();
    builder.Services.AddSingleton<ITop300StockService, Top300StockService>();

    // WebSocket Services
    builder.Services.AddSingleton<IWebSocketManager, WebSocketManager>();
    builder.Services.AddSingleton<IRealtimeDataAggregator, RealtimeDataAggregator>();

    // Trading Services
    builder.Services.AddSingleton<IConditionEvaluator, ConditionEvaluator>();
    builder.Services.AddSingleton<ICandidateTracker, CandidateTracker>();
    builder.Services.AddSingleton<OrderApiService>();
    builder.Services.AddSingleton<IOrderExecutor, OrderExecutor>();

    // Quartz.NET 스케줄러 설정
    builder.Services.AddQuartz(q =>
    {
        q.UseMicrosoftDependencyInjectionJobFactory();

        // Top 300 갱신 Job (15분마다)
        var jobKey = new JobKey("RefreshTop300Job");
        q.AddJob<RefreshTop300Job>(opts => opts.WithIdentity(jobKey));

        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity("RefreshTop300Trigger")
            .WithCronSchedule("0 */15 * * * ?")); // 15분마다
    });

    builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

    // Worker Service 등록
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    Log.Information("All services registered. Starting host...");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("=== AutoTrader Worker Service Stopped ===");
    Log.CloseAndFlush();
}
