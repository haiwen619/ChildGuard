using System;
using System.Threading.Tasks;
using Haidou.ChildGuard.Domain.Policies.Aggregates;
using Haidou.ChildGuard.Domain.Policies.Repositories;
using Haidou.ChildGuard.Domain.Policies.ValueObjects;
using Microsoft.Data.Sqlite;

namespace Haidou.ChildGuard.Infrastructure.Persistence;

public class SqlitePolicyRepository : IPolicyRepository
{
    private readonly DatabaseContext _context;

    public SqlitePolicyRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<GuardianPolicyAggregate> GetPolicyAsync()
    {
        await using var conn = new SqliteConnection(_context.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DailyLimitMinutes, SingleGameLimitMinutes, CurfewStart, CurfewEnd, RestIntervalMinutes, RestDurationMinutes FROM Policies WHERE Id = 1;";

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new GuardianPolicyAggregate(
                1,
                reader.GetInt32(0),
                reader.GetInt32(1),
                new TimeWindow(TimeSpan.Parse(reader.GetString(2)), TimeSpan.Parse(reader.GetString(3))),
                reader.GetInt32(4),
                reader.GetInt32(5)
            );
        }

        // 默认策略
        return new GuardianPolicyAggregate(1, 120, 60, new TimeWindow(new TimeSpan(22, 0, 0), new TimeSpan(7, 0, 0)));
    }

    public async Task SavePolicyAsync(GuardianPolicyAggregate policy)
    {
        await using var conn = new SqliteConnection(_context.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Policies (Id, DailyLimitMinutes, SingleGameLimitMinutes, CurfewStart, CurfewEnd, RestIntervalMinutes, RestDurationMinutes)
            VALUES (1, @daily, @single, @cstart, @cend, @rinterval, @rduration)
            ON CONFLICT(Id) DO UPDATE SET
                DailyLimitMinutes = excluded.DailyLimitMinutes,
                SingleGameLimitMinutes = excluded.SingleGameLimitMinutes,
                CurfewStart = excluded.CurfewStart,
                CurfewEnd = excluded.CurfewEnd,
                RestIntervalMinutes = excluded.RestIntervalMinutes,
                RestDurationMinutes = excluded.RestDurationMinutes;
        ";
        cmd.Parameters.AddWithValue("@daily", policy.DailyTotalLimitMinutes);
        cmd.Parameters.AddWithValue("@single", policy.SingleGameLimitMinutes);
        cmd.Parameters.AddWithValue("@cstart", policy.CurfewWindow.StartTime.ToString());
        cmd.Parameters.AddWithValue("@cend", policy.CurfewWindow.EndTime.ToString());
        cmd.Parameters.AddWithValue("@rinterval", policy.RestIntervalMinutes);
        cmd.Parameters.AddWithValue("@rduration", policy.RestDurationMinutes);

        await cmd.ExecuteNonQueryAsync();
    }
}
