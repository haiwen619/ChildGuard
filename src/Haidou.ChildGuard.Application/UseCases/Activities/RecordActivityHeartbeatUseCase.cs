using System;
using System.Threading.Tasks;
using Haidou.ChildGuard.Application.DTOs;
using Haidou.ChildGuard.Domain.Activities.Entities;
using Haidou.ChildGuard.Domain.Activities.Repositories;
using Haidou.ChildGuard.Domain.Activities.ValueObjects;

namespace Haidou.ChildGuard.Application.UseCases.Activities;

public class RecordActivityHeartbeatUseCase
{
    private readonly IActivityRepository _repository;

    public RecordActivityHeartbeatUseCase(IActivityRepository repository)
    {
        _repository = repository;
    }

    public async Task ExecuteAsync(ActivitySessionDto dto)
    {
        var processInfo = new ProcessInfo(dto.ProcessName, dto.Category);
        var session = new ActivitySession(
            0,
            dto.ProcessId,
            processInfo,
            dto.WindowTitle,
            dto.StartTimeUtc,
            dto.DurationSeconds,
            dto.IsAfk
        );

        await _repository.SaveSessionAsync(session);
    }
}
