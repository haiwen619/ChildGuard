using System;
using Haidou.ChildGuard.Application.DTOs;

namespace Haidou.ChildGuard.Infrastructure.Adapters;

public class ActivityHeartbeatEngine
{
    private readonly object _syncLock = new();
    private ActivitySessionDto? _currentSession;
    private DateTime _lastFlushTimeUtc = DateTime.UtcNow;

    public double MaxPulseSeconds { get; set; } = 30.0;
    public event Action<ActivitySessionDto>? SessionFlushed;

    public void Pulse(uint processId, string processName, string windowTitle)
    {
        lock (_syncLock)
        {
            var now = DateTime.UtcNow;
            bool isAfk = InputInactivityAdapter.IsAfk();

            if (_currentSession == null)
            {
                _currentSession = new ActivitySessionDto(processId, processName, windowTitle, now, 0, isAfk, "Uncategorized");
                _lastFlushTimeUtc = now;
                return;
            }

            bool isSame = _currentSession.ProcessId == processId &&
                          string.Equals(_currentSession.ProcessName, processName, StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(_currentSession.WindowTitle, windowTitle, StringComparison.Ordinal) &&
                          _currentSession.IsAfk == isAfk;

            double elapsed = (now - _lastFlushTimeUtc).TotalSeconds;

            if (isSame)
            {
                _currentSession = _currentSession with { DurationSeconds = _currentSession.DurationSeconds + elapsed };
                _lastFlushTimeUtc = now;

                if (_currentSession.DurationSeconds >= MaxPulseSeconds)
                {
                    FlushInternal();
                }
            }
            else
            {
                _currentSession = _currentSession with { DurationSeconds = _currentSession.DurationSeconds + elapsed };
                FlushInternal();
                _currentSession = new ActivitySessionDto(processId, processName, windowTitle, now, 0, isAfk, "Uncategorized");
                _lastFlushTimeUtc = now;
            }
        }
    }

    public void Flush()
    {
        lock (_syncLock)
        {
            if (_currentSession != null)
            {
                var now = DateTime.UtcNow;
                _currentSession = _currentSession with { DurationSeconds = _currentSession.DurationSeconds + (now - _lastFlushTimeUtc).TotalSeconds };
                FlushInternal();
            }
        }
    }

    private void FlushInternal()
    {
        if (_currentSession != null && _currentSession.DurationSeconds > 0.5)
        {
            SessionFlushed?.Invoke(_currentSession);
        }
        _currentSession = null;
        _lastFlushTimeUtc = DateTime.UtcNow;
    }
}
