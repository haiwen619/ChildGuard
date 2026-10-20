using System;
using System.Linq;
using System.Threading.Tasks;
using Haidou.ChildGuard.Application.DTOs;
using Haidou.ChildGuard.Application.Interfaces;
using Haidou.ChildGuard.Domain.Auditing.Entities;
using Haidou.ChildGuard.Domain.Auditing.Repositories;

namespace Haidou.ChildGuard.Application.UseCases.Auditing;

public sealed class PerformScreenAuditUseCase
{
    private readonly IScreenOcrService _ocrService;
    private readonly ISensitiveKeywordMatcher _matcher;
    private readonly IScreenAuditRepository _auditRepository;
    private readonly ISystemNotificationService _notificationService;

    public PerformScreenAuditUseCase(
        IScreenOcrService ocrService,
        ISensitiveKeywordMatcher matcher,
        IScreenAuditRepository auditRepository,
        ISystemNotificationService notificationService)
    {
        _ocrService = ocrService;
        _matcher = matcher;
        _auditRepository = auditRepository;
        _notificationService = notificationService;
    }

    public async Task<ScreenAuditRecordDto?> ExecuteAsync(string processName, string windowTitle)
    {
        try
        {
            // 1. 纯本地抽取前台焦点窗口文字
            string ocrText = await _ocrService.RecognizeActiveWindowTextAsync();
            if (string.IsNullOrWhiteSpace(ocrText) || ocrText.Length < 2)
            {
                return null;
            }

            // 2. 毫秒级多模式字典树匹配敏感词
            var matches = _matcher.MatchText(ocrText);
            if (matches == null || matches.Count == 0)
            {
                // 未命中任何违规内容：立即丢弃 OCR 文本，保证 100% 隐私零留存
                return null;
            }

            // 3. 命中违规内容：分析最高风险等级与关键词
            var maxRisk = matches.Max(m => m.RiskLevel);
            var primaryCategory = matches.OrderByDescending(m => m.RiskLevel).First().Category;
            var keywords = string.Join(", ", matches.Select(m => m.Keyword).Distinct());
            var snippet = matches.First().Snippet;

            var record = ScreenAuditRecord.Create(
                processName,
                windowTitle,
                primaryCategory,
                maxRisk,
                keywords,
                snippet
            );

            // 4. 持久化至本地 SQLite 数据库
            await _auditRepository.SaveAuditRecordAsync(record);

            // 5. 触发温和的健康护航提示
            string categoryDisplay = primaryCategory switch
            {
                SensitiveCategory.AdultContent => "不良成人信息",
                SensitiveCategory.Gambling => "疑似涉赌内容",
                SensitiveCategory.GameCheating => "非正规游戏外挂/代充",
                SensitiveCategory.Cyberbullying => "极端言论或网络欺凌",
                SensitiveCategory.SuspiciousPay => "异常转账/虚假中奖",
                _ => "敏感不良内容"
            };
            _notificationService.ShowAuditWarningAlert(categoryDisplay, processName);

            return new ScreenAuditRecordDto(
                record.Id,
                record.Timestamp,
                record.ProcessName,
                record.WindowTitle,
                categoryDisplay,
                record.RiskLevel.ToString(),
                record.MatchedKeywords,
                record.Snippet,
                record.IsAcknowledged
            );
        }
        catch
        {
            // 离线审计引擎容错保证，绝不引发宿主崩溃
            return null;
        }
    }
}
