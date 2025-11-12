using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using AutoTrader.Core.Services.Stock;
using AutoTrader.UI.Services;
using AutoTrader.UI.ViewModels;
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

                // HttpClient 등록
                services.AddHttpClient();

                // Core 서비스 등록
                services.AddSingleton<AutoTrader.Core.Services.Auth.IKisAuthService, AutoTrader.Core.Services.Auth.KisAuthService>();
                services.AddSingleton<AutoTrader.Core.Services.Api.IKisApiClient, AutoTrader.Core.Services.Api.KisApiClient>();
                services.AddSingleton<AutoTrader.Core.Services.Throttling.IApiThrottler, AutoTrader.Core.Services.Throttling.ApiThrottler>();
                services.AddSingleton<AutoTrader.Core.Services.Stock.TradeRankingApiService>();
                services.AddSingleton<ITop300StockService, AutoTrader.Core.Services.Stock.Top300StockService>();

                // UI 서비스 등록
                services.AddTransient<ITradingService>(sp =>
                {
                    // ITop300StockService 주입
                    var top300Service = sp.GetService<ITop300StockService>();
                    return new TradingService(top300Service);
                });

                // ViewModels 등록
                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host!.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();

        // MainViewModel을 DI에서 가져와서 DataContext 설정
        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        mainWindow.DataContext = mainViewModel;

        mainWindow.Show();

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

