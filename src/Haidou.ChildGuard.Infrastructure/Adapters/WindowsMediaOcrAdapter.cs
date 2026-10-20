using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Haidou.ChildGuard.Application.Interfaces;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public sealed class WindowsMediaOcrAdapter : IScreenOcrService
{
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private OcrEngine? _ocrEngine;
    private bool _engineInitialized;

    private OcrEngine? GetEngine()
    {
        if (_engineInitialized) return _ocrEngine;

        try
        {
            var zhLang = new Windows.Globalization.Language("zh-Hans-CN");
            if (OcrEngine.IsLanguageSupported(zhLang))
            {
                _ocrEngine = OcrEngine.TryCreateFromLanguage(zhLang);
            }

            if (_ocrEngine == null)
            {
                _ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            }
        }
        catch
        {
            _ocrEngine = null;
        }

        _engineInitialized = true;
        return _ocrEngine;
    }

    public async Task<string> RecognizeActiveWindowTextAsync()
    {
        var engine = GetEngine();
        if (engine == null) return string.Empty;

        try
        {
            // 1. 抓取前台活动窗口图像
            using var bitmap = CaptureForegroundWindowBitmap();
            if (bitmap == null) return string.Empty;

            // 2. 将 System.Drawing.Bitmap 转为 WinRT SoftwareBitmap（纯内存流转，0 磁盘 I/O）
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Bmp);
            memoryStream.Position = 0;

            using var randomAccessStream = memoryStream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

            // 3. 执行纯本地离线 OCR
            var ocrResult = await engine.RecognizeAsync(softwareBitmap);
            return ocrResult?.Text ?? string.Empty;
        }
        catch
        {
            // 离线捕获安全容错，绝不引发宿主崩溃
            return string.Empty;
        }
    }

    private Bitmap? CaptureForegroundWindowBitmap()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            int x = 0, y = 0, w = 1280, h = 720;

            if (hwnd != IntPtr.Zero && !IsIconic(hwnd) && GetWindowRect(hwnd, out var rect))
            {
                w = rect.Right - rect.Left;
                h = rect.Bottom - rect.Top;
                x = rect.Left;
                y = rect.Top;
            }

            // 异常窗口或过小尺寸保护：如果尺寸不合理，抓取主屏工作区
            if (w < 100 || h < 100 || w > 7680 || h > 4320)
            {
                x = 0;
                y = 0;
                w = GetSystemMetrics(SM_CXSCREEN);
                h = GetSystemMetrics(SM_CYSCREEN);
                if (w <= 0) w = 1920;
                if (h <= 0) h = 1080;
            }

            // 对超大分辨率进行轻量下采样，确保 OCR 毫秒级极速响应且降低 CPU 开销
            int targetW = w;
            int targetH = h;
            if (targetW > 1920)
            {
                double ratio = 1920.0 / targetW;
                targetW = 1920;
                targetH = (int)(targetH * ratio);
            }

            var bmp = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            g.CopyFromScreen(x, y, 0, 0, new Size(targetW, targetH), CopyPixelOperation.SourceCopy);

            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
