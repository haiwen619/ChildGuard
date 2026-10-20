using System;

namespace Haidou.ChildGuard.Application.DTOs;

public record ActivitySessionDto(
    uint ProcessId,
    string ProcessName,
    string WindowTitle,
    DateTime StartTimeUtc,
    double DurationSeconds,
    bool IsAfk,
    string Category
);

public record DailyAppUsageDto(
    string ProcessName,
    string Category,
    double TotalActiveSeconds,
    double TotalAfkSeconds,
    int LaunchCount
);

public record DailyReportDto(
    string DateString,
    double TotalActiveSeconds,
    double TotalAfkSeconds,
    DailyAppUsageDto[] TopApps
);

public record PolicyConfigDto(
    int DailyTotalLimitMinutes,
    int SingleGameLimitMinutes,
    TimeSpan CurfewStart,
    TimeSpan CurfewEnd,
    int RestIntervalMinutes,
    int RestDurationMinutes
);

public record TotpQrResultDto(
    string SecretBase32,
    string QrCodePngBase64,
    string OtpAuthUri
);

public record ScreenAuditRecordDto(
    string Id,
    DateTime Timestamp,
    string ProcessName,
    string WindowTitle,
    string CategoryName,
    string RiskLevelName,
    string MatchedKeywords,
    string Snippet,
    bool IsAcknowledged
);
