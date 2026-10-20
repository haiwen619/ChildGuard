using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Haidou.Watchdog;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    private static void Main(string[] args)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string logFile = Path.Combine(baseDir, "watchdog.log");

        void Log(string msg)
        {
            try { File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\r\n"); } catch {}
        }

        Log("Watchdog starting (Independent Shell Engine)...");

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Log($"FATAL UnhandledException: {e.ExceptionObject}");
        };

        try
        {
            _mutex = new Mutex(true, @"Local\HaidouWatchdogSingleInstance", out bool isNew);
            if (!isNew)
            {
                Log("Another watchdog instance detected. Exiting gracefully.");
                return;
            }
        }
        catch (Exception ex)
        {
            Log($"Mutex warning: {ex.Message}");
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string permitPath = Path.Combine(appData, "HaidouChildGuard", "guardian_exit.token");
        string mainAppExe = Path.Combine(baseDir, "Haidou.ChildGuard.Wpf.exe");

        Log($"Watching for Haidou.ChildGuard.Wpf. Target path: {mainAppExe}");

        bool CheckExitPermit()
        {
            try
            {
                if (!File.Exists(permitPath)) return false;
                string token = File.ReadAllText(permitPath);
                var parts = token.Split(':');
                if (parts.Length == 3 && parts[0] == "PERMIT")
                {
                    if (long.TryParse(parts[1], out long expireEpoch))
                    {
                        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        return now <= expireEpoch;
                    }
                }
            }
            catch {}
            return false;
        }

        while (true)
        {
            try
            {
                Thread.Sleep(200);

                // 1. 检查是否存在未过期的家长合法退出许可
                if (CheckExitPermit())
                {
                    Log("Parent exit permit is VALID. Terminating watchdog gracefully.");
                    try { File.Delete(permitPath); } catch {}
                    break;
                }

                // 2. 检测主应用进程存活状态
                var procs = Process.GetProcessesByName("Haidou.ChildGuard.Wpf");
                if (procs.Length == 0)
                {
                    Log("ALERT: Haidou.ChildGuard.Wpf is NOT running! Initiating immediate revival...");
                    if (File.Exists(mainAppExe))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = mainAppExe,
                            Arguments = "--recovered",
                            UseShellExecute = true,
                            WorkingDirectory = baseDir
                        };
                        var p = Process.Start(psi);
                        Log($"SUCCESS: Main app revived! New PID: {p?.Id}");
                    }
                    else
                    {
                        Log($"ERROR: Main app executable not found at: {mainAppExe}");
                    }

                    Thread.Sleep(2000);
                }
            }
            catch (Exception ex)
            {
                Log($"Exception in watchdog loop: {ex}");
                Thread.Sleep(1000);
            }
        }

        Log("Watchdog terminated.");
    }
}