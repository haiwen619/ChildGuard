using System;
using System.Collections.Generic;
using Haidou.ChildGuard.Domain.Common;

namespace Haidou.ChildGuard.Domain.Activities.ValueObjects;

public class ProcessInfo : ValueObject
{
    public string ProcessName { get; }
    public string Category { get; }

    public ProcessInfo(string processName, string? category = null)
    {
        ProcessName = string.IsNullOrWhiteSpace(processName) ? "Unknown.exe" : processName;
        Category = category ?? Categorize(ProcessName);
    }

    private static string Categorize(string processName)
    {
        string p = processName.ToLowerInvariant();
        if (p.Contains("genshin") || p.Contains("steam") || p.Contains("league") || p.Contains("game") || p.Contains("crossfire"))
            return "游戏娱乐";
        if (p.Contains("code") || p.Contains("devenv") || p.Contains("word") || p.Contains("excel") || p.Contains("wps"))
            return "学习工作";
        if (p.Contains("chrome") || p.Contains("msedge") || p.Contains("firefox"))
            return "网页浏览";
        return "常规应用";
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ProcessName.ToLowerInvariant();
        yield return Category;
    }
}
