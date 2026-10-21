using Haidou.ChildGuard.App.Services;
using Haidou.ChildGuard.Application.UseCases.Auditing;
using Haidou.ChildGuard.Application.Interfaces;
using System.Runtime.InteropServices;
﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Haidou.ChildGuard.Application.DTOs;
using Haidou.ChildGuard.Application.UseCases.Activities;
using Haidou.ChildGuard.Application.UseCases.Policies;
using Haidou.ChildGuard.Application.UseCases.Security;
using Haidou.ChildGuard.Domain.Policies.Services;
using Haidou.ChildGuard.Infrastructure.Adapters;
using Haidou.ChildGuard.Infrastructure.Persistence;
using Haidou.ChildGuard.Infrastructure.Services;
using Wpf.Ui.Controls;

using Forms = System.Windows.Forms;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Haidou.ChildGuard.App;

public partial class MainWindow : FluentWindow
{



    private readonly RecordActivityHeartbeatUseCase _recordHeartbeatUseCase;
    private readonly GetDailyReportUseCase _getDailyReportUseCase;
    private readonly ManagePolicyUseCase _managePolicyUseCase;
    private readonly AuthenticateParentUseCase _authUseCase;

    private readonly WinEventHookAdapter _hookAdapter;
    private readonly ActivityHeartbeatEngine _heartbeatEngine;
    private readonly DatabaseContext _dbContext;
    private readonly HostsWebFilterAdapter _webFilterAdapter;
    private readonly LocalCredentialStore _credStore;
        private readonly TotpService _totpService;

    private readonly SqliteScreenAuditRepository _auditRepo;
    private readonly IScreenOcrService _ocrService;
    private readonly AhoCorasickMatcher _keywordMatcher;
    private readonly PerformScreenAuditUseCase _performAuditUseCase;
    private readonly GetScreenAuditsUseCase _getAuditsUseCase;
    private readonly AcknowledgeScreenAuditUseCase _ackAuditUseCase;
    private readonly ScreenAuditEngine _auditEngine;

    private bool _isScreenAuditUnlocked = false;
    public ObservableCollection<ScreenAuditItemViewModel> ScreenAuditList { get; } = new();

    private readonly BlueLightFilterAdapter _blueLightFilter;
    private readonly EyeCareTimerEngine _eyeCareTimer;
    private EyeCareBreakWindow? _activeBreakWindow;
    private CurfewLockWindow? _activeCurfewWindow;
    private int _filterWarmthPercent = 70;

    private readonly DispatcherTimer _watchdogGuardTimer;
    private Forms.NotifyIcon? _notifyIcon;

    private string _currentTotpSecret = string.Empty;
    private bool _isExplicitExit = false;
    private int _dailyLimitMinutes = 120;

    public ObservableCollection<UsageItemViewModel> TodayUsageList { get; } = new();
    public ObservableCollection<ActivitySessionDto> ActivityList { get; } = new();
    public ObservableCollection<BlockedRuleItemViewModel> BlockedRuleList { get; } = new();
    private List<BlockedRuleItemViewModel> _allCachedRules = new();

    private Func<System.Threading.Tasks.Task>? _pendingParentAuthorizedAction;

    public MainWindow()
    {

        InitializeComponent();

        _dbContext = new DatabaseContext();
        var activityRepo = new SqliteActivityRepository(_dbContext);
        var policyRepo = new SqlitePolicyRepository(_dbContext);

        _credStore = new LocalCredentialStore();
        _credStore.RevokeExitPermit();
        _totpService = new TotpService(_credStore);

        _recordHeartbeatUseCase = new RecordActivityHeartbeatUseCase(activityRepo);
        _getDailyReportUseCase = new GetDailyReportUseCase(activityRepo);
        _managePolicyUseCase = new ManagePolicyUseCase(policyRepo);
        _authUseCase = new AuthenticateParentUseCase(_totpService, _credStore);

        _hookAdapter = new WinEventHookAdapter();
        _heartbeatEngine = new ActivityHeartbeatEngine();
        _webFilterAdapter = new HostsWebFilterAdapter();

        ListTodayUsage.ItemsSource = TodayUsageList;
        ListAllActivities.ItemsSource = ActivityList;

        _hookAdapter.ForegroundChanged += OnForegroundChanged;
        _heartbeatEngine.SessionFlushed += OnSessionFlushed;

        InitTotp();
        ListBlockedRules.ItemsSource = BlockedRuleList;
        RefreshWebFilterRulesDisplay();

        _hookAdapter.Start();
        _ = LoadDailyReportAsync();
        _auditRepo = new SqliteScreenAuditRepository(_dbContext);
                // 关键：非 MSIX 打包环境（普通桌面 exe）绝对不加载 Windows.Media.Ocr，防止 Windows 10 触发 0xc0000602 FailFast 猝死
        if (PackageIdentityHelper.HasPackageIdentity())
        {
            _ocrService = new WindowsMediaOcrAdapter();
        }
        else
        {
            _ocrService = new FallbackSafeOcrAdapter();
        }
        _keywordMatcher = new AhoCorasickMatcher();

        var notifService = new WpfSystemNotificationService(() => _notifyIcon);
        _performAuditUseCase = new PerformScreenAuditUseCase(_ocrService, _keywordMatcher, _auditRepo, notifService);
        _getAuditsUseCase = new GetScreenAuditsUseCase(_auditRepo);
        _ackAuditUseCase = new AcknowledgeScreenAuditUseCase(_auditRepo);

        _auditEngine = new ScreenAuditEngine(_performAuditUseCase);
        _auditEngine.AuditTriggered += OnScreenAuditTriggered;
        _auditEngine.Start();

        ListScreenAudits.ItemsSource = ScreenAuditList;
        _ = RefreshAuditBadgeAsync();

        _blueLightFilter = new BlueLightFilterAdapter();
        _eyeCareTimer = new EyeCareTimerEngine();
        _eyeCareTimer.RestBreakRequired += OnRestBreakRequired;
        _eyeCareTimer.CurfewLockRequired += OnCurfewLockRequired;
        _eyeCareTimer.StatusUpdated += OnEyeCareStatusUpdated;

        // 1. 初始化 Windows 系统托盘常驻图标 (NotifyIcon)
        InitSystemTray();

        // 2. 启动独立伴生看门狗进程 (Haidou.Watchdog.exe)
        EnsureWatchdogRunning();

        // 3. 启动双向互保定时器 (每 1.5 秒检查一次看门狗是否存活)
        _watchdogGuardTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };
        _watchdogGuardTimer.Tick += (s, e) => EnsureWatchdogRunning();
        _watchdogGuardTimer.Start();

        // 4. 检查是否是由看门狗自愈重启拉起
        if (Environment.GetCommandLineArgs().Contains("--recovered"))
        {
            BannerRecovered.Visibility = Visibility.Visible;
            RestoreAndBringToFront();
            _notifyIcon?.ShowBalloonTip(2000, "童伴守护已自愈复苏", "检测到异常终止，系统已自动重新恢复健康护航。", Forms.ToolTipIcon.Info);
        }
    }

    private void FluentWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000)
            {
                WindowBackdropType = WindowBackdropType.Mica;
            }
        }
        catch {}

        RestoreAndBringToFront();

        // 首次使用检测：如果家长尚未设置主密码，强制弹出首次设置密码模态浮层
        if (!_authUseCase.IsPasswordConfigured())
        {
            ModalHost.Visibility = Visibility.Visible;
            ModalSetupPassword.Visibility = Visibility.Visible;
            ModalExitAuth.Visibility = Visibility.Collapsed;
            InputNewPassword.Focus();
        }
    }

    #region 任务栏与系统托盘管理

    private void InitSystemTray()
    {
        try
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Text = "海豆·童伴守护 - 纯本地健康护航中",
                Visible = true
            };

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.ico");
            if (File.Exists(iconPath))
            {
                _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
            }
            else
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Shield;
            }

            // 双击托盘图标：瞬间激活并前置展示主窗口
            _notifyIcon.DoubleClick += (s, e) =>
            {
                Dispatcher.Invoke(RestoreAndBringToFront);
            };

            // 点击气泡通知：瞬间激活并前置展示主窗口
            _notifyIcon.BalloonTipClicked += (s, e) =>
            {
                Dispatcher.Invoke(RestoreAndBringToFront);
            };

            // 右键托盘上下文菜单
            var menu = new Forms.ContextMenuStrip();

            var itemOpen = new Forms.ToolStripMenuItem("📊 打开守护控制中心");
            itemOpen.Click += (s, e) => Dispatcher.Invoke(RestoreAndBringToFront);

            var itemStatus = new Forms.ToolStripMenuItem("🛡️ 状态: 纯本地离线保护中");
            itemStatus.Enabled = false;

            var itemSep = new Forms.ToolStripSeparator();

            var itemExit = new Forms.ToolStripMenuItem("🚪 退出童伴守护 (需家长密码)");
            itemExit.Click += (s, e) => Dispatcher.Invoke(() =>
            {
                RestoreAndBringToFront();
                BtnRequestExit_Click(this, new RoutedEventArgs());
            });

            menu.Items.Add(itemOpen);
            menu.Items.Add(itemStatus);
            menu.Items.Add(itemSep);
            menu.Items.Add(itemExit);

            _notifyIcon.ContextMenuStrip = menu;
        }
        catch {}
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

    public void MinimizeToTray(bool showBalloon = true)
    {
        try
        {
            this.WindowState = WindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();


            if (showBalloon)
            {
                _notifyIcon?.ShowBalloonTip(
                    1500,
                    "海豆·童伴守护",
                    "系统已转入后台持续护航，双击右下角系统托盘图标可随时呼出控制面板。",
                    Forms.ToolTipIcon.None
                );
            }
        }
        catch {}
    }

    public void RestoreAndBringToFront()
    {
        try
        {
            this.ShowInTaskbar = true;
            if (this.Visibility != Visibility.Visible)
            {
                this.Show();
            }

            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }

            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            IntPtr handle = helper.EnsureHandle();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, 9); // SW_RESTORE
                SetForegroundWindow(handle);
                SwitchToThisWindow(handle, true);
            }

            this.Activate();
            this.Topmost = true;
            this.Topmost = false;
            this.Focus();
        }
        catch {}
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (this.WindowState == WindowState.Minimized && !_isExplicitExit)
        {
            // 用户点击窗口右上角最小化按钮，也隐藏任务栏图标，只保留右下角系统托盘
            MinimizeToTray(showBalloon: false);
        }
    }

    #endregion

    private void EnsureWatchdogRunning()
    {
        if (_isExplicitExit) return;

        try
        {
            var pList = Process.GetProcessesByName("Haidou.Watchdog");
            if (pList.Length == 0)
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string watchdogExe = Path.Combine(baseDir, "Haidou.Watchdog.exe");
                if (File.Exists(watchdogExe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = watchdogExe,
                        UseShellExecute = false,
                        WorkingDirectory = baseDir,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    });
                }
            }
        }
        catch {}
    }

    #region 首次密码设置与修改

    private void BtnConfirmSetupPassword_Click(object sender, RoutedEventArgs e)
    {
        string p1 = InputNewPassword.Password;
        string p2 = InputConfirmPassword.Password;

        if (string.IsNullOrWhiteSpace(p1) || p1.Length < 4)
        {
            TxtSetupError.Text = "❌ 密码长度至少需要 4 位字符。";
            return;
        }

        if (p1 != p2)
        {
            TxtSetupError.Text = "❌ 两次输入的密码不一致，请重新输入。";
            return;
        }

        bool ok = _authUseCase.SetInitialPassword(p1, out string errMsg);
        if (ok)
        {
            ModalHost.Visibility = Visibility.Collapsed;
            ModalSetupPassword.Visibility = Visibility.Collapsed;
            System.Windows.MessageBox.Show("✅ 家长主密码设置成功！已通过 Windows DPAPI 本地硬件固化保护。退出系统或修改策略将受此密码保护。", "海豆·童伴守护", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            TxtSetupError.Text = "❌ " + errMsg;
        }
    }

    private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
    {
        string oldPwd = InputOldPwd.Password;
        string newPwd = InputNewPwdChange.Password;

        if (!_authUseCase.VerifyPassword(oldPwd))
        {
            TxtChangePwdResult.Text = "❌ 原密码验证失败，无法修改！";
            TxtChangePwdResult.Foreground = System.Windows.Media.Brushes.OrangeRed;
            return;
        }

        if (string.IsNullOrWhiteSpace(newPwd) || newPwd.Length < 4)
        {
            TxtChangePwdResult.Text = "❌ 新密码长度至少需要 4 位字符。";
            TxtChangePwdResult.Foreground = System.Windows.Media.Brushes.OrangeRed;
            return;
        }

        bool ok = _authUseCase.SetInitialPassword(newPwd, out string err);
        if (ok)
        {
            TxtChangePwdResult.Text = "✅ 主密码修改成功！已更新本地固化存储。";
            TxtChangePwdResult.Foreground = System.Windows.Media.Brushes.LightGreen;
            InputOldPwd.Password = "";
            InputNewPwdChange.Password = "";
        }
        else
        {
            TxtChangePwdResult.Text = "❌ 修改失败: " + err;
            TxtChangePwdResult.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }
    }

    #endregion

    #region 退出守护核销机制

    private void BtnRequestExit_Click(object sender, RoutedEventArgs e)
    {
        InputExitPassword.Password = "";
        TxtExitError.Text = "";
        ModalHost.Visibility = Visibility.Visible;
        ModalExitAuth.Visibility = Visibility.Visible;
        ModalSetupPassword.Visibility = Visibility.Collapsed;
        InputExitPassword.Focus();
    }

    private void BtnCancelExit_Click(object sender, RoutedEventArgs e)
    {
        ModalHost.Visibility = Visibility.Collapsed;
        ModalExitAuth.Visibility = Visibility.Collapsed;
    }

    private void BtnConfirmExit_Click(object sender, RoutedEventArgs e)
    {
        string input = InputExitPassword.Password.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            TxtExitError.Text = "❌ 请输入家长主密码或 6 位离线验证码。";
            return;
        }

        if (_authUseCase.AuthorizeExit(input))
        {
            // 1. 停止互保定时器
            _watchdogGuardTimer.Stop();

            // 2. 签发合法退出许可 (看门狗捕获后放行，不自动复活)
            _credStore.IssueExitPermit(validSeconds: 15);

            // 3. 销毁系统托盘
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            // 4. 标记显式安全退出
            _isExplicitExit = true;
            System.Windows.Application.Current.Shutdown();
        }
        else
        {
            TxtExitError.Text = "❌ 密码错误或动态验证码无效，拒绝退出守护！";
        }
    }

    #endregion

    private void OnForegroundChanged(uint pid, string processName, string title)
    {
        Dispatcher.InvokeAsync(() =>
        {
            TxtCurrentApp.Text = processName;
            TxtCurrentTitle.Text = string.IsNullOrWhiteSpace(title) ? "(无标题窗口)" : title;
        });

        _heartbeatEngine.Pulse(pid, processName, title);
    }

    private async void OnSessionFlushed(ActivitySessionDto session)
    {
        string category = ResolveCategory(session.ProcessName);
        var sessionWithCat = session with { Category = category };

        await _recordHeartbeatUseCase.ExecuteAsync(sessionWithCat);

        await Dispatcher.InvokeAsync(async () =>
        {
            ActivityList.Insert(0, sessionWithCat);
            if (ActivityList.Count > 100) ActivityList.RemoveAt(ActivityList.Count - 1);
            await LoadDailyReportAsync();
        });
    }

    private static string ResolveCategory(string processName)
    {
        string p = processName.ToLowerInvariant();
        if (p.Contains("genshin") || p.Contains("steam") || p.Contains("league") || p.Contains("game") ||
            p.Contains("crossfire") || p.Contains("valorant") || p.Contains("starrail") || p.Contains("bilibili"))
            return "游戏娱乐";

        if (p.Contains("code") || p.Contains("devenv") || p.Contains("word") || p.Contains("excel") ||
            p.Contains("wps") || p.Contains("notepad") || p.Contains("terminal") || p.Contains("powershell"))
            return "学习工作";

        if (p.Contains("chrome") || p.Contains("msedge") || p.Contains("firefox") || p.Contains("opera") || p.Contains("browser"))
            return "网页浏览";

        if (p.Contains("childguard") || p.Contains("explorer") || p.Contains("taskmgr"))
            return "系统守护";

        return "常规工具";
    }

    private async System.Threading.Tasks.Task LoadDailyReportAsync()
    {
        var report = await _getDailyReportUseCase.ExecuteAsync(DateTime.UtcNow);
        TodayUsageList.Clear();

        foreach (var app in report.TopApps)
        {
            string cat = app.Category;
            if (string.IsNullOrEmpty(cat) || cat == "Uncategorized" || cat == "Other")
            {
                cat = ResolveCategory(app.ProcessName);
            }

            var vm = new UsageItemViewModel
            {
                ProcessName = app.ProcessName,
                Category = cat,
                ActiveTimeDisplay = FormatSeconds(app.TotalActiveSeconds),
                AfkTimeDisplay = FormatSeconds(app.TotalAfkSeconds),
                LaunchCount = app.LaunchCount,
                ActiveSeconds = app.TotalActiveSeconds
            };

            AssignCategoryStyling(vm);
            TodayUsageList.Add(vm);
        }

        int totalMins = (int)(report.TotalActiveSeconds / 60);
        TxtTotalScreenTime.Text = $"{totalMins / 60} 小时 {totalMins % 60} 分钟";

        ProgressScreenTime.Maximum = _dailyLimitMinutes;
        ProgressScreenTime.Value = Math.Min(_dailyLimitMinutes, totalMins);

        int percent = (int)((totalMins / (double)_dailyLimitMinutes) * 100);
        TxtLimitRemaining.Text = $"限额 {_dailyLimitMinutes} 分钟 (已消耗 {percent}%)";
    }

    private static void AssignCategoryStyling(UsageItemViewModel vm)
    {
        switch (vm.Category)
        {
            case "游戏娱乐":
                vm.CategoryColor = "#FF7B72";
                vm.CategoryBg = "#2E1A1A";
                vm.IconEmoji = "🎮";
                break;
            case "学习工作":
                vm.CategoryColor = "#3FB950";
                vm.CategoryBg = "#172A1C";
                vm.IconEmoji = "📚";
                break;
            case "网页浏览":
                vm.CategoryColor = "#58A6FF";
                vm.CategoryBg = "#162436";
                vm.IconEmoji = "🌐";
                break;
            case "系统守护":
                vm.CategoryColor = "#D2A8FF";
                vm.CategoryBg = "#241838";
                vm.IconEmoji = "🛡️";
                break;
            default:
                vm.CategoryColor = "#8B949E";
                vm.CategoryBg = "#21262D";
                vm.IconEmoji = "📱";
                break;
        }
    }

    private static string FormatSeconds(double sec)
    {
        int s = (int)sec;
        if (s < 60) return $"{s} 秒";
        int m = s / 60;
        if (m < 60) return $"{m} 分 {s % 60} 秒";
        return $"{m / 60} 小时 {m % 60} 分";
    }

    private void InitTotp()
    {
        var result = _authUseCase.GetOrGenerateRegistrationQr(Environment.MachineName);
        _currentTotpSecret = result.SecretBase32;
        TxtTotpSecret.Text = _currentTotpSecret;
        RenderQrCode(result.QrCodePngBase64);
    }

    private void RenderQrCode(string base64Png)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(base64Png);
            var bitmap = new BitmapImage();
            using var stream = new MemoryStream(bytes);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            ImgTotpQr.Source = bitmap;
        }
        catch (Exception ex)
        {
            TxtTotpResult.Text = "二维码渲染异常: " + ex.Message;
        }
    }

    private void BtnResetTotp_Click(object sender, RoutedEventArgs e)
    {
        var result = _authUseCase.ResetAndGenerateRegistrationQr(Environment.MachineName);
        _currentTotpSecret = result.SecretBase32;
        TxtTotpSecret.Text = _currentTotpSecret;
        RenderQrCode(result.QrCodePngBase64);
        TxtTotpResult.Text = "ℹ️ 已重新生成全新密钥并使用 DPAPI 本地固化更新！";
        TxtTotpResult.Foreground = System.Windows.Media.Brushes.LightSkyBlue;
    }

    private void BtnVerifyTotp_Click(object sender, RoutedEventArgs e)
    {
        string code = InputTotpCode.Text.Trim();
        bool ok = _authUseCase.VerifyParentCode(_currentTotpSecret, code);
        if (ok)
        {
            TxtTotpResult.Text = "✅ 离线验证成功！已通过本地领域安全核销。";
            TxtTotpResult.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        else
        {
            TxtTotpResult.Text = "❌ 验证码错误或已在当前窗口重放，请重试。";
            TxtTotpResult.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }
    }

        #region 家长敏感操作安全核验

    private void RequestParentAuthorization(string prompt, Func<System.Threading.Tasks.Task> authorizedAction)
    {
        _pendingParentAuthorizedAction = authorizedAction;
        TxtParentActionPrompt.Text = "操作目标: " + prompt;
        TxtParentActionError.Text = string.Empty;
        InputParentActionPassword.Password = string.Empty;

        ModalHost.Visibility = Visibility.Visible;
        ModalParentActionAuth.Visibility = Visibility.Visible;
        ModalSetupPassword.Visibility = Visibility.Collapsed;
        ModalExitAuth.Visibility = Visibility.Collapsed;

        InputParentActionPassword.Focus();
    }

    private void BtnCancelParentAction_Click(object sender, RoutedEventArgs e)
    {
        _pendingParentAuthorizedAction = null;
        ModalHost.Visibility = Visibility.Collapsed;
        ModalParentActionAuth.Visibility = Visibility.Collapsed;
    }

    private async void BtnConfirmParentAction_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteParentActionAuthAsync();
    }

    private async void InputParentActionPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            await ExecuteParentActionAuthAsync();
        }
    }

    private async System.Threading.Tasks.Task ExecuteParentActionAuthAsync()
    {
        string input = InputParentActionPassword.Password;
        if (string.IsNullOrWhiteSpace(input))
        {
            TxtParentActionError.Text = "请输入家长管理密码或 6 位动态验证码。";
            return;
        }

        if (_authUseCase.AuthorizeAction(input))
        {
            var action = _pendingParentAuthorizedAction;
            _pendingParentAuthorizedAction = null;
            ModalHost.Visibility = Visibility.Collapsed;
            ModalParentActionAuth.Visibility = Visibility.Collapsed;

            if (action != null)
            {
                await action.Invoke();
            }
        }
        else
        {
            TxtParentActionError.Text = "❌ 家长密码错误或动态验证码无效，操作已被安全拒绝！";
        }
    }

    #endregion

    #region 全系统级网站屏蔽与规则管理

    private void BtnToggleWebFilter_Click(object sender, RoutedEventArgs e)
    {
        bool isActive = _webFilterAdapter.IsFilterActive();
        string prompt = isActive ? "停用全系统不良网站与网址过滤保护" : "激活全系统不良网站与网址过滤保护";

        RequestParentAuthorization(prompt, async () =>
        {
            if (isActive)
            {
                bool removed = await _webFilterAdapter.RemoveFilterAsync();
                if (removed)
                {
                    System.Windows.MessageBox.Show("已停用不良网站过滤规则。", "海豆·童伴守护", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show("停用失败，修改 Hosts 文件需要以管理员权限运行程序。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                bool applied = await _webFilterAdapter.ApplyFilterAsync();
                if (applied)
                {
                    System.Windows.MessageBox.Show("已成功激活全系统不良网站加密屏蔽！底层 Hosts 生效，全浏览器无痕模式无法穿透。", "海豆·童伴守护", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show("激活失败，修改系统 Hosts 文件需要以管理员权限运行程序。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            RefreshWebFilterRulesDisplay();
        });
    }

    private void BtnAddBlockedDomain_Click(object sender, RoutedEventArgs e)
    {
        string raw = InputNewBlockedDomain.Text.Trim();
        string domain = HostsWebFilterAdapter.NormalizeDomain(raw);

        if (string.IsNullOrWhiteSpace(domain) || !domain.Contains('.'))
        {
            System.Windows.MessageBox.Show("请输入合法的网址域名（例如: bilibili.com 或 douyin.com）", "输入格式有误", MessageBoxButton.OK, MessageBoxImage.Warning);
            InputNewBlockedDomain.Focus();
            return;
        }

        RequestParentAuthorization($"添加家长自定义管控网站: {domain}", async () =>
        {
            bool success = await _webFilterAdapter.AddCustomDomainAsync(domain);
            if (success)
            {
                InputNewBlockedDomain.Clear();
                RefreshWebFilterRulesDisplay();
                System.Windows.MessageBox.Show($"已成功将【{domain}】加入全系统底层屏蔽列表！", "海豆·童伴守护", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show($"该域名已在屏蔽列表中，无需重复添加。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });
    }

    private void InputNewBlockedDomain_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            BtnAddBlockedDomain_Click(sender, e);
        }
    }

    private void BtnDeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string domain) return;

        RequestParentAuthorization($"从管控清单中移除: {domain}", async () =>
        {
            bool success = await _webFilterAdapter.RemoveCustomDomainAsync(domain);
            if (success)
            {
                RefreshWebFilterRulesDisplay();
                System.Windows.MessageBox.Show($"已解除对【{domain}】的系统级拦截。", "海豆·童伴守护", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });
    }

    private void RefreshWebFilterRulesDisplay()
    {
        bool isActive = _webFilterAdapter.IsFilterActive();
        var stats = _webFilterAdapter.GetStats();

        // 刷新四大维度统计徽章 (展示规模与状态，完全不暴露敏感网址)
        TxtCountAdult.Text = $"🔞 成人低俗防护: {stats.AdultCount} 核心站 (加密拦截中)";
        TxtCountGambling.Text = $"🎲 涉赌网彩防护: {stats.GamblingCount} 核心站 (加密拦截中)";
        TxtCountGameGray.Text = $"🎮 租号外挂黑产: {stats.GameGrayCount} 核心站 (加密拦截中)";
        TxtCountPhishing.Text = $"🎣 钓鱼欺诈陷阱: {stats.PhishingCount} 核心站 (加密拦截中)";

        TxtRuleCountStats.Text = $"当前自定义管控: {stats.CustomCount} 个网站 · 底层动态加密阻断: {stats.TotalActiveRules - stats.CustomCount} 个高危核心站";

        if (isActive)
        {
            BtnToggleWebFilter.Content = "停用网站屏蔽";
            BtnToggleWebFilter.Appearance = ControlAppearance.Secondary;
            TxtWebFilterStatus.Text = $"状态: 🛡️ 已激活全系统动态加密阻断 (保护中·共 {stats.TotalActiveRules} 项生效)";
            TxtWebFilterStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        else
        {
            BtnToggleWebFilter.Content = "一键激活网站屏蔽";
            BtnToggleWebFilter.Appearance = ControlAppearance.Primary;
            TxtWebFilterStatus.Text = "状态: ⚠️ 未激活 (未屏蔽不良网站)";
            TxtWebFilterStatus.Foreground = System.Windows.Media.Brushes.Orange;
        }

        // 刷新家长自定义管控清单（遵循未成年人防诱导规范，仅展示家长自主添加的域名）
        BlockedRuleList.Clear();
        var customDomains = _webFilterAdapter.GetCustomDomains();

        foreach (var d in customDomains)
        {
            BlockedRuleList.Add(new BlockedRuleItemViewModel { Domain = d });
        }

        CardNoCustomRules.Visibility = customDomains.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion

    private void BtnNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.CommandParameter == null) return;
        int index = Convert.ToInt32(btn.CommandParameter);

        BtnNavOverview.Tag = index == 0 ? "Active" : null;
        BtnNavApps.Tag = index == 1 ? "Active" : null;
        BtnNavPolicy.Tag = index == 2 ? "Active" : null;
        BtnNavTotp.Tag = index == 3 ? "Active" : null;
        BtnNavAbout.Tag = index == 4 ? "Active" : null;

        PanelOverview.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        PanelApps.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        PanelPolicy.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        PanelTotp.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
                PanelAbout.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;

        BtnNavScreenAudit.Tag = index == 5 ? "Active" : null;
        PanelScreenAudit.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
        if (index == 5 && _isScreenAuditUnlocked)
        {
            _ = LoadScreenAuditsAsync();
        }
    }

    private async void BtnSavePolicy_Click(object sender, RoutedEventArgs e)
    {
        int limit = int.TryParse(InputDailyLimit.Text, out int val) ? val : 120;
        _dailyLimitMinutes = limit;
        TimeSpan start = TimeSpan.TryParse(InputCurfewStart.Text, out var s) ? s : new TimeSpan(22, 0, 0);
        TimeSpan end = TimeSpan.TryParse(InputCurfewEnd.Text, out var ed) ? ed : new TimeSpan(7, 0, 0);

        var dto = new PolicyConfigDto(limit, 60, start, end, 45, 5);
        await _managePolicyUseCase.UpdatePolicyAsync(dto);

        ProgressScreenTime.Maximum = limit;
        System.Windows.MessageBox.Show("DDD 领域策略已保存至本地加密仓储！", "海豆·童伴守护", MessageBoxButton.OK, MessageBoxImage.Information);
        _ = LoadDailyReportAsync();
    }

    protected override void OnClosing(CancelEventArgs e)
    {

        if (!_isExplicitExit)
        {
            // 防随手点击右上角叉号退出：拦截并隐藏到任务栏最右边系统托盘（任务栏不显示它）
            e.Cancel = true;
            MinimizeToTray(showBalloon: true);
            return;
        }
        base.OnClosing(e);
    }


    #region 屏幕内容健康审计交互与状态管理

    private void OnScreenAuditTriggered(ScreenAuditRecordDto record)
    {
        Dispatcher.Invoke(async () =>
        {
            await RefreshAuditBadgeAsync();
            if (_isScreenAuditUnlocked)
            {
                await LoadScreenAuditsAsync();
            }
        });
    }

    private async System.Threading.Tasks.Task RefreshAuditBadgeAsync()
    {
        try
        {
            int unackCount = await _getAuditsUseCase.GetUnacknowledgedCountAsync();
            if (unackCount > 0)
            {
                BadgeAuditCount.Visibility = Visibility.Visible;
                TxtAuditBadgeCount.Text = unackCount > 99 ? "99+" : unackCount.ToString();
            }
            else
            {
                BadgeAuditCount.Visibility = Visibility.Collapsed;
            }
        }
        catch {}
    }

    private async System.Threading.Tasks.Task LoadScreenAuditsAsync()
    {
        try
        {
            var audits = await _getAuditsUseCase.GetRecentAuditsAsync(100);
            ScreenAuditList.Clear();
            int unack = 0;

            foreach (var a in audits)
            {
                if (!a.IsAcknowledged) unack++;
                (string bg, string fg) = a.RiskLevelName switch
                {
                    "Critical" => ("#3D1D1D", "#FF7B72"),
                    "High" => ("#38241D", "#F0883E"),
                    "Medium" => ("#38321D", "#D29922"),
                    _ => ("#1C2838", "#58A6FF")
                };

                string riskCn = a.RiskLevelName switch
                {
                    "Critical" => "高危违规",
                    "High" => "高风险",
                    "Medium" => "中度预警",
                    _ => "低度提示"
                };

                ScreenAuditList.Add(new ScreenAuditItemViewModel
                {
                    Id = a.Id,
                    TimeFormatted = a.Timestamp.ToLocalTime().ToString("MM-dd HH:mm:ss"),
                    ProcessName = a.ProcessName,
                    WindowTitle = a.WindowTitle,
                    CategoryName = a.CategoryName,
                    RiskLevelName = riskCn,
                    RiskBadgeBackground = bg,
                    RiskBadgeForeground = fg,
                    MatchedKeywords = a.MatchedKeywords,
                    Snippet = a.Snippet,
                    IsAcknowledged = a.IsAcknowledged
                });
            }

            TxtAuditTotalCount.Text = $"{audits.Count} 次";
            TxtAuditPendingCount.Text = $"{unack} 条";
            TxtEmptyAuditNotice.Visibility = audits.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            await RefreshAuditBadgeAsync();
        }
        catch {}
    }

    private void UpdateAuditLockedViewState()
    {
        AuditLockedContainer.Visibility = _isScreenAuditUnlocked ? Visibility.Collapsed : Visibility.Visible;
        AuditUnlockedContainer.Visibility = _isScreenAuditUnlocked ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnUnlockAuditRecords_Click(object sender, RoutedEventArgs e)
    {
        RequestParentAuthorization("解锁审阅未成年人屏幕健康审计日志", async () =>
        {
            _isScreenAuditUnlocked = true;
            UpdateAuditLockedViewState();
            await LoadScreenAuditsAsync();
        });
    }

    private void BtnRelockAuditRecords_Click(object sender, RoutedEventArgs e)
    {
        _isScreenAuditUnlocked = false;
        UpdateAuditLockedViewState();
    }

    private async void BtnRefreshAuditList_Click(object sender, RoutedEventArgs e)
    {
        await LoadScreenAuditsAsync();
    }

    private async void BtnAcknowledgeAudit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string auditId)
        {
            await _ackAuditUseCase.ExecuteAsync(auditId);
            await LoadScreenAuditsAsync();
        }
    }

    private async void BtnTriggerAuditSample_Click(object sender, RoutedEventArgs e)
    {
        BtnTriggerAuditSample.IsEnabled = false;
        BtnTriggerAuditSample.Content = "⏳ 正在抓样分析中...";

        try
        {
            string currentApp = TxtCurrentApp.Text;
            string currentTitle = TxtCurrentTitle.Text;
            var result = await _performAuditUseCase.ExecuteAsync(currentApp, currentTitle);
            if (result != null)
            {
                System.Windows.MessageBox.Show(
                    $"【离线识别完成】命中疑似敏感内容：\n\n类别：{result.CategoryName}\n触发关键词：{result.MatchedKeywords}\n\n已记录至本地安全审计数据库。",
                    "屏幕内容安全分析报告",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "【离线识别完成】当前前台屏幕未检测到任何涉黄、涉赌、外挂、欺诈或暴恐敏感内容，画面安全健康！",
                    "屏幕内容安全分析报告",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            await RefreshAuditBadgeAsync();
            if (_isScreenAuditUnlocked)
            {
                await LoadScreenAuditsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"检测异常: {ex.Message}", "海豆·童伴守护", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnTriggerAuditSample.IsEnabled = true;
            BtnTriggerAuditSample.Content = "⚡ 立即抓样检测";
        }
    }

    #endregion

    private sealed class WpfSystemNotificationService : ISystemNotificationService
    {
        private readonly Func<Forms.NotifyIcon?> _getNotifyIcon;

        public WpfSystemNotificationService(Func<Forms.NotifyIcon?> getNotifyIcon)
        {
            _getNotifyIcon = getNotifyIcon;
        }

        public void ShowRestBreakAlert(int breakMinutes)
        {
            _getNotifyIcon()?.ShowBalloonTip(3000, "护眼休息提醒", $"已连续使用 45 分钟，建议休息远眺 {breakMinutes} 分钟。", Forms.ToolTipIcon.Info);
        }

        public void ShowCurfewWarning(string message)
        {
            _getNotifyIcon()?.ShowBalloonTip(3000, "就寝提醒", message, Forms.ToolTipIcon.Warning);
        }

        public void ShowTimeLimitReachedAlert()
        {
            _getNotifyIcon()?.ShowBalloonTip(3000, "今日时长提醒", "今日允许屏幕时长已达上限，请注意身心休息。", Forms.ToolTipIcon.Warning);
        }

        public void ShowAuditWarningAlert(string categoryName, string appName)
        {
            _getNotifyIcon()?.ShowBalloonTip(
                4000,
                "童伴守护健康提醒",
                $"在 [{appName}] 中检测到疑似【{categoryName}】，已记录本地审计日志。请注意信息安全与身心健康。",
                Forms.ToolTipIcon.Warning);
        }
    }

    
    #region 腾讯电脑管家风格护眼模式与科学用眼健康闹钟

    private void OnRestBreakRequired(int breakMinutes)
    {
        Dispatcher.Invoke(() =>
        {
            if (_activeBreakWindow != null && _activeBreakWindow.IsVisible) return;

            _activeBreakWindow = new EyeCareBreakWindow(breakMinutes);
            _activeBreakWindow.RestCompleted += () => _eyeCareTimer.NotifyRestCompleted();
            _activeBreakWindow.RestSnoozed += () => _eyeCareTimer.Snooze(5);
            _activeBreakWindow.Show();
        });
    }

    private void OnCurfewLockRequired(string promptText)
    {
        Dispatcher.Invoke(() =>
        {
            if (_activeCurfewWindow != null && _activeCurfewWindow.IsVisible) return;

            _activeCurfewWindow = new CurfewLockWindow(_authUseCase, promptText);
            _activeCurfewWindow.TemporaryUnlocked += () => _eyeCareTimer.Snooze(30);
            _activeCurfewWindow.Show();
        });
    }

    private void OnEyeCareStatusUpdated(int continuousMinutes, int remainingMinutes)
    {
        Dispatcher.Invoke(() =>
        {
            if (TxtEyeCareSubtitle != null)
            {
                TxtEyeCareSubtitle.Text = $"滤减蓝光呵护双眸，定时提醒科学用眼 (当前已用眼 {continuousMinutes} 分钟，距离下次远眺还有 {remainingMinutes} 分钟)";
            }
            if (TxtRestCountdown != null)
            {
                TxtRestCountdown.Text = remainingMinutes > 0 ? $"还剩 {remainingMinutes} 分钟" : "建议远眺";
            }
        });
    }

    private void BtnToggleEyeCare_Click(object sender, RoutedEventArgs e)
    {
        if (_blueLightFilter.IsFilterActive)
        {
            _blueLightFilter.RestoreDefault();
            UpdateEyeCareUiState(false);
        }
        else
        {
            bool ok = _blueLightFilter.ApplyWarmFilter(_filterWarmthPercent);
            UpdateEyeCareUiState(ok);
        }
    }

    private void UpdateEyeCareUiState(bool isActive)
    {
        if (isActive)
        {
            TxtEyeCareTitle.Text = "护眼模式保护中 · 蓝光滤减已生效";
            BadgeEyeCareActive.Visibility = Visibility.Visible;
            BtnToggleEyeCare.Content = "🌿 调节/关闭保护";
            BtnToggleEyeCare.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            BtnPolicyToggleEyeCare.Content = "🌿 护眼保护运行中 (点击关闭)";
            BtnPolicyToggleEyeCare.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
        }
        else
        {
            TxtEyeCareTitle.Text = "开启护眼模式，定制健康闹钟";
            BadgeEyeCareActive.Visibility = Visibility.Collapsed;
            BtnToggleEyeCare.Content = "✨ 开启保护";
            BtnToggleEyeCare.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
            BtnPolicyToggleEyeCare.Content = "✨ 开启护眼模式";
            BtnPolicyToggleEyeCare.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
        }
    }

    private void BtnQuickTakeBreak_Click(object sender, RoutedEventArgs e)
    {
        OnRestBreakRequired(_eyeCareTimer.RestDurationMinutes);
    }

    private void SliderEyeCareWarmth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _filterWarmthPercent = (int)e.NewValue;
        if (TxtWarmthPercent != null)
        {
            string desc = _filterWarmthPercent switch
            {
                < 55 => $"{_filterWarmthPercent}% 轻柔过滤",
                < 80 => $"{_filterWarmthPercent}% 经典护眼 (推荐)",
                _ => $"{_filterWarmthPercent}% 深度纸质/夜间"
            };
            TxtWarmthPercent.Text = desc;
        }

        if (_blueLightFilter != null && _blueLightFilter.IsFilterActive)
        {
            _blueLightFilter.ApplyWarmFilter(_filterWarmthPercent);
        }
    }

    private void ComboRestInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_eyeCareTimer == null || ComboRestInterval == null) return;
        int interval = ComboRestInterval.SelectedIndex switch
        {
            0 => 30,
            1 => 45,
            2 => 60,
            _ => 45
        };
        _eyeCareTimer.RestIntervalMinutes = interval;
    }

    private void ComboRestDuration_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_eyeCareTimer == null || ComboRestDuration == null) return;
        int duration = ComboRestDuration.SelectedIndex == 0 ? 3 : 5;
        _eyeCareTimer.RestDurationMinutes = duration;
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {

        _hookAdapter.Dispose();
        _heartbeatEngine.Flush();
        _dbContext.Dispose();

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        base.OnClosed(e);
    }

    #region 多语言支持与国家/IP 自适应切换

    private bool _isUpdatingLanguageUi = false;

    private void InitLanguageUi()
    {
        _isUpdatingLanguageUi = true;
        try
        {
            string pref = LanguageService.Instance.SelectedPreference;

            // 同步侧边栏 ComboBox
            if (CmbNavLanguage != null)
            {
                foreach (ComboBoxItem item in CmbNavLanguage.Items)
                {
                    if (item.Tag?.ToString() == pref)
                    {
                        CmbNavLanguage.SelectedItem = item;
                        break;
                    }
                }
            }

            // 同步关于面板 RadioButton
            if (RbLangAuto != null && RbLangZh != null && RbLangEn != null)
            {
                RbLangAuto.IsChecked = (pref == LanguageService.AutoMode);
                RbLangZh.IsChecked = (pref == LanguageService.Chinese);
                RbLangEn.IsChecked = (pref == LanguageService.English);
            }

            // 更新检测到的国家显示
            if (TxtDetectedCountry != null)
            {
                TxtDetectedCountry.Text = LanguageService.Instance.DetectedCountryDisplay;
            }
        }
        finally
        {
            _isUpdatingLanguageUi = false;
        }

        // 监听语言切换事件
        LanguageService.Instance.LanguageChanged += (s, langCode) =>
        {
            Dispatcher.Invoke(() =>
            {
                UpdateTrayMenuText();
                if (TxtDetectedCountry != null)
                {
                    TxtDetectedCountry.Text = LanguageService.Instance.DetectedCountryDisplay;
                }
            });
        };

        UpdateTrayMenuText();
    }

    private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingLanguageUi) return;
        if (CmbNavLanguage?.SelectedItem is ComboBoxItem item && item.Tag is string langPref)
        {
            LanguageService.Instance.SetLanguage(langPref);
            _isUpdatingLanguageUi = true;
            try
            {
                if (RbLangAuto != null && RbLangZh != null && RbLangEn != null)
                {
                    RbLangAuto.IsChecked = (langPref == LanguageService.AutoMode);
                    RbLangZh.IsChecked = (langPref == LanguageService.Chinese);
                    RbLangEn.IsChecked = (langPref == LanguageService.English);
                }
            }
            finally
            {
                _isUpdatingLanguageUi = false;
            }
        }
    }

    private void RbLang_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingLanguageUi) return;
        if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string langPref)
        {
            LanguageService.Instance.SetLanguage(langPref);
            _isUpdatingLanguageUi = true;
            try
            {
                if (CmbNavLanguage != null)
                {
                    foreach (ComboBoxItem item in CmbNavLanguage.Items)
                    {
                        if (item.Tag?.ToString() == langPref)
                        {
                            CmbNavLanguage.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            finally
            {
                _isUpdatingLanguageUi = false;
            }
        }
    }

    private void UpdateTrayMenuText()
    {
        try
        {
            if (_notifyIcon == null) return;
            _notifyIcon.Text = LanguageService.T("Tray.Tooltip", "海豆·童伴守护 (运行中)");

            if (_notifyIcon.ContextMenuStrip != null && _notifyIcon.ContextMenuStrip.Items.Count >= 4)
            {
                _notifyIcon.ContextMenuStrip.Items[0].Text = LanguageService.T("Tray.ShowMain", "📊 打开守护控制中心");
                _notifyIcon.ContextMenuStrip.Items[1].Text = LanguageService.T("App.LocalFirstBadge", "🛡️ 状态: 纯本地离线保护中");
                _notifyIcon.ContextMenuStrip.Items[3].Text = LanguageService.T("Nav.Exit", "🚪 退出童伴守护 (需家长密码)");
            }
        }
        catch { }
    }

    #endregion
}


public class BlockedRuleItemViewModel
{
    public string Domain { get; set; } = string.Empty;
}

public class UsageItemViewModel
{
    public string ProcessName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ActiveTimeDisplay { get; set; } = string.Empty;
    public string AfkTimeDisplay { get; set; } = string.Empty;
    public int LaunchCount { get; set; }

    public string CategoryColor { get; set; } = "#3FB950";
    public string CategoryBg { get; set; } = "#172A1C";
    public string IconEmoji { get; set; } = "📱";
    public double ActiveSeconds { get; set; }
}

public class ScreenAuditItemViewModel : INotifyPropertyChanged
{
    public string Id { get; set; } = string.Empty;
    public string TimeFormatted { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string RiskLevelName { get; set; } = string.Empty;
    public string RiskBadgeBackground { get; set; } = "#3D1D1D";
    public string RiskBadgeForeground { get; set; } = "#FF7B72";
    public string MatchedKeywords { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }

    public string StatusText => IsAcknowledged ? "✓ 已审阅" : "⚡ 标记知晓";
    public string StatusForeground => IsAcknowledged ? "#7D8590" : "#F0883E";

    public event PropertyChangedEventHandler? PropertyChanged = delegate { };
}







