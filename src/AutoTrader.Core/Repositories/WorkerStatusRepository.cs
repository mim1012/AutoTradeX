using AutoTrader.Core.Data;
using AutoTrader.Core.Models.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutoTrader.Core.Repositories;

/// <summary>
/// Worker Service 상태 정보 관리 Repository
/// - VPS에서 실행 중인 Worker Service의 상태 추적
/// - 로컬 PC UI에서 Worker 상태 모니터링
/// </summary>
public class WorkerStatusRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkerStatusRepository> _logger;

    // Heartbeat 유효 시간 (30초)
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(30);

    public WorkerStatusRepository(AppDbContext context, ILogger<WorkerStatusRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 가장 최근 Worker 상태 정보 조회 (ID=1 레코드)
    /// </summary>
    public async Task<WorkerStatus?> GetLatestAsync()
    {
        try
        {
            // WorkerStatus는 단일 레코드 (ID=1)로 관리
            var status = await _context.WorkerStatus
                .FirstOrDefaultAsync(ws => ws.Id == 1);

            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest worker status");
            throw;
        }
    }

    /// <summary>
    /// Worker 상태 업데이트 (Upsert: 없으면 생성, 있으면 업데이트)
    /// </summary>
    public async Task UpdateStatusAsync(WorkerStatus status)
    {
        if (status == null)
            throw new ArgumentNullException(nameof(status));

        try
        {
            var existing = await _context.WorkerStatus
                .FirstOrDefaultAsync(ws => ws.Id == 1);

            if (existing == null)
            {
                // 최초 생성
                status.Id = 1;
                status.UpdatedAt = DateTime.UtcNow;
                _context.WorkerStatus.Add(status);

                _logger.LogInformation("Worker status created: {Status}", status);
            }
            else
            {
                // 기존 레코드 업데이트
                existing.IsRunning = status.IsRunning;
                existing.LastHeartbeat = DateTime.UtcNow;
                existing.Top300Count = status.Top300Count;
                existing.CandidateCount = status.CandidateCount;
                existing.OrderCount = status.OrderCount;
                existing.LastLog = status.LastLog;
                existing.UpdatedAt = DateTime.UtcNow;

                _context.WorkerStatus.Update(existing);

                _logger.LogDebug("Worker status updated: {Status}", existing);
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update worker status");
            throw;
        }
    }

    /// <summary>
    /// Worker Heartbeat 업데이트 (빠른 업데이트용)
    /// </summary>
    public async Task UpdateHeartbeatAsync(string? lastLog = null)
    {
        try
        {
            var existing = await _context.WorkerStatus
                .FirstOrDefaultAsync(ws => ws.Id == 1);

            if (existing == null)
            {
                // 최초 생성
                var newStatus = new WorkerStatus
                {
                    Id = 1,
                    IsRunning = true,
                    LastHeartbeat = DateTime.UtcNow,
                    LastLog = lastLog,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.WorkerStatus.Add(newStatus);
            }
            else
            {
                // Heartbeat 및 로그만 업데이트
                existing.LastHeartbeat = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(lastLog))
                    existing.LastLog = lastLog;

                _context.WorkerStatus.Update(existing);
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update heartbeat");
            throw;
        }
    }

    /// <summary>
    /// Worker가 살아있는지 확인 (마지막 Heartbeat 기준)
    /// </summary>
    public async Task<bool> IsWorkerAliveAsync()
    {
        try
        {
            var status = await GetLatestAsync();

            if (status == null)
                return false;

            // 마지막 Heartbeat가 30초 이내인지 확인
            var timeSinceHeartbeat = DateTime.UtcNow - status.LastHeartbeat;
            return timeSinceHeartbeat < HeartbeatTimeout && status.IsRunning;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if worker is alive");
            return false;
        }
    }

    /// <summary>
    /// Worker 실행 상태 변경 (시작/중지)
    /// </summary>
    public async Task SetRunningStatusAsync(bool isRunning, string? logMessage = null)
    {
        try
        {
            var existing = await _context.WorkerStatus
                .FirstOrDefaultAsync(ws => ws.Id == 1);

            if (existing == null)
            {
                // 최초 생성
                var newStatus = new WorkerStatus
                {
                    Id = 1,
                    IsRunning = isRunning,
                    LastHeartbeat = DateTime.UtcNow,
                    LastLog = logMessage ?? (isRunning ? "Worker started" : "Worker stopped"),
                    UpdatedAt = DateTime.UtcNow
                };
                _context.WorkerStatus.Add(newStatus);
            }
            else
            {
                // 실행 상태 변경
                existing.IsRunning = isRunning;
                existing.LastHeartbeat = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(logMessage))
                    existing.LastLog = logMessage;

                _context.WorkerStatus.Update(existing);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Worker running status changed to: {IsRunning}", isRunning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set worker running status");
            throw;
        }
    }

    /// <summary>
    /// Worker 통계 업데이트
    /// </summary>
    public async Task UpdateMetricsAsync(int top300Count, int candidateCount, int orderCount)
    {
        try
        {
            var existing = await _context.WorkerStatus
                .FirstOrDefaultAsync(ws => ws.Id == 1);

            if (existing == null)
            {
                // 최초 생성
                var newStatus = new WorkerStatus
                {
                    Id = 1,
                    IsRunning = true,
                    LastHeartbeat = DateTime.UtcNow,
                    Top300Count = top300Count,
                    CandidateCount = candidateCount,
                    OrderCount = orderCount,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.WorkerStatus.Add(newStatus);
            }
            else
            {
                // 통계 업데이트
                existing.Top300Count = top300Count;
                existing.CandidateCount = candidateCount;
                existing.OrderCount = orderCount;
                existing.LastHeartbeat = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;

                _context.WorkerStatus.Update(existing);
            }

            await _context.SaveChangesAsync();

            _logger.LogDebug("Worker metrics updated: Top300={Top300}, Candidates={Candidates}, Orders={Orders}",
                top300Count, candidateCount, orderCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update worker metrics");
            throw;
        }
    }

    /// <summary>
    /// 마지막 Heartbeat 이후 경과 시간 (초)
    /// </summary>
    public async Task<int> GetSecondsSinceLastHeartbeatAsync()
    {
        try
        {
            var status = await GetLatestAsync();

            if (status == null)
                return int.MaxValue; // Worker 상태가 없으면 무한대

            var timeSince = DateTime.UtcNow - status.LastHeartbeat;
            return (int)timeSince.TotalSeconds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get seconds since last heartbeat");
            return int.MaxValue;
        }
    }
}
