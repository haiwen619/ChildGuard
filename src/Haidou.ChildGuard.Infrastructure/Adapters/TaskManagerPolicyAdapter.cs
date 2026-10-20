using System;
using Microsoft.Win32;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

/// <summary>
/// Windows 任务管理器策略适配器（防止未成年人通过任务管理器强杀守护进程）
/// </summary>
public static class TaskManagerPolicyAdapter
{
    private const string SubKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string ValueName = "DisableTaskMgr";

    public static bool IsTaskManagerDisabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(SubKey, false);
            if (key == null) return false;
            object? val = key.GetValue(ValueName);
            return val != null && Convert.ToInt32(val) == 1;
        }
        catch
        {
            return false;
        }
    }

    public static bool SetTaskManagerDisabled(bool disabled)
    {
        try
        {
            if (disabled)
            {
                using var key = Registry.CurrentUser.CreateSubKey(SubKey, true);
                key?.SetValue(ValueName, 1, RegistryValueKind.DWord);
                return true;
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(SubKey, true);
                if (key != null)
                {
                    key.DeleteValue(ValueName, false);
                }
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}