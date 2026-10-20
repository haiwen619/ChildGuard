using System.Collections.Generic;
using System.Threading.Tasks;
using Haidou.ChildGuard.Domain.Auditing.Entities;

namespace Haidou.ChildGuard.Domain.Auditing.Repositories;

public interface IScreenAuditRepository
{
    Task SaveAuditRecordAsync(ScreenAuditRecord record);
    Task<IReadOnlyList<ScreenAuditRecord>> GetRecentAuditsAsync(int maxCount = 100);
    Task<int> GetUnacknowledgedCountAsync();
    Task MarkAuditAcknowledgedAsync(string auditId);
    Task PurgeOldAuditsAsync(int retentionDays = 7);
}
