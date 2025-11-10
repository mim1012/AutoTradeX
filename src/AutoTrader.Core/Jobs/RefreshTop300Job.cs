using AutoTrader.Core.Services.Stock;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AutoTrader.Core.Jobs;

/// <summary>
/// Top 300 종목 갱신 Job (Quartz.NET)
/// 스케줄: 1분마다 실행 (Cron: "0 */1 * * * ?")
/// </summary>
[DisallowConcurrentExecution] // 동시 실행 방지
public class RefreshTop300Job : IJob
{
    private readonly ITop300StockService _stockService;
    private readonly ILogger<RefreshTop300Job> _logger;

    public RefreshTop300Job(
        ITop300StockService stockService,
        ILogger<RefreshTop300Job> logger)
    {
        _stockService = stockService ?? throw new ArgumentNullException(nameof(stockService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Job 실행
    /// </summary>
    public async Task Execute(IJobExecutionContext context)
    {
        var jobKey = context.JobDetail.Key;
        _logger.LogInformation("RefreshTop300Job started (Job: {JobKey})", jobKey);

        try
        {
            // Top 300 종목 갱신
            await _stockService.RefreshTop300Async();

            _logger.LogInformation("RefreshTop300Job completed successfully (Cached: {Count} stocks)",
                _stockService.CachedStockCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RefreshTop300Job failed");

            // JobExecutionException을 던지면 Quartz가 재시도
            var jobEx = new JobExecutionException(ex)
            {
                RefireImmediately = false // 즉시 재시도 하지 않음
            };

            throw jobEx;
        }
    }
}
