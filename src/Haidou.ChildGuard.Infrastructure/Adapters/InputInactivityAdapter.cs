using System;
using System.Runtime.InteropServices;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public static class InputInactivityAdapter
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static double GetIdleSeconds()
    {
        var lii = new LASTINPUTINFO();
        lii.cbSize = (uint)Marshal.SizeOf(lii);
        if (!GetLastInputInfo(ref lii)) return 0;

        uint tickCount = (uint)Environment.TickCount;
        return (tickCount - lii.dwTime) / 1000.0;
    }

    public static bool IsAfk(double thresholdSeconds = 180.0)
    {
        return GetIdleSeconds() >= thresholdSeconds;
    }
}
