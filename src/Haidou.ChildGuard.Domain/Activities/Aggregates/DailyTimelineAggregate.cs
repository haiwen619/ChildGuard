using System.Collections.Generic;
using System.Linq;
using Haidou.ChildGuard.Domain.Common;
using Haidou.ChildGuard.Domain.Activities.Entities;

namespace Haidou.ChildGuard.Domain.Activities.Aggregates;

public class DailyTimelineAggregate : IAggregateRoot
{
    public string DateString { get; }
    private readonly List<ActivitySession> _sessions = new();
    public IReadOnlyList<ActivitySession> Sessions => _sessions.AsReadOnly();

    public DailyTimelineAggregate(string dateString)
    {
        DateString = dateString;
    }

    public void AddSession(ActivitySession session)
    {
        _sessions.Add(session);
    }

    public double GetTotalActiveSeconds()
    {
        return _sessions.Where(s => !s.IsAfk).Sum(s => s.DurationSeconds);
    }

    public double GetCategoryActiveSeconds(string category)
    {
        return _sessions.Where(s => !s.IsAfk && s.Process.Category == category).Sum(s => s.DurationSeconds);
    }
}
