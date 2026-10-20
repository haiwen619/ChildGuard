using System;
using Haidou.ChildGuard.Domain.Policies.Aggregates;

namespace Haidou.ChildGuard.Domain.Policies.Services;

public enum PolicyDecision
{
    Allowed,
    CurfewLock,
    ProcessBlocked,
    DailyTimeLimitReached
}

public class PolicyEvaluationService
{
    public PolicyDecision Evaluate(GuardianPolicyAggregate policy, string processName, double currentDailyActiveMinutes, TimeSpan currentTime)
    {
        if (policy.CurfewWindow.IsInWindow(currentTime))
        {
            return PolicyDecision.CurfewLock;
        }

        if (policy.IsBlocked(processName))
        {
            return PolicyDecision.ProcessBlocked;
        }

        if (policy.DailyTotalLimitMinutes > 0 && currentDailyActiveMinutes >= policy.DailyTotalLimitMinutes)
        {
            return PolicyDecision.DailyTimeLimitReached;
        }

        return PolicyDecision.Allowed;
    }
}
