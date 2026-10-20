using System;
using System.Collections.Generic;
using Haidou.ChildGuard.Domain.Common;
using Haidou.ChildGuard.Domain.Policies.ValueObjects;

namespace Haidou.ChildGuard.Domain.Policies.Aggregates;

public class GuardianPolicyAggregate : Entity<int>, IAggregateRoot
{
    public int DailyTotalLimitMinutes { get; private set; }
    public int SingleGameLimitMinutes { get; private set; }
    public TimeWindow CurfewWindow { get; private set; }
    public int RestIntervalMinutes { get; private set; }
    public int RestDurationMinutes { get; private set; }

    private readonly HashSet<string> _blockedProcesses = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _gameProcesses = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> BlockedProcesses => _blockedProcesses;
    public IReadOnlyCollection<string> GameProcesses => _gameProcesses;

    public GuardianPolicyAggregate(
        int id,
        int dailyTotalLimitMinutes,
        int singleGameLimitMinutes,
        TimeWindow curfewWindow,
        int restIntervalMinutes = 45,
        int restDurationMinutes = 5)
    {
        Id = id;
        DailyTotalLimitMinutes = dailyTotalLimitMinutes;
        SingleGameLimitMinutes = singleGameLimitMinutes;
        CurfewWindow = curfewWindow;
        RestIntervalMinutes = restIntervalMinutes;
        RestDurationMinutes = restDurationMinutes;

        _blockedProcesses.Add("cheatengine.exe");
        _blockedProcesses.Add("cheatengine-x86_64.exe");

        _gameProcesses.Add("genshinimpact.exe");
        _gameProcesses.Add("yuanshen.exe");
        _gameProcesses.Add("starrail.exe");
        _gameProcesses.Add("leagueclient.exe");
        _gameProcesses.Add("crossfire.exe");
        _gameProcesses.Add("steam.exe");
    }

    public void UpdateLimits(int totalDailyMinutes, int singleGameMinutes)
    {
        DailyTotalLimitMinutes = Math.Max(0, totalDailyMinutes);
        SingleGameLimitMinutes = Math.Max(0, singleGameMinutes);
    }

    public void UpdateCurfew(TimeSpan start, TimeSpan end)
    {
        CurfewWindow = new TimeWindow(start, end);
    }

    public bool IsBlocked(string processName) => _blockedProcesses.Contains(processName);
    public bool IsGame(string processName) => _gameProcesses.Contains(processName);
}
