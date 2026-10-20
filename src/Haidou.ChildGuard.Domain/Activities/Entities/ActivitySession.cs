using System;
using Haidou.ChildGuard.Domain.Common;
using Haidou.ChildGuard.Domain.Activities.ValueObjects;

namespace Haidou.ChildGuard.Domain.Activities.Entities;

public class ActivitySession : Entity<long>
{
    public uint ProcessId { get; private set; }
    public ProcessInfo Process { get; private set; }
    public string WindowTitle { get; private set; }
    public DateTime StartTimeUtc { get; private set; }
    public double DurationSeconds { get; private set; }
    public bool IsAfk { get; private set; }

    public ActivitySession(long id, uint processId, ProcessInfo process, string windowTitle, DateTime startTimeUtc, double durationSeconds, bool isAfk)
    {
        Id = id;
        ProcessId = processId;
        Process = process;
        WindowTitle = windowTitle;
        StartTimeUtc = startTimeUtc;
        DurationSeconds = durationSeconds;
        IsAfk = isAfk;
    }

    public void ExtendDuration(double additionalSeconds)
    {
        if (additionalSeconds > 0)
        {
            DurationSeconds += additionalSeconds;
        }
    }
}
