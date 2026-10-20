using System.Threading.Tasks;
using Haidou.ChildGuard.Domain.Policies.Aggregates;

namespace Haidou.ChildGuard.Domain.Policies.Repositories;

public interface IPolicyRepository
{
    Task<GuardianPolicyAggregate> GetPolicyAsync();
    Task SavePolicyAsync(GuardianPolicyAggregate policy);
}
