namespace Haidou.ChildGuard.Application.Interfaces;

public interface ITotpService
{
    string GetOrInitializeSecret();
    string ResetSecret();
    string BuildUri(string secretBase32, string deviceName);
    string GenerateQrBase64(string secretBase32, string deviceName);
    bool VerifyCode(string secretBase32, string code, out long matchedTimeStep);
}

public interface ISystemNotificationService
{
    void ShowRestBreakAlert(int breakMinutes);
    void ShowCurfewWarning(string message);
    void ShowTimeLimitReachedAlert();
}
