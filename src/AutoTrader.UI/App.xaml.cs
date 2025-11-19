using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using AutoTrader.Core.Configuration;
using AutoTrader.Core.Data;
using AutoTrader.Core.Repositories;
using AutoTrader.Core.Services.Security;
using AutoTrader.Core.Services.Stock;
using AutoTrader.UI.Services;
using AutoTrader.UI.ViewModels;
using AutoTrader.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AutoTrader.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                // appsettings.json 로드 (bin 폴더 기준)
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration 등록
                var configuration = context.Configuration;

                // Configuration 섹션 바인딩
                services.Configure<KisSettings>(configuration.GetSection("KIS"));
                services.Configure<AutoTrader.Core.Configuration.ApiThrottlingSettings>(configuration.GetSection("ApiThrottling"));

                // HttpClient 등록
                services.AddHttpClient();

                // DbContext 등록 (SQLite, MySQL, PostgreSQL)
                var databaseType = configuration.GetValue<string>("ConnectionStrings:DatabaseType") ?? "SQLite";
                services.AddDbContext<AppDbContext>(options =>
                {
                    switch (databaseType.ToUpper())
                    {
                        case "SQLITE":
                            options.UseSqlite("Data Source=autotrader.db");
                            break;
                        case "MYSQL":
                            var mysqlConnectionString = configuration.GetConnectionString("DefaultConnection");
                            var serverVersion = ServerVersion.AutoDetect(mysqlConnectionString);
                            options.UseMySql(mysqlConnectionString, serverVersion);
                            break;
                        case "POSTGRESQL":
                        case "POSTGRES":
                            var postgresConnectionString = configuration.GetConnectionString("DefaultConnection");
                            options.UseNpgsql(postgresConnectionString);
                            break;
                        default:
                            throw new InvalidOperationException($"Unsupported database type: {databaseType}. Use 'SQLite', 'MySQL', or 'PostgreSQL'.");
                    }
                });

                // Security Services
                services.AddSingleton<IEncryptionService, EncryptionService>();

                // Repository 등록
                services.AddScoped<AccountRepository>();
                services.AddScoped<ConditionSetRepository>();
                services.AddScoped<WorkerStatusRepository>();

                // Core 서비스 등록
                services.AddSingleton<AutoTrader.Core.Services.Auth.IKisAuthService, AutoTrader.Core.Services.Auth.KisAuthService>();
                services.AddSingleton<AutoTrader.Core.Services.Api.IKisApiClient, AutoTrader.Core.Services.Api.KisApiClient>();
                services.AddSingleton<AutoTrader.Core.Services.Throttling.IApiThrottler, AutoTrader.Core.Services.Throttling.ApiThrottler>();
                services.AddSingleton<AutoTrader.Core.Services.Stock.TradeRankingApiService>();
                services.AddSingleton<ITop300StockService, AutoTrader.Core.Services.Stock.Top300StockService>();
                services.AddSingleton<AutoTrader.Core.Services.Api.BalanceApiService>();
                services.AddSingleton<AutoTrader.Core.Services.Schedule.IMarketScheduleService, AutoTrader.Core.Services.Schedule.MarketScheduleService>();

                // UI 서비스 등록
                services.AddTransient<ITradingService>(sp =>
                {
                    // 모든 필요한 서비스 주입
                    var top300Service = sp.GetService<ITop300StockService>();
                    var balanceService = sp.GetService<AutoTrader.Core.Services.Api.BalanceApiService>();
                    var marketSchedule = sp.GetService<AutoTrader.Core.Services.Schedule.IMarketScheduleService>();
                    return new TradingService(top300Service, balanceService, marketSchedule);
                });

                // ViewModels 등록
                services.AddTransient<MainDashboardViewModel>();
                services.AddTransient<MainViewModel>(sp =>
                {
                    var tradingService = sp.GetRequiredService<ITradingService>();
                    var accountRepo = sp.GetRequiredService<AccountRepository>();
                    var conditionRepo = sp.GetRequiredService<ConditionSetRepository>();
                    var workerStatusRepo = sp.GetRequiredService<WorkerStatusRepository>();
                    return new MainViewModel(tradingService, accountRepo, conditionRepo, workerStatusRepo);
                });

                // Views 등록
                services.AddTransient<MainDashboard>();
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host!.StartAsync();

        // DB 초기화
        using (var scope = _host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await DbInitializer.InitializeAsync(dbContext);
        }

        // MainDashboard를 시작 화면으로 표시
        var mainDashboard = _host.Services.GetRequiredService<MainDashboard>();
        var mainDashboardViewModel = _host.Services.GetRequiredService<MainDashboardViewModel>();
        mainDashboard.DataContext = mainDashboardViewModel;
        mainDashboard.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        using (_host)
        {
            await _host!.StopAsync();
        }

        base.OnExit(e);
    }
}

