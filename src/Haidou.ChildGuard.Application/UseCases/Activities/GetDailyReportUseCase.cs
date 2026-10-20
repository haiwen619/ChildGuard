using System;
using System.Linq;
using System.Threading.Tasks;
using Haidou.ChildGuard.Application.DTOs;
using Haidou.ChildGuard.Domain.Activities.Repositories;

namespace Haidou.ChildGuard.Application.UseCases.Activities;

public class GetDailyReportUseCase
{
    private readonly IActivityRepository _repository;

    public GetDailyReportUseCase(IActivityRepository repository)
    {
        _repository = repository;
    }

    public async Task<DailyReportDto> ExecuteAsync(DateTime date)
    {
        string dateString = date.ToString("yyyy-MM-dd");
        var timeline = await _repository.GetDailyTimelineAsync(dateString);

        var topApps = timeline.Sessions
            .GroupBy(s => s.Process.ProcessName)
            .Select(g => new DailyAppUsageDto(
                g.Key,
                g.First().Process.Category,
                g.Where(x => !x.IsAfk).Sum(x => x.DurationSeconds),
                g.Where(x => x.IsAfk).Sum(x => x.DurationSeconds),
                g.Count()
            ))
            .OrderByDescending(x => x.TotalActiveSeconds)
            .ToArray();

        double totalActive = timeline.GetTotalActiveSeconds();
        double totalAfk = timeline.Sessions.Where(s => s.IsAfk).Sum(s => s.DurationSeconds);

        return new DailyReportDto(dateString, totalActive, totalAfk, topApps);
    }
}
