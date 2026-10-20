using System;
using System.Threading;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public sealed class EyeCareTimerEngine : IDisposable
{
    private readonly Timer _timer;
    private readonly object _lock = new();

    private bool _isEnabled = true;
    private int _restIntervalMinutes = 45;
    private int _restDurationMinutes = 5;
    private double _continuousActiveSeconds = 0;
    private bool _isInRestBreak = false;

    private TimeSpan _curfewStart = new(22, 0, 0); // 22:00
    private TimeSpan _curfewEnd = new(7, 0, 0);   // 07:00
    private bool _curfewLockActive = false;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    public int RestIntervalMinutes
    {
        get => _restIntervalMinutes;
        set => _restIntervalMinutes = Math.Clamp(value, 15, 120);
    }

    public int RestDurationMinutes
    {
        get => _restDurationMinutes;
        set => _restDurationMinutes = Math.Clamp(value, 2, 15);
    }

    public TimeSpan CurfewStart
    {
        get => _curfewStart;
        set => _curfewStart = value;
    }

    public TimeSpan CurfewEnd
    {
        get => _curfewEnd;
        set => _curfewEnd = value;
    }

    public event Action<int>? RestBreakRequired;
    public event Action<string>? CurfewLockRequired;
    public event Action<int, int>? StatusUpdated; // (continuousMinutes, remainingMinutes)

    public EyeCareTimerEngine()
    {
        _timer = new Timer(OnTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void OnTick(object? state)
    {
        if (!_isEnabled) return;

        lock (_lock)
        {
            var now = DateTime.Now;
            var currentTime = now.TimeOfDay;

            // 1. 就寝强制断屏 (Curfew Lock) 检查
            bool isCurfew = IsWithinCurfew(currentTime, _curfewStart, _curfewEnd);
            if (isCurfew)
            {
                if (!_curfewLockActive)
                {
                    _curfewLockActive = true;
                    CurfewLockRequired?.Invoke($"已到达就寝休息时段 ({_curfewStart:hh\\:mm} - {_curfewEnd:hh\\:mm})，请关机就寝！");
                }
                return;
            }
            else
            {
                _curfewLockActive = false;
            }

            // 2. 如果正在休息中，不累积用眼时间
            if (_isInRestBreak) return;

            // 3. AFK 挂机判定：若离开键盘鼠标超过 3 分钟，视作已经离开视线休息，自动重置连续用眼时长
            if (InputInactivityAdapter.IsAfk())
            {
                _continuousActiveSeconds = 0;
                StatusUpdated?.Invoke(0, _restIntervalMinutes);
                return;
            }

            // 4. 正常用眼时长累加
            _continuousActiveSeconds += 1.0;
            int currentMinutes = (int)(_continuousActiveSeconds / 60.0);
            int remainingMinutes = Math.Max(0, _restIntervalMinutes - currentMinutes);

            StatusUpdated?.Invoke(currentMinutes, remainingMinutes);

            // 5. 达到健康闹钟设定的用眼间隙，触发远眺休息提示
            if (_continuousActiveSeconds >= _restIntervalMinutes * 60)
            {
                _isInRestBreak = true;
                _continuousActiveSeconds = 0;
                RestBreakRequired?.Invoke(_restDurationMinutes);
            }
        }
    }

    private static bool IsWithinCurfew(TimeSpan current, TimeSpan start, TimeSpan end)
    {
        if (start < end)
        {
            return current >= start && current <= end;
        }
        else
        {
            // 跨午夜 (例如 22:00 到次日 07:00)
            return current >= start || current <= end;
        }
    }

    public void NotifyRestCompleted()
    {
        lock (_lock)
        {
            _isInRestBreak = false;
            _continuousActiveSeconds = 0;
        }
    }

    public void Snooze(int minutes = 5)
    {
        lock (_lock)
        {
            _isInRestBreak = false;
            _continuousActiveSeconds = Math.Max(0, (_restIntervalMinutes - minutes) * 60);
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
