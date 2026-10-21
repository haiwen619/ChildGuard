using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Haidou.ChildGuard.App.Services;

/// <summary>
/// 智能国家位置与 IP 语言探测器
/// 负责根据网络 IP、操作系统地理区域 (RegionInfo) 与 CultureInfo 智能匹配适宜的语言
/// </summary>
public static class GeoLanguageDetector
{
    private static readonly string CacheFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HaidouChildGuard",
        "geo_cache.json");

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3.5)
    };

    public record GeoLocationResult(string CountryCode, string CountryName, string MatchedLanguage, string Source);

    /// <summary>
    /// 0 毫秒同步极速推断（纯本地，离线立即可用）
    /// </summary>
    public static GeoLocationResult DetectFromSystemRegion()
    {
        try
        {
            // 1. 读取 Windows 区域设置
            var region = RegionInfo.CurrentRegion;
            string countryCode = region?.TwoLetterISORegionName?.ToUpperInvariant() ?? "CN";
            string countryName = region?.DisplayName ?? "China";

            // 2. 检查 UI 文化
            string uiCulture = CultureInfo.CurrentUICulture.Name;
            bool isChineseRegion = countryCode is "CN" or "HK" or "TW" or "MO" || uiCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

            string matchedLang = isChineseRegion ? "zh-CN" : "en-US";
            return new GeoLocationResult(countryCode, countryName, matchedLang, "SystemRegion");
        }
        catch
        {
            return new GeoLocationResult("CN", "China", "zh-CN", "DefaultFallback");
        }
    }

    /// <summary>
    /// 异步通过网络公网 IP 与地理位置服务探测真实所属国家
    /// 优先调用高可用、无鉴权的地理定位 API，带本地持久化缓存
    /// </summary>
    public static async Task<GeoLocationResult> DetectFromIpAsync(CancellationToken cancellationToken = default)
    {
        // 1. 尝试从本地缓存读取（如果 24 小时内已检测过，直接返回避免重复发网络请求）
        var cached = TryReadCache();
        if (cached != null)
        {
            return cached;
        }

        // 2. 尝试从主要 IP 地理服务获取
        try
        {
            using var response = await HttpClient.GetAsync("http://ip-api.com/json/?fields=status,countryCode,country", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
                {
                    string code = root.GetProperty("countryCode").GetString()?.ToUpperInvariant() ?? "CN";
                    string name = root.GetProperty("country").GetString() ?? code;
                    string lang = IsChineseCountry(code) ? "zh-CN" : "en-US";

                    var result = new GeoLocationResult(code, name, lang, "IpApi");
                    SaveCache(result);
                    return result;
                }
            }
        }
        catch
        {
            // 主接口失败，尝试备用接口
        }

        // 3. 备用接口: https://api.country.is/
        try
        {
            using var response = await HttpClient.GetAsync("https://api.country.is/", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("country", out var countryProp))
                {
                    string code = countryProp.GetString()?.ToUpperInvariant() ?? "CN";
                    string lang = IsChineseCountry(code) ? "zh-CN" : "en-US";

                    var result = new GeoLocationResult(code, code, lang, "CountryIs");
                    SaveCache(result);
                    return result;
                }
            }
        }
        catch
        {
            // 离线或两路接口均不可达，优雅回退到系统本地
        }

        return DetectFromSystemRegion();
    }

    private static bool IsChineseCountry(string code) =>
        code is "CN" or "HK" or "TW" or "MO";

    private static GeoLocationResult? TryReadCache()
    {
        try
        {
            if (!File.Exists(CacheFile)) return null;
            var fileInfo = new FileInfo(CacheFile);
            if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > TimeSpan.FromHours(24))
            {
                return null; // 超过 24 小时过期重探
            }

            string text = File.ReadAllText(CacheFile);
            var doc = JsonDocument.Parse(text).RootElement;
            string code = doc.GetProperty("CountryCode").GetString() ?? "CN";
            string name = doc.GetProperty("CountryName").GetString() ?? code;
            string lang = doc.GetProperty("MatchedLanguage").GetString() ?? "zh-CN";
            return new GeoLocationResult(code, name, lang, "LocalCache");
        }
        catch
        {
            return null;
        }
    }

    private static void SaveCache(GeoLocationResult result)
    {
        try
        {
            string? dir = Path.GetDirectoryName(CacheFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(result);
            File.WriteAllText(CacheFile, json);
        }
        catch { }
    }
}