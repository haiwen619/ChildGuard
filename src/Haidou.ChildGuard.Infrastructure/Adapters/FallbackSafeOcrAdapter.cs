using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Haidou.ChildGuard.Application.Interfaces;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

/// <summary>
/// 纯托管安全降级文本审计适配器（用于非 MSIX 打包的纯普通 exe 桌面环境，100% 杜绝 WinRT 0xc0000602 FailFast 闪退）
/// </summary>
public sealed class FallbackSafeOcrAdapter : IScreenOcrService
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
