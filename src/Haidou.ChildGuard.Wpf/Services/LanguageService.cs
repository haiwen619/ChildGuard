using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Haidou.ChildGuard.App.Services;

/// <summary>
/// 全局多语言与本地化管理服务
/// 支持 XAML 动态热切换、IP/地理位置自适应及用户偏好持久化
/// </summary>
public sealed class LanguageService
{
    private static readonly Lazy<LanguageService> _instance = new(() => new LanguageService());
    public static LanguageService Instance => _instance.Value;

    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HaidouChildGuard",
        "language_settings.json");

    public const string AutoMode = "auto";
    public const string Chinese = "zh-CN";
    public const string English = "en-US";

    public string CurrentLanguage { get; private set; } = Chinese;
    public string SelectedPreference { get; private set; } = AutoMode;
    public string DetectedCountryDisplay { get; private set; } = "CN (China)";

    public event EventHandler<string>? LanguageChanged;

    private LanguageService() { }

    /// <summary>
    /// 初始化语言系统（在 App.OnStartup 或 Program.Main 中调用）
    /// 0 毫秒首屏加载 + 异步后台 IP 地理校验
    /// </summary>
    public void Initialize()
    {
        // 1. 读取持久化偏好
        LoadSettings();

        // 2. 如果是手动模式，立即应用
        if (SelectedPreference is Chinese or English)
        {
            ApplyLanguageDictionary(SelectedPreference);
            return;
        }

        // 3. 自动模式：第一阶段 0 毫秒本地系统区域快速判定
        var localResult = GeoLanguageDetector.DetectFromSystemRegion();
        DetectedCountryDisplay = $"{localResult.CountryCode} ({localResult.CountryName})";
        ApplyLanguageDictionary(localResult.MatchedLanguage);

        // 4. 自动模式：第二阶段 后台异步 IP 真实地理位置校验
        _ = Task.Run(async () =>
        {
            try
            {
                var netResult = await GeoLanguageDetector.DetectFromIpAsync();
                DetectedCountryDisplay = $"{netResult.CountryCode} ({netResult.CountryName})";

                if (SelectedPreference == AutoMode && netResult.MatchedLanguage != CurrentLanguage)
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        ApplyLanguageDictionary(netResult.MatchedLanguage);
                    });
                }
            }
            catch { }
        });
    }

    /// <summary>
    /// 动态切换语言偏好（支持热切换，0 重启）
    /// </summary>
    /// <param name="preference">auto, zh-CN 或 en-US</param>
    /// <param name="persist">是否保存偏好到磁盘</param>
    public void SetLanguage(string preference, bool persist = true)
    {
        SelectedPreference = preference;

        string targetLang;
        if (preference == AutoMode)
        {
            var res = GeoLanguageDetector.DetectFromSystemRegion();
            targetLang = res.MatchedLanguage;
        }
        else
        {
            targetLang = preference == English ? English : Chinese;
        }

        ApplyLanguageDictionary(targetLang);

        if (persist)
        {
            SaveSettings();
        }
    }

    /// <summary>
    /// 代码层快捷安全读取当前语言的本地化文本
    /// </summary>
    public static string T(string key, string fallback = "")
    {
        try
        {
            if (System.Windows.Application.Current?.Resources?.Contains(key) == true)
            {
                var val = System.Windows.Application.Current.Resources[key];
                if (val is string str) return str;
            }
        }
        catch { }

        return !string.IsNullOrEmpty(fallback) ? fallback : key;
    }

    private void ApplyLanguageDictionary(string langCode)
    {
        string uriStr = langCode switch
        {
            English => "pack://application:,,,/Resources/Languages/Strings.en-US.xaml",
            _ => "pack://application:,,,/Resources/Languages/Strings.zh-CN.xaml"
        };

        try
        {
            var newDict = new ResourceDictionary { Source = new Uri(uriStr, UriKind.Absolute) };
            var appResources = System.Windows.Application.Current?.Resources;
            if (appResources == null) return;

            // 查找并替换现有的语言字典
            ResourceDictionary? existingLangDict = null;
            foreach (var dict in appResources.MergedDictionaries)
            {
                if (dict.Source != null && dict.Source.OriginalString.Contains("/Resources/Languages/"))
                {
                    existingLangDict = dict;
                    break;
                }
            }

            if (existingLangDict != null)
            {
                int index = appResources.MergedDictionaries.IndexOf(existingLangDict);
                appResources.MergedDictionaries[index] = newDict;
            }
            else
            {
                appResources.MergedDictionaries.Add(newDict);
            }

            CurrentLanguage = langCode;
            LanguageChanged?.Invoke(this, langCode);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LanguageService] Failed to load {uriStr}: {ex.Message}");
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("LanguagePreference", out var prefProp))
                {
                    string? pref = prefProp.GetString();
                    if (!string.IsNullOrEmpty(pref))
                    {
                        SelectedPreference = pref;
                    }
                }
            }
        }
        catch { }
    }

    private void SaveSettings()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("LanguagePreference", SelectedPreference);
                writer.WriteString("LastSavedUtc", DateTime.UtcNow.ToString("o"));
                writer.WriteEndObject();
            }

            File.WriteAllBytes(SettingsFile, ms.ToArray());
        }
        catch { }
    }
}