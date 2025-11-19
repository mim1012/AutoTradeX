using AutoTrader.Core.Configuration;
using AutoTrader.Core.Data;
using AutoTrader.Core.Jobs;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Services.Api;
using AutoTrader.Core.Services.Auth;
using AutoTrader.Core.Services.Market;
using AutoTrader.Core.Services.Realtime;
using AutoTrader.Core.Services.Security;
using AutoTrader.Core.Services.Stock;
using AutoTrader.Core.Services.Throttling;
using AutoTrader.Core.Services.Trading;
using AutoTrader.Core.Services.WebSocket;
using AutoTrader.Worker;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;

// Serilog 설정
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/autotrader-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting AutoTradeX Worker Service");

    var builder = Host.CreateApplicationBuilder(args);

    // Serilog 등록
    builder.Services.AddSerilog();

    // Configuration 설정 바인딩
    builder.Services.Configure<KisSettings>(builder.Configuration.GetSection("KIS"));
    builder.Services.Configure<TradingSettings>(builder.Configuration.GetSection("Trading"));
    builder.Services.Configure<SchedulerSettings>(builder.Configuration.GetSection("Scheduler"));
    builder.Services.Configure<WebSocketSettings>(builder.Configuration.GetSection("WebSocket"));
    builder.Services.Configure<ApiThrottlingSettings>(builder.Configuration.GetSection("ApiThrottling"));

    // Database 설정 (SQLite, MySQL, PostgreSQL)
    var databaseType = builder.Configuration.GetValue<string>("ConnectionStrings:DatabaseType") ?? "SQLite";
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        switch (databaseType.ToUpper())
        {
            case "SQLITE":
                options.UseSqlite("Data Source=autotrader.db");
                Log.Information("Using SQLite database: autotrader.db");
                break;
            case "MYSQL":
                var mysqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                var serverVersion = ServerVersion.AutoDetect(mysqlConnectionString);
                options.UseMySql(mysqlConnectionString, serverVersion);
                Log.Information("Using MySQL database: {ConnectionString}", mysqlConnectionString?.Replace("Password=", "Password=***"));
                break;
            case "POSTGRESQL":
            case "POSTGRES":
                var postgresConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
                options.UseNpgsql(postgresConnectionString);
                Log.Information("Using PostgreSQL database: {ConnectionString}", postgresConnectionString?.Replace("Password=", "Password=***"));
                break;
            default:
                throw new InvalidOperationException($"Unsupported database type: {databaseType}. Use 'SQLite', 'MySQL', or 'PostgreSQL'.");
        }
    });

    // Security Services
    builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

    // Repositories
    builder.Services.AddScoped<AccountRepository>();
    builder.Services.AddScoped<ConditionSetRepository>();
    builder.Services.AddScoped<WorkerStatusRepository>();

    // HttpClient 등록
    builder.Services.AddHttpClient<IKisApiClient, KisApiClient>();

    // 인증 서비스
    builder.Services.AddSingleton<IKisAuthService, KisAuthService>();

    // API Throttling
    builder.Services.AddSingleton<IApiThrottler, ApiThrottler>();

    // API 클라이언트
    builder.Services.AddSingleton<IKisApiClient, KisApiClient>();

    // API 서비스
    builder.Services.AddSingleton<TradeRankingApiService>();
    builder.Services.AddSingleton<BalanceApiService>();

    // Stock 서비스
    builder.Services.AddSingleton<ITop300StockService, Top300StockService>();

    // Historical Data 서비스 (MovingAverage, PriceComparison 조건용)
    builder.Services.AddSingleton<IHistoricalDataService, HistoricalDataService>();

    // WebSocket 서비스
    builder.Services.AddSingleton<IWebSocketManager, WebSocketManager>();

    // Realtime 서비스
    builder.Services.AddSingleton<IRealtimeDataAggregator, RealtimeDataAggregator>();

    // Snapshot 서비스 (다중 시점 스캔용)
    builder.Services.AddSingleton<ISnapshotDataService, SnapshotDataService>();
    builder.Services.AddSingleton<IMultiPointValidator, MultiPointValidator>();

    // Trading 서비스
    builder.Services.AddSingleton<IConditionEvaluator, ConditionEvaluator>();
    builder.Services.AddSingleton<ICandidateTracker, CandidateTracker>();
    builder.Services.AddSingleton<OrderApiService>();
    builder.Services.AddSingleton<IOrderExecutor, OrderExecutor>();

    // Quartz.NET 설정
    builder.Services.AddQuartz(q =>
    {
        // RefreshTop300Job 등록
        var jobKey = new JobKey("RefreshTop300Job");
        q.AddJob<RefreshTop300Job>(opts => opts.WithIdentity(jobKey));

        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity("RefreshTop300Job-trigger")
            .WithCronSchedule("0 */1 * * * ?") // 1분마다
            .StartNow());
    });

    builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

    // Main Worker Service 등록
    builder.Services.AddHostedService<TradingWorker>();

    var host = builder.Build();

    // 데이터베이스 초기화
    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DbInitializer.InitializeAsync(dbContext);
        Log.Information("Database initialized successfully");
    }

    Log.Information("AutoTradeX Worker Service configured successfully");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AutoTradeX Worker Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
