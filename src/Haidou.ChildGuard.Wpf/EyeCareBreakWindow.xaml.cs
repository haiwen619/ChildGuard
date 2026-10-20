using System;
using System.Windows;
using System.Windows.Threading;

namespace Haidou.ChildGuard.App;

public partial class EyeCareBreakWindow : Window
{
    private readonly DispatcherTimer _timer;
    private int _remainingSeconds;

    public event Action? RestCompleted;
    public event Action? RestSnoozed;

    public EyeCareBreakWindow(int durationMinutes = 5)
    {
        InitializeComponent();
        _remainingSeconds = Math.Max(30, durationMinutes * 60);
        UpdateDisplay();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (s, e) =>
        {
            _remainingSeconds--;
            UpdateDisplay();
            if (_remainingSeconds <= 0)
            {
                _timer.Stop();
                RestCompleted?.Invoke();
                Close();
            }
        };
        _timer.Start();
    }

    private void UpdateDisplay()
    {
        int min = _remainingSeconds / 60;
        int sec = _remainingSeconds % 60;
        TxtCountdown.Text = $"{min:D2}:{sec:D2}";
    }

    private void BtnComplete_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        RestCompleted?.Invoke();
        Close();
    }

    private void BtnSnooze_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        RestSnoozed?.Invoke();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
