using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public record BlockedRuleModel(
    string Domain,
    string Category,
    bool IsCustom,
    DateTime AddedAtUtc
);

/// <summary>
/// 全系统级 Hosts 网页屏蔽适配器（支持内置开源不良规则 + 家长自定义黑名单）
/// 0 浏览器插件依赖，Chrome/Edge/Firefox 全局生效，无痕模式无法穿透
/// </summary>
public class HostsWebFilterAdapter
{
    private const string BlockMarkerBegin = "# --- BEGIN HaidouChildGuard Blocklist ---";
    private const string BlockMarkerEnd = "# --- END HaidouChildGuard Blocklist ---";

    private readonly string _hostsPath;
    private readonly string _customRulesPath;

    public HostsWebFilterAdapter(string? hostsPath = null, string? customRulesPath = null)
    {
        _hostsPath = hostsPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
        
        if (string.IsNullOrEmpty(customRulesPath))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string baseFolder = Path.Combine(appData, "HaidouChildGuard");
            Directory.CreateDirectory(baseFolder);
            _customRulesPath = Path.Combine(baseFolder, "custom_blocked_domains.json");
        }
        else
        {
            _customRulesPath = customRulesPath;
        }
    }

    /// <summary>
    /// 预置高频不良/成人/涉赌网址核心黑名单
    /// </summary>
    public static readonly string[] DefaultAdultDomains = new[]
    {
        "pornhub.com", "www.pornhub.com",
        "xvideos.com", "www.xvideos.com",
        "xnxx.com", "www.xnxx.com",
        "xhamster.com", "www.xhamster.com",
        "gambling.com", "www.gambling.com",
        "bet365.com", "www.bet365.com",
        "biquge.com.co", "unsafe-gambling-site.com"
    };

    /// <summary>
    /// 检查当前是否已启用 Hosts 网页屏蔽
    /// </summary>
    public bool IsFilterActive()
    {
        if (!File.Exists(_hostsPath)) return false;
        try
        {
            string content = File.ReadAllText(_hostsPath);
            return content.Contains(BlockMarkerBegin);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取家长自定义屏蔽域名列表
    /// </summary>
    public List<string> GetCustomDomains()
    {
        if (!File.Exists(_customRulesPath)) return new List<string>();

        try
        {
            string json = File.ReadAllText(_customRulesPath, Encoding.UTF8);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// 保存家长自定义屏蔽域名列表
    /// </summary>
    public void SaveCustomDomains(IEnumerable<string> domains)
    {
        try
        {
            var clean = domains
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim().ToLowerInvariant())
                .Distinct()
                .ToList();

            string json = JsonSerializer.Serialize(clean, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_customRulesPath, json, Encoding.UTF8);
        }
        catch {}
    }

    /// <summary>
    /// 获取所有生效规则的视图模型（合并内置与自定义）
    /// </summary>
    public List<BlockedRuleModel> GetAllRules()
    {
        var result = new List<BlockedRuleModel>();

        // 1. 家长自定义规则
        var custom = GetCustomDomains();
        foreach (var c in custom)
        {
            result.Add(new BlockedRuleModel(c, "家长自定义屏蔽", true, DateTime.UtcNow));
        }

        // 2. 内置核心规则
        foreach (var d in DefaultAdultDomains)
        {
            if (!custom.Contains(d, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(new BlockedRuleModel(d, "内置高频不良与涉赌防护", false, DateTime.UtcNow));
            }
        }

        return result;
    }

    /// <summary>
    /// 添加自定义域名并同步更新 Hosts
    /// </summary>
    public async Task<bool> AddCustomDomainAsync(string rawDomain)
    {
        string normalized = NormalizeDomain(rawDomain);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        var custom = GetCustomDomains();
        if (!custom.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            custom.Add(normalized);
            SaveCustomDomains(custom);

            if (IsFilterActive())
            {
                await ApplyFilterAsync();
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// 删除自定义域名并同步更新 Hosts
    /// </summary>
    public async Task<bool> RemoveCustomDomainAsync(string rawDomain)
    {
        string normalized = NormalizeDomain(rawDomain);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        var custom = GetCustomDomains();
        bool removed = custom.RemoveAll(d => string.Equals(d, normalized, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
        {
            SaveCustomDomains(custom);
            if (IsFilterActive())
            {
                await ApplyFilterAsync();
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// 域名清洗规范化（去除 http://, https://, 路径及前缀斜杠）
    /// </summary>
    public static string NormalizeDomain(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        string clean = input.Trim().ToLowerInvariant();

        if (clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(7);
        if (clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(8);

        int slashIdx = clean.IndexOf('/');
        if (slashIdx >= 0) clean = clean.Substring(0, slashIdx);

        int colonIdx = clean.IndexOf(':');
        if (colonIdx >= 0) clean = clean.Substring(0, colonIdx);

        return clean.Trim();
    }

    /// <summary>
    /// 应用并激活 Hosts 过滤规则（内置 + 自定义）
    /// </summary>
    public async Task<bool> ApplyFilterAsync()
    {
        try
        {
            var domains = new HashSet<string>(DefaultAdultDomains, StringComparer.OrdinalIgnoreCase);
            var custom = GetCustomDomains();
            foreach (var d in custom)
            {
                if (!string.IsNullOrWhiteSpace(d))
                {
                    domains.Add(d);
                    if (!d.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    {
                        domains.Add("www." + d);
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(BlockMarkerBegin);
            sb.AppendLine("# 本规则由 海豆·童伴守护 (Haidou ChildGuard) 自动维护，保护未成年人身心健康");
            sb.AppendLine($"# 更新时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | 屏蔽域名总数: {domains.Count}");
            foreach (var domain in domains)
            {
                sb.AppendLine($"127.0.0.1 {domain}");
                sb.AppendLine($"::1 {domain}");
            }
            sb.AppendLine(BlockMarkerEnd);

            string originalContent = File.Exists(_hostsPath) ? await File.ReadAllTextAsync(_hostsPath) : "";
            string cleanedContent = RemoveFilterBlock(originalContent);

            string finalContent = cleanedContent.TrimEnd() + Environment.NewLine + Environment.NewLine + sb.ToString();
            await File.WriteAllTextAsync(_hostsPath, finalContent, Encoding.UTF8);

            return true;
        }
        catch
        {
            // 在普通标准用户权限下若无管理员权限修改 hosts，将安全捕获并返回 false
            return false;
        }
    }

    /// <summary>
    /// 移除 Hosts 过滤规则
    /// </summary>
    public async Task<bool> RemoveFilterAsync()
    {
        try
        {
            if (!File.Exists(_hostsPath)) return true;

            string content = await File.ReadAllTextAsync(_hostsPath);
            string cleaned = RemoveFilterBlock(content);
            await File.WriteAllTextAsync(_hostsPath, cleaned, Encoding.UTF8);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string RemoveFilterBlock(string content)
    {
        int startIndex = content.IndexOf(BlockMarkerBegin, StringComparison.Ordinal);
        if (startIndex < 0) return content;

        int endIndex = content.IndexOf(BlockMarkerEnd, StringComparison.Ordinal);
        if (endIndex < 0) return content.Substring(0, startIndex);

        string before = content.Substring(0, startIndex);
        string after = content.Substring(endIndex + BlockMarkerEnd.Length);
        return before.TrimEnd() + Environment.NewLine + after.TrimStart();
    }
}
