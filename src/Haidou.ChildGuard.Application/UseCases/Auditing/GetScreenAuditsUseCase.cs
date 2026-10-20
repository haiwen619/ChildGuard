using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Haidou.ChildGuard.Application.DTOs;
using Haidou.ChildGuard.Domain.Auditing.Entities;
using Haidou.ChildGuard.Domain.Auditing.Repositories;

namespace Haidou.ChildGuard.Application.UseCases.Auditing;

public sealed class GetScreenAuditsUseCase
{
    private readonly IScreenAuditRepository _auditRepository;

    public GetScreenAuditsUseCase(IScreenAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    public async Task<IReadOnlyList<ScreenAuditRecordDto>> GetRecentAuditsAsync(int maxCount = 100)
    {
        var records = await _auditRepository.GetRecentAuditsAsync(maxCount);
        return records.Select(r => new ScreenAuditRecordDto(
            r.Id,
            r.Timestamp,
            r.ProcessName,
            r.WindowTitle,
            FormatCategory(r.Category),
            r.RiskLevel.ToString(),
            r.MatchedKeywords,
            r.Snippet,
            r.IsAcknowledged
        )).ToList();
    }

    public async Task<int> GetUnacknowledgedCountAsync()
    {
        return await _auditRepository.GetUnacknowledgedCountAsync();
    }

    private static string FormatCategory(SensitiveCategory category) => category switch
    {
        SensitiveCategory.AdultContent => "不良成人信息",
        SensitiveCategory.Gambling => "疑似涉赌内容",
        SensitiveCategory.GameCheating => "非正规游戏外挂/代充",
        SensitiveCategory.Cyberbullying => "极端言论或网络欺凌",
        SensitiveCategory.SuspiciousPay => "异常转账/虚假中奖",
        _ => "敏感不良内容"
    };
}

public sealed class AcknowledgeScreenAuditUseCase
{
    private readonly IScreenAuditRepository _auditRepository;

    public AcknowledgeScreenAuditUseCase(IScreenAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    public async Task ExecuteAsync(string auditId)
    {
        await _auditRepository.MarkAuditAcknowledgedAsync(auditId);
    }
}
