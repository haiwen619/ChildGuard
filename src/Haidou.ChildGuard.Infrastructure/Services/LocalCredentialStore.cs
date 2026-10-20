using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Haidou.ChildGuard.Domain.Security.Repositories;
using Haidou.ChildGuard.Domain.Security.ValueObjects;

namespace Haidou.ChildGuard.Infrastructure.Services;

/// <summary>
/// 本地凭据安全存储（利用 Windows DPAPI 保护家长密码与 TOTP 密钥）
/// </summary>
public class LocalCredentialStore : IParentCredentialRepository
{
    private readonly string _baseFolder;
    private readonly string _totpFilePath;
    private readonly string _passwordFilePath;
    private readonly string _exitPermitFilePath;

    public LocalCredentialStore(string? baseFolder = null)
    {
        if (string.IsNullOrEmpty(baseFolder))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            _baseFolder = Path.Combine(appData, "HaidouChildGuard");
        }
        else
        {
            _baseFolder = baseFolder;
        }

        Directory.CreateDirectory(_baseFolder);
        _totpFilePath = Path.Combine(_baseFolder, "guardian_cred.dat");
        _passwordFilePath = Path.Combine(_baseFolder, "guardian_pwd.dat");
        _exitPermitFilePath = Path.Combine(_baseFolder, "guardian_exit.token");
    }

    #region 家长密码凭据管理 (Password Credential)

    public bool IsPasswordConfigured()
    {
        return GetPasswordCredential() != null;
    }

    public ParentPasswordCredential? GetPasswordCredential()
    {
        if (!File.Exists(_passwordFilePath)) return null;

        try
        {
            byte[] encrypted = File.ReadAllBytes(_passwordFilePath);
            byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            string json = Encoding.UTF8.GetString(plain);
            var model = JsonSerializer.Deserialize<PasswordStoreModel>(json);
            if (model != null && !string.IsNullOrWhiteSpace(model.SaltBase64) && !string.IsNullOrWhiteSpace(model.HashBase64))
            {
                return new ParentPasswordCredential(model.SaltBase64, model.HashBase64, model.CreatedAtUtc);
            }
        }
        catch
        {
            // 解密失败或损坏
        }
        return null;
    }

    public void SavePasswordCredential(ParentPasswordCredential credential)
    {
        var model = new PasswordStoreModel
        {
            SaltBase64 = credential.SaltBase64,
            HashBase64 = credential.HashBase64,
            CreatedAtUtc = credential.CreatedAtUtc
        };

        string json = JsonSerializer.Serialize(model);
        byte[] plain = Encoding.UTF8.GetBytes(json);
        byte[] encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_passwordFilePath, encrypted);
    }

    private class PasswordStoreModel
    {
        public string SaltBase64 { get; set; } = string.Empty;
        public string HashBase64 { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    #endregion

    #region TOTP 离线密钥管理

    public string GetOrGenerateTotpSecret(Func<string> generator) => GetOrGenerateSecret(generator);

    public void SaveTotpSecret(string secretBase32) => SaveSecret(secretBase32);

    public string GetOrGenerateSecret(Func<string> generator)
    {
        if (File.Exists(_totpFilePath))
        {
            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(_totpFilePath);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                string secret = Encoding.UTF8.GetString(plainBytes);
                if (!string.IsNullOrWhiteSpace(secret))
                {
                    return secret;
                }
            }
            catch
            {
                // 若解密失败（如更换机器上下文），则重新生成
            }
        }

        string newSecret = generator();
        SaveSecret(newSecret);
        return newSecret;
    }

    public void SaveSecret(string secretBase32)
    {
        byte[] plainBytes = Encoding.UTF8.GetBytes(secretBase32);
        byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_totpFilePath, encryptedBytes);
    }

    #endregion

    #region 退出授权许可管理 (Exit Permit Handshake)

    /// <summary>
    /// 家长密码核销通过后，签发短期有效合法退出令牌，通知看门狗放行退出
    /// </summary>
    public void IssueExitPermit(int validSeconds = 15)
    {
        try
        {
            long expireEpoch = DateTimeOffset.UtcNow.AddSeconds(validSeconds).ToUnixTimeSeconds();
            string tokenText = $"PERMIT:{expireEpoch}:{Guid.NewGuid():N}";
            File.WriteAllText(_exitPermitFilePath, tokenText, Encoding.UTF8);
        }
        catch {}
    }

    /// <summary>
    /// 检查是否存在未过期的合法退出令牌
    /// </summary>
    public bool IsExitPermitValid()
    {
        if (!File.Exists(_exitPermitFilePath)) return false;

        try
        {
            string tokenText = File.ReadAllText(_exitPermitFilePath, Encoding.UTF8);
            var parts = tokenText.Split(':');
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

    public void RevokeExitPermit()
    {
        try
        {
            if (File.Exists(_exitPermitFilePath))
            {
                File.Delete(_exitPermitFilePath);
            }
        }
        catch {}
    }

    #endregion
}