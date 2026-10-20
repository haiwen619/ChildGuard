using System;
using System.Diagnostics;
using System.Windows;
using Haidou.ChildGuard.Application.UseCases.Security;

namespace Haidou.ChildGuard.App;

public partial class CurfewLockWindow : Window
{
    private readonly AuthenticateParentUseCase _authUseCase;
    public event Action? TemporaryUnlocked;

    public CurfewLockWindow(AuthenticateParentUseCase authUseCase, string promptText)
    {
        InitializeComponent();
        _authUseCase = authUseCase;
        if (!string.IsNullOrWhiteSpace(promptText))
        {
            TxtCurfewPrompt.Text = promptText;
        }
    }

    private void BtnRequestParent_Click(object sender, RoutedEventArgs e)
    {
        PanelDefaultBtns.Visibility = Visibility.Collapsed;
        PanelAuth.Visibility = Visibility.Visible;
        InputPassword.Password = string.Empty;
        TxtError.Text = string.Empty;
        InputPassword.Focus();
    }

    private void BtnCancelAuth_Click(object sender, RoutedEventArgs e)
    {
        PanelAuth.Visibility = Visibility.Collapsed;
        PanelDefaultBtns.Visibility = Visibility.Visible;
    }

    private void BtnConfirmAuth_Click(object sender, RoutedEventArgs e)
    {
        string pwd = InputPassword.Password.Trim();
        if (string.IsNullOrEmpty(pwd))
        {
            TxtError.Text = "请输入家长密码或 6 位动态验证码";
            return;
        }

        bool ok = _authUseCase.AuthorizeAction(pwd);
        if (ok)
        {
            TemporaryUnlocked?.Invoke();
            Close();
        }
        else
        {
            TxtError.Text = "密码或验证码错误，无法解锁！";
        }
    }

    private void BtnShutdown_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("shutdown", "/s /t 10") { CreateNoWindow = true, UseShellExecute = false });
        }
        catch {}
    }
}

