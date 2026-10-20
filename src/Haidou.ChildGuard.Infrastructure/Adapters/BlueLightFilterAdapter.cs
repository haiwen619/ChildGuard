using System;
using System.Runtime.InteropServices;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public sealed class BlueLightFilterAdapter : IDisposable
{
    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

    [DllImport("gdi32.dll")]
    private static extern bool GetDeviceGammaRamp(IntPtr hDC, ref RAMP lpRamp);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct RAMP
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    private readonly object _syncLock = new();
    private RAMP? _originalRamp;
    private bool _isFilterActive;

    public bool IsFilterActive => _isFilterActive;

    public BlueLightFilterAdapter()
    {
        SaveOriginalRamp();
    }

    private void SaveOriginalRamp()
    {
        try
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                var ramp = new RAMP
                {
                    Red = new ushort[256],
                    Green = new ushort[256],
                    Blue = new ushort[256]
                };

                if (GetDeviceGammaRamp(hdc, ref ramp))
                {
                    _originalRamp = ramp;
                }
                ReleaseDC(IntPtr.Zero, hdc);
            }
        }
        catch {}
    }

    /// <summary>
    /// 开启滤蓝光暖色温护眼模式 (strength: 1 ~ 100, 推荐 70)
    /// </summary>
    public bool ApplyWarmFilter(int strengthPercent = 70)
    {
        lock (_syncLock)
        {
            try
            {
                IntPtr hdc = GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero) return false;

                if (_originalRamp == null)
                {
                    SaveOriginalRamp();
                }

                // 强度映射：0 (无过滤) ~ 100 (强过滤深度暖光)
                // strength 70: Red 保持 1.0, Green 约 0.93, Blue 约 0.72 (过滤 28% 高能短波蓝光)
                double factor = Math.Clamp(strengthPercent, 10, 100) / 100.0;
                double blueScale = 1.0 - (0.42 * factor);  // 最低降至 0.58
                double greenScale = 1.0 - (0.10 * factor); // 最低降至 0.90
                double redScale = 1.0;

                var newRamp = new RAMP
                {
                    Red = new ushort[256],
                    Green = new ushort[256],
                    Blue = new ushort[256]
                };

                for (int i = 0; i < 256; i++)
                {
                    int baseVal = i * 256;
                    if (baseVal > 65535) baseVal = 65535;

                    newRamp.Red[i] = (ushort)Math.Clamp((int)(baseVal * redScale), 0, 65535);
                    newRamp.Green[i] = (ushort)Math.Clamp((int)(baseVal * greenScale), 0, 65535);
                    newRamp.Blue[i] = (ushort)Math.Clamp((int)(baseVal * blueScale), 0, 65535);
                }

                bool result = SetDeviceGammaRamp(hdc, ref newRamp);
                ReleaseDC(IntPtr.Zero, hdc);

                if (result)
                {
                    _isFilterActive = true;
                }
                return result;
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
    public bool RestoreDefault()
    {
        lock (_syncLock)
        {
            try
            {
                IntPtr hdc = GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero) return false;

                bool result = false;
                if (_originalRamp.HasValue)
                {
                    var ramp = _originalRamp.Value;
                    result = SetDeviceGammaRamp(hdc, ref ramp);
                }
                else
                {
                    // 构造标准线性色彩 Ramp
                    var linearRamp = new RAMP
                    {
                        Red = new ushort[256],
                        Green = new ushort[256],
                        Blue = new ushort[256]
                    };
                    for (int i = 0; i < 256; i++)
                    {
                        ushort val = (ushort)Math.Min(65535, i * 256);
                        linearRamp.Red[i] = val;
                        linearRamp.Green[i] = val;
                        linearRamp.Blue[i] = val;
                    }
                    result = SetDeviceGammaRamp(hdc, ref linearRamp);
                }

                ReleaseDC(IntPtr.Zero, hdc);
                _isFilterActive = false;
                return result;
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
