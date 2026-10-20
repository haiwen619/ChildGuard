using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Haidou.ChildGuard.App;

internal static class Program
{
    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    [STAThread]
    public static void Main(string[] args)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        Directory.SetCurrentDirectory(baseDir);

        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string crashLog = Path.Combine(desktopPath, "ChildGuard_Crash.txt");

        void RecordError(string title, Exception? ex)
        {
            string detail = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}\n" +
                            $"Exception: {ex?.GetType().FullName}\n" +
                            $"Message: {ex?.Message}\n" +
                            $"StackTrace:\n{ex?.StackTrace}\n";
            if (ex?.InnerException != null)
            {
                detail += $"\nInner Exception: {ex.InnerException.GetType().FullName}\n" +
                          $"Inner Message: {ex.InnerException.Message}\n" +
                          $"Inner StackTrace:\n{ex.InnerException.StackTrace}\n";
            }

            try { File.WriteAllText(crashLog, detail, Encoding.UTF8); } catch { }
            try { MessageBoxW(IntPtr.Zero, detail, "童伴守护 (ChildGuard) 启动异常拦截", 0x00000010 /* MB_ICONERROR */); } catch { }
        }

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            RecordError("AppDomain UnhandledException", e.ExceptionObject as Exception);
        };

        try
        {
            var app = new App();
            app.InitializeComponent();

            app.DispatcherUnhandledException += (s, e) =>
            {
                RecordError("DispatcherUnhandledException", e.Exception);
                e.Handled = true;
            };

            // 手动创建并展示 MainWindow，彻底杜绝 StartupUri 黑盒反射静默 FailFast (0xc0000602)
            var mainWindow = new MainWindow();
            app.Run(mainWindow);
        }
        catch (Exception ex)
        {
            RecordError("Main 入口直接捕获异常", ex);
        }
    }
}
