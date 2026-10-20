using System;
using System.Threading.Tasks;
using Haidou.ChildGuard.Application.DTOs;
using Haidou.ChildGuard.Domain.Policies.Aggregates;
using Haidou.ChildGuard.Domain.Policies.Repositories;
using Haidou.ChildGuard.Domain.Policies.Services;

namespace Haidou.ChildGuard.Application.UseCases.Policies;

public class ManagePolicyUseCase
{
    private readonly IPolicyRepository _policyRepository;
    private readonly PolicyEvaluationService _evaluationService;

    public ManagePolicyUseCase(IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
        _evaluationService = new PolicyEvaluationService();
    }

    public async Task<PolicyConfigDto> GetCurrentPolicyAsync()
    {
        var aggregate = await _policyRepository.GetPolicyAsync();
        return new PolicyConfigDto(
            aggregate.DailyTotalLimitMinutes,
            aggregate.SingleGameLimitMinutes,
            aggregate.CurfewWindow.StartTime,
            aggregate.CurfewWindow.EndTime,
            aggregate.RestIntervalMinutes,
            aggregate.RestDurationMinutes
        );
    }

    public async Task UpdatePolicyAsync(PolicyConfigDto dto)
    {
        var aggregate = await _policyRepository.GetPolicyAsync();
        aggregate.UpdateLimits(dto.DailyTotalLimitMinutes, dto.SingleGameLimitMinutes);
        aggregate.UpdateCurfew(dto.CurfewStart, dto.CurfewEnd);
        await _policyRepository.SavePolicyAsync(aggregate);
    }

    public async Task<PolicyDecision> EvaluateAsync(string processName, double currentDailyActiveMinutes)
    {
        var aggregate = await _policyRepository.GetPolicyAsync();
        return _evaluationService.Evaluate(aggregate, processName, currentDailyActiveMinutes, DateTime.Now.TimeOfDay);
    }
}
