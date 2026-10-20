using System;
using Haidou.ChildGuard.Application.DTOs;
using Haidou.ChildGuard.Application.Interfaces;
using Haidou.ChildGuard.Domain.Security.Repositories;
using Haidou.ChildGuard.Domain.Security.ValueObjects;

namespace Haidou.ChildGuard.Application.UseCases.Security;

public class AuthenticateParentUseCase
{
    private readonly ITotpService _totpService;
    private readonly IParentCredentialRepository _credentialRepository;

    public AuthenticateParentUseCase(ITotpService totpService, IParentCredentialRepository credentialRepository)
    {
        _totpService = totpService;
        _credentialRepository = credentialRepository;
    }

    #region 家长密码管理

    public bool IsPasswordConfigured()
    {
        return _credentialRepository.IsPasswordConfigured();
    }

    public bool SetInitialPassword(string plainPassword, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(plainPassword) || plainPassword.Trim().Length < 4)
        {
            errorMessage = "密码长度至少需要 4 位字符。";
            return false;
        }

        try
        {
            var credential = ParentPasswordCredential.CreateFromPassword(plainPassword.Trim());
            _credentialRepository.SavePasswordCredential(credential);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = "保存家长密码失败: " + ex.Message;
            return false;
        }
    }

    public bool VerifyPassword(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword)) return false;
        var credential = _credentialRepository.GetPasswordCredential();
        if (credential == null) return false;
        return credential.Verify(plainPassword.Trim());
    }

    /// <summary>
    /// 家长退出授权核销：优先匹配密码，备选匹配 6 位离线 TOTP 验证码
    /// </summary>
        /// <summary>
    /// 家长敏感操作安全核销（修改网站黑名单、添加/删除规则、开关系统级拦截）
    /// 优先匹配家长主密码，备选匹配 6 位离线 TOTP 验证码
    /// </summary>
    public bool AuthorizeAction(string passwordOrTotp) => AuthorizeExit(passwordOrTotp);

    public bool AuthorizeExit(string passwordOrTotp)
    {
        if (string.IsNullOrWhiteSpace(passwordOrTotp)) return false;
        string input = passwordOrTotp.Trim();

        // 1. 尝试主密码核销
        if (VerifyPassword(input))
        {
            return true;
        }

        // 2. 备选 6 位 TOTP 动态码核销
        if (input.Length == 6 && long.TryParse(input, out _))
        {
            string secret = _totpService.GetOrInitializeSecret();
            if (_totpService.VerifyCode(secret, input, out _))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region 离线 TOTP 2FA

    public TotpQrResultDto GetOrGenerateRegistrationQr(string deviceName)
    {
        string secret = _totpService.GetOrInitializeSecret();
        string qrBase64 = _totpService.GenerateQrBase64(secret, deviceName);
        string uri = _totpService.BuildUri(secret, deviceName);
        return new TotpQrResultDto(secret, qrBase64, uri);
    }

    public TotpQrResultDto ResetAndGenerateRegistrationQr(string deviceName)
    {
        string secret = _totpService.ResetSecret();
        string qrBase64 = _totpService.GenerateQrBase64(secret, deviceName);
        string uri = _totpService.BuildUri(secret, deviceName);
        return new TotpQrResultDto(secret, qrBase64, uri);
    }

    public bool VerifyParentCode(string secretBase32, string code)
    {
        return _totpService.VerifyCode(secretBase32, code, out _);
    }

    #endregion
}