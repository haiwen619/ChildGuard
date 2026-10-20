using System;
using System.Runtime.InteropServices;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public sealed class BlueLightFilterAdapter : IDisposable
{
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern unsafe bool SetDeviceGammaRamp(IntPtr hDC, void* lpRamp);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern unsafe bool GetDeviceGammaRamp(IntPtr hDC, void* lpRamp);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    // 严苛保证纯连续 1536 字节平坦结构，零托管堆引用，杜绝 CLR Marshaller 堆破坏 (0x80131506)
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RAMP
    {
        public fixed ushort Red[256];
        public fixed ushort Green[256];
        public fixed ushort Blue[256];
    }

    private readonly object _syncLock = new();
    private RAMP _originalRamp;
    private bool _hasOriginalRamp = false;
    private bool _isSupported = true;
    private bool _isFilterActive = false;

    public bool IsFilterActive => _isFilterActive;
    public bool IsSupported => _isSupported;

    public BlueLightFilterAdapter()
    {
        // 构造时不强行调用非托管硬件接口，避免在虚拟机或无显卡驱动环境下启动即崩死
    }

    private unsafe bool EnsureOriginalRamp()
    {
        if (_hasOriginalRamp) return true;
        if (!_isSupported) return false;

        try
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero)
            {
                _isSupported = false;
                return false;
            }

            try
            {
                fixed (RAMP* pRamp = &_originalRamp)
                {
                    bool ok = GetDeviceGammaRamp(hdc, pRamp);
                    if (ok)
                    {
                        _hasOriginalRamp = true;
                        return true;
                    }
                    else
                    {
                        // 显卡驱动或当前显示器不支持硬件 Gamma 调节，标记不支持并优雅退出
                        _isSupported = false;
                        return false;
                    }
                }
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }
        catch
        {
            _isSupported = false;
            return false;
        }
    }

    /// <summary>
    /// 开启滤蓝光暖色温护眼模式 (strength: 10 ~ 100, 推荐 70)
    /// </summary>
    public unsafe bool ApplyWarmFilter(int strengthPercent = 70)
    {
        lock (_syncLock)
        {
            if (!EnsureOriginalRamp()) return false;

            try
            {
                IntPtr hdc = GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero) return false;

                try
                {
                    double factor = Math.Clamp(strengthPercent, 10, 100) / 100.0;
                    double blueScale = 1.0 - (0.42 * factor);
                    double greenScale = 1.0 - (0.10 * factor);
                    double redScale = 1.0;

                    RAMP newRamp;
                    for (int i = 0; i < 256; i++)
                    {
                        int baseVal = i * 256;
                        if (baseVal > 65535) baseVal = 65535;

                        newRamp.Red[i] = (ushort)Math.Clamp((int)(baseVal * redScale), 0, 65535);
                        newRamp.Green[i] = (ushort)Math.Clamp((int)(baseVal * greenScale), 0, 65535);
                        newRamp.Blue[i] = (ushort)Math.Clamp((int)(baseVal * blueScale), 0, 65535);
                    }

                    bool result = SetDeviceGammaRamp(hdc, &newRamp);
                    if (result)
                    {
                        _isFilterActive = true;
                    }
                    return result;
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, hdc);
                }
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 恢复系统默认原生色温
    /// </summary>
    public unsafe bool RestoreDefault()
    {
        lock (_syncLock)
        {
            if (!_isFilterActive && !_hasOriginalRamp) return true;

            try
            {
                IntPtr hdc = GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero) return false;

                try
                {
                    bool result;
                    if (_hasOriginalRamp)
                    {
                        fixed (RAMP* pRamp = &_originalRamp)
                        {
                            result = SetDeviceGammaRamp(hdc, pRamp);
                        }
                    }
                    else
                    {
                        RAMP linearRamp;
                        for (int i = 0; i < 256; i++)
                        {
                            ushort val = (ushort)Math.Min(65535, i * 256);
                            linearRamp.Red[i] = val;
                            linearRamp.Green[i] = val;
                            linearRamp.Blue[i] = val;
                        }
                        result = SetDeviceGammaRamp(hdc, &linearRamp);
                    }

                    _isFilterActive = false;
                    return result;
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, hdc);
                }
            }
            catch
            {
                return false;
            }
        }
    }

    public void Dispose()
    {
        RestoreDefault();
    }
}
