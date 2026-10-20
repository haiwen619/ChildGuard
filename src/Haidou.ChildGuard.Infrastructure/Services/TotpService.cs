using System;
using Haidou.ChildGuard.Application.Interfaces;
using OtpNet;
using QRCoder;

namespace Haidou.ChildGuard.Infrastructure.Services;

public class TotpService : ITotpService
{
    private readonly LocalCredentialStore _store;

    public TotpService(LocalCredentialStore? store = null)
    {
        _store = store ?? new LocalCredentialStore();
    }

    public string GetOrInitializeSecret()
    {
        return _store.GetOrGenerateSecret(GenerateNewRawSecret);
    }

    public string ResetSecret()
    {
        string newSecret = GenerateNewRawSecret();
        _store.SaveSecret(newSecret);
        return newSecret;
    }

    private static string GenerateNewRawSecret()
    {
        byte[] secretBytes = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(secretBytes);
    }

    public string BuildUri(string secretBase32, string deviceName)
    {
        return $"otpauth://totp/HaidouChildGuard:{Uri.EscapeDataString(deviceName)}?secret={secretBase32}&issuer=HaidouChildGuard";
    }

    public string GenerateQrBase64(string secretBase32, string deviceName)
    {
        string uri = BuildUri(secretBase32, deviceName);
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        using var pngQrCode = new PngByteQRCode(qrData);
        byte[] qrBytes = pngQrCode.GetGraphic(20);
        return Convert.ToBase64String(qrBytes);
    }

    public bool VerifyCode(string secretBase32, string code, out long matchedTimeStep)
    {
        matchedTimeStep = 0;
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6) return false;

        try
        {
            byte[] secretBytes = Base32Encoding.ToBytes(secretBase32);
            var totp = new Totp(secretBytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
            return totp.VerifyTotp(code.Trim(), out matchedTimeStep, VerificationWindow.RfcSpecifiedNetworkDelay);
        }
        catch
        {
            return false;
        }
    }
}
