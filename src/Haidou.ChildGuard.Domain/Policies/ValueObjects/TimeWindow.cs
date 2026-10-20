using System;
using System.Collections.Generic;
using Haidou.ChildGuard.Domain.Common;

namespace Haidou.ChildGuard.Domain.Policies.ValueObjects;

public class TimeWindow : ValueObject
{
    public TimeSpan StartTime { get; }
    public TimeSpan EndTime { get; }

    public TimeWindow(TimeSpan startTime, TimeSpan endTime)
    {
        StartTime = startTime;
        EndTime = endTime;
    }

    public bool IsInWindow(TimeSpan time)
    {
        if (StartTime <= EndTime)
        {
            return time >= StartTime && time <= EndTime;
        }
        return time >= StartTime || time < EndTime;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return StartTime;
        yield return EndTime;
    }
}
