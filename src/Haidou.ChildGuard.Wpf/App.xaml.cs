using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Haidou.ChildGuard.App;

public partial class App : System.Windows.Application
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HaidouChildGuard",
        "logs");

    protected override void OnStartup(StartupEventArgs e)
    {
        // 关键：强制设置当前工作目录为 exe 所在目录，杜绝因外部调用目录不同导致资源与看门狗路径找不到
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        // 注册全方位未处理异常捕获，杜绝静默闪退，并记录到本地日志
        SetupCrashReporting();

        base.OnStartup(e);
    }

    private void SetupCrashReporting()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            RecordCrash("AppDomain.UnhandledException", args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            RecordCrash("DispatcherUnhandledException", args.Exception);
            // 尝试阻止程序硬性崩溃退出
            args.Handled = true;
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            RecordCrash("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };
    }

    private static void RecordCrash(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            string logFile = Path.Combine(LogDir, $"crash_{DateTime.Now:yyyyMMdd}.log");
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}]");
            if (ex != null)
            {
                sb.AppendLine($"Type: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    sb.AppendLine($"Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                    sb.AppendLine($"InnerStackTrace: {ex.InnerException.StackTrace}");
                }
            }
            sb.AppendLine(new string('-', 60));
            File.AppendAllText(logFile, sb.ToString(), Encoding.UTF8);
        }
        catch { }
    }
}
