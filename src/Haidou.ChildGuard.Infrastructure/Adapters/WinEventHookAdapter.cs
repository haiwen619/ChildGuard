using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public class WinEventHookAdapter : IDisposable
{
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int GetWindowTextW(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private readonly WinEventDelegate _delegate;
    private IntPtr _hookHandle = IntPtr.Zero;

    public event Action<uint, string, string>? ForegroundChanged;

    public WinEventHookAdapter()
    {
        _delegate = OnHookCallback;
    }

    public void Start()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            _hookHandle = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _delegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        }
    }

    private void OnHookCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == IntPtr.Zero) return;

        char[] buf = new char[512];
        int len = GetWindowTextW(hwnd, buf, buf.Length);
        string title = len > 0 ? new string(buf, 0, len) : string.Empty;

        GetWindowThreadProcessId(hwnd, out uint pid);
        string processName = "Unknown.exe";

        if (pid > 0)
        {
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                processName = proc.ProcessName + ".exe";
            }
            catch { }
        }

        ForegroundChanged?.Invoke(pid, processName, title);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
