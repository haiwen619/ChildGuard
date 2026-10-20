using System;
using System.Threading;
using System.Threading.Tasks;
using Haidou.ChildGuard.Application.UseCases.Policies;
using Haidou.ChildGuard.Domain.Activities.Repositories;
using Haidou.ChildGuard.Domain.Policies.Services;
using Haidou.ChildGuard.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Haidou.ChildGuard.Service;

/// <summary>
/// Session 0 系统守护服务进程（DDD 架构应用层与基础设施接入）
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly DatabaseContext _dbContext;
    private readonly IActivityRepository _activityRepository;
    private readonly ManagePolicyUseCase _managePolicyUseCase;
    private DateTime _lastPurgeTime = DateTime.MinValue;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _dbContext = new DatabaseContext();
        _activityRepository = new SqliteActivityRepository(_dbContext);
        var policyRepository = new SqlitePolicyRepository(_dbContext);
        _managePolicyUseCase = new ManagePolicyUseCase(policyRepository);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("海豆·童伴守护 (Haidou ChildGuard) Session 0 守护服务 [DDD驱动] 已就绪。");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // 1. 每隔 6 小时自动执行仓储层滚动擦除（保留 7 天滚动记录）
                if ((now - _lastPurgeTime).TotalHours >= 6)
                {
                    int deletedRows = await _activityRepository.PurgeOldSessionsAsync(keepDays: 7);
                    _logger.LogInformation("DDD 仓储层执行本地滚动清理，已安全擦除过期会话记录数: {Count}", deletedRows);
                    _lastPurgeTime = now;
                }

                // 2. 策略引擎巡检：通过应用层用例评估就寝与限制状态
                var decision = await _managePolicyUseCase.EvaluateAsync("System", 0);
                if (decision == PolicyDecision.CurfewLock)
                {
                    _logger.LogWarning("当前处于就寝断屏管控时段 (Curfew Active)");
                }

                // 3. 守护巡检心跳（每 5 秒一次）
                await Task.Delay(5000, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "守护服务执行巡检时发生异常");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    public override void Dispose()
    {
        _dbContext.Dispose();
        base.Dispose();
    }
}
