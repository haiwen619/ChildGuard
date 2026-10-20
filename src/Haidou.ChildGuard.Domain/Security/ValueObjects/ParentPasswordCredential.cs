using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Haidou.ChildGuard.Domain.Common;

namespace Haidou.ChildGuard.Domain.Security.ValueObjects;

public class ParentPasswordCredential : ValueObject
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public string SaltBase64 { get; }
    public string HashBase64 { get; }
    public DateTime CreatedAtUtc { get; }

    public ParentPasswordCredential(string saltBase64, string hashBase64, DateTime? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(saltBase64)) throw new ArgumentException("Salt cannot be empty.", nameof(saltBase64));
        if (string.IsNullOrWhiteSpace(hashBase64)) throw new ArgumentException("Hash cannot be empty.", nameof(hashBase64));

        SaltBase64 = saltBase64;
        HashBase64 = hashBase64;
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
    }

    public static ParentPasswordCredential CreateFromPassword(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword))
            throw new ArgumentException("密码不能为空。", nameof(plainPassword));

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            plainPassword,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return new ParentPasswordCredential(
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash),
            DateTime.UtcNow);
    }

    public bool Verify(string inputPassword)
    {
        if (string.IsNullOrWhiteSpace(inputPassword)) return false;

        try
        {
            byte[] salt = Convert.FromBase64String(SaltBase64);
            byte[] expectedHash = Convert.FromBase64String(HashBase64);

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                inputPassword,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash);
        }
        catch
        {
            return false;
        }
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return SaltBase64;
        yield return HashBase64;
    }
}