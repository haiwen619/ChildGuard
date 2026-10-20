using System;
using Haidou.ChildGuard.Domain.Common;

namespace Haidou.ChildGuard.Domain.Auditing.Entities;

public enum AuditRiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum SensitiveCategory
{
    AdultContent = 1,     // 色情低俗
    Gambling = 2,         // 涉赌彩票
    GameCheating = 3,     // 游戏租号/外挂/私服代充
    Cyberbullying = 4,    // 极端网络暴力与欺凌
    SuspiciousPay = 5     // 异常转账诱导与虚假中奖
}

public sealed class ScreenAuditRecord : Entity<string>
{
    public DateTime Timestamp { get; private set; }
    public string ProcessName { get; private set; }
    public string WindowTitle { get; private set; }
    public SensitiveCategory Category { get; private set; }
    public AuditRiskLevel RiskLevel { get; private set; }
    public string MatchedKeywords { get; private set; }
    public string Snippet { get; private set; }
    public bool IsAcknowledged { get; private set; }

    public ScreenAuditRecord(
        string id,
        DateTime timestamp,
        string processName,
        string windowTitle,
        SensitiveCategory category,
        AuditRiskLevel riskLevel,
        string matchedKeywords,
        string snippet,
        bool isAcknowledged = false)
    {
        Id = id;
        Timestamp = timestamp;
        ProcessName = string.IsNullOrWhiteSpace(processName) ? "Unknown" : processName;
        WindowTitle = string.IsNullOrWhiteSpace(windowTitle) ? "Active Window" : windowTitle;
        Category = category;
        RiskLevel = riskLevel;
        MatchedKeywords = matchedKeywords ?? string.Empty;
        Snippet = snippet ?? string.Empty;
        IsAcknowledged = isAcknowledged;
    }

    public static ScreenAuditRecord Create(
        string processName,
        string windowTitle,
        SensitiveCategory category,
        AuditRiskLevel riskLevel,
        string matchedKeywords,
        string snippet)
    {
        return new ScreenAuditRecord(
            Guid.NewGuid().ToString("N"),
            DateTime.UtcNow,
            processName,
            windowTitle,
            category,
            riskLevel,
            matchedKeywords,
            snippet,
            false);
    }

    public void MarkAcknowledged()
    {
        IsAcknowledged = true;
    }
}
