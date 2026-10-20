using System;
using System.IO;
using System.Windows;

namespace Haidou.ChildGuard.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 关键：强制设置当前工作目录为 exe 所在目录，杜绝因外部调用目录不同导致资源与看门狗路径找不到
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        base.OnStartup(e);
    }
}
