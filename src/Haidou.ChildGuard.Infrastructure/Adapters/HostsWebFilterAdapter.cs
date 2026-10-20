using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

/// <summary>
/// 全系统级 Hosts 网页屏蔽适配器（基于 StevenBlack 开源规则结构）
/// 0 浏览器插件依赖，Chrome/Edge/Firefox 全局生效，无痕模式无法穿透
/// </summary>
public class HostsWebFilterAdapter
{
    private const string BlockMarkerBegin = "# --- BEGIN HaidouChildGuard Blocklist ---";
    private const string BlockMarkerEnd = "# --- END HaidouChildGuard Blocklist ---";

    private readonly string _hostsPath;

    public HostsWebFilterAdapter(string? hostsPath = null)
    {
        _hostsPath = hostsPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
    }

    /// <summary>
    /// 预置高频不良/成人类网址示范黑名单
    /// </summary>
    public static readonly string[] DefaultAdultDomains = new[]
    {
        "pornhub.com", "www.pornhub.com",
        "xvideos.com", "www.xvideos.com",
        "xnxx.com", "www.xnxx.com",
        "xhamster.com", "www.xhamster.com",
        "gambling.com", "bet365.com",
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
    /// 应用并激活 Hosts 过滤规则
    /// </summary>
    public async Task<bool> ApplyFilterAsync(IEnumerable<string>? customDomains = null)
    {
        try
        {
            var domains = new HashSet<string>(DefaultAdultDomains, StringComparer.OrdinalIgnoreCase);
            if (customDomains != null)
            {
                foreach (var d in customDomains)
                {
                    if (!string.IsNullOrWhiteSpace(d)) domains.Add(d.Trim());
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(BlockMarkerBegin);
            sb.AppendLine("# 本规则由 海豆·童伴守护 (Haidou ChildGuard) 自动维护，保护未成年人身心健康");
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
