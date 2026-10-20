using System;
using Haidou.ChildGuard.Domain.Security.ValueObjects;

namespace Haidou.ChildGuard.Domain.Security.Repositories;

public interface IParentCredentialRepository
{
    bool IsPasswordConfigured();
    ParentPasswordCredential? GetPasswordCredential();
    void SavePasswordCredential(ParentPasswordCredential credential);
    string GetOrGenerateTotpSecret(Func<string> generator);
    void SaveTotpSecret(string secretBase32);
}