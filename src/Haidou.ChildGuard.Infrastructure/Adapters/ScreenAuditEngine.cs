using System;
using System.Threading;
using System.Threading.Tasks;
using Haidou.ChildGuard.Application.DTOs;
using Haidou.ChildGuard.Application.UseCases.Auditing;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public sealed class ScreenAuditEngine : IDisposable
{
    private readonly PerformScreenAuditUseCase _performAuditUseCase;
    private readonly Timer _timer;
    private readonly object _stateLock = new();

    private bool _isRunning;
    private bool _isAuditing;
    private string _latestProcessName = "Desktop";
    private string _latestWindowTitle = "Desktop";

    public bool IsEnabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 25;

    public event Action<ScreenAuditRecordDto>? AuditTriggered;

    public ScreenAuditEngine(PerformScreenAuditUseCase performAuditUseCase)
    {
        _performAuditUseCase = performAuditUseCase;
        _timer = new Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void UpdateActiveWindow(string processName, string windowTitle)
    {
        lock (_stateLock)
        {
            _latestProcessName = processName;
            _latestWindowTitle = windowTitle;
        }
    }

    public void Start()
    {
        lock (_stateLock)
        {
            if (_isRunning) return;
            _isRunning = true;
            _timer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(IntervalSeconds));
        }
    }

    public void Stop()
    {
        lock (_stateLock)
        {
            if (!_isRunning) return;
            _isRunning = false;
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private async void OnTimerTick(object? state)
    {
        if (!IsEnabled || !_isRunning) return;

        // 如果用户处于 AFK 挂机状态，自动跳过离线 OCR，保持极低 CPU 开销
        if (InputInactivityAdapter.IsAfk()) return;

        lock (_stateLock)
        {
            if (_isAuditing) return;
            _isAuditing = true;
        }

        try
        {
            string pName, wTitle;
            lock (_stateLock)
            {
                pName = _latestProcessName;
                wTitle = _latestWindowTitle;
            }

            var record = await _performAuditUseCase.ExecuteAsync(pName, wTitle);
            if (record != null)
            {
                AuditTriggered?.Invoke(record);
            }
        }
        catch
        {
            // 守护引擎健壮性保障
        }
        finally
        {
            lock (_stateLock)
            {
                _isAuditing = false;
            }
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
