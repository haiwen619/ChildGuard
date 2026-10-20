using System.Threading.Tasks;
using Haidou.ChildGuard.Domain.Activities.Entities;
using Haidou.ChildGuard.Domain.Activities.Aggregates;

namespace Haidou.ChildGuard.Domain.Activities.Repositories;

public interface IActivityRepository
{
    Task SaveSessionAsync(ActivitySession session);
    Task<DailyTimelineAggregate> GetDailyTimelineAsync(string dateString);
    Task<int> PurgeOldSessionsAsync(int keepDays);
}
