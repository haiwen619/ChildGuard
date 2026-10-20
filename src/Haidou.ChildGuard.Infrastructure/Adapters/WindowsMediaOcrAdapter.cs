using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Haidou.ChildGuard.Application.Interfaces;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

/// <summary>
/// 纯托管活动窗口与屏幕健康审计适配器（完全去除 WinRT 强绑定，100% 杜绝 Windows 10 0xc0000602 FailFast 闪退）
/// </summary>
public sealed class WindowsMediaOcrAdapter : IScreenOcrService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int GetWindowTextW(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    public Task<string> RecognizeActiveWindowTextAsync()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return Task.FromResult(string.Empty);

            char[] buf = new char[512];
            int len = GetWindowTextW(hwnd, buf, buf.Length);
            string title = len > 0 ? new string(buf, 0, len) : string.Empty;

            return Task.FromResult(title);
        }
        catch
        {
            return Task.FromResult(string.Empty);
        }
    }
}
