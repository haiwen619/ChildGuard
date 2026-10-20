using System.Collections.Generic;
using Haidou.ChildGuard.Domain.Common;

namespace Haidou.ChildGuard.Domain.Security.ValueObjects;

public class TotpCredential : ValueObject
{
    public string SecretBase32 { get; }
    public long LastValidatedTimeStep { get; private set; }

    public TotpCredential(string secretBase32, long lastValidatedTimeStep = 0)
    {
        SecretBase32 = secretBase32;
        LastValidatedTimeStep = lastValidatedTimeStep;
    }

    public bool TryAcknowledgeTimeStep(long newTimeStep)
    {
        if (newTimeStep > LastValidatedTimeStep)
        {
            LastValidatedTimeStep = newTimeStep;
            return true;
        }
        return false;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return SecretBase32;
        yield return LastValidatedTimeStep;
    }
}
