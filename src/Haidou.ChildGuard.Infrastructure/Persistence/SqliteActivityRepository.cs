using System;
using System.Threading.Tasks;
using Haidou.ChildGuard.Domain.Activities.Aggregates;
using Haidou.ChildGuard.Domain.Activities.Entities;
using Haidou.ChildGuard.Domain.Activities.Repositories;
using Haidou.ChildGuard.Domain.Activities.ValueObjects;
using Microsoft.Data.Sqlite;

namespace Haidou.ChildGuard.Infrastructure.Persistence;

public class SqliteActivityRepository : IActivityRepository
{
    private readonly DatabaseContext _context;

    public SqliteActivityRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task SaveSessionAsync(ActivitySession session)
    {
        await using var conn = new SqliteConnection(_context.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Activities 
            (ProcessId, ProcessName, WindowTitle, StartTimeUtc, DurationSeconds, IsAfk, Category)
            VALUES 
            (@pid, @pname, @title, @time, @duration, @afk, @cat);
        ";
        cmd.Parameters.AddWithValue("@pid", session.ProcessId);
        cmd.Parameters.AddWithValue("@pname", session.Process.ProcessName);
        cmd.Parameters.AddWithValue("@title", session.WindowTitle);
        cmd.Parameters.AddWithValue("@time", session.StartTimeUtc.ToString("O"));
        cmd.Parameters.AddWithValue("@duration", session.DurationSeconds);
        cmd.Parameters.AddWithValue("@afk", session.IsAfk ? 1 : 0);
        cmd.Parameters.AddWithValue("@cat", session.Process.Category);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<DailyTimelineAggregate> GetDailyTimelineAsync(string dateString)
    {
        var aggregate = new DailyTimelineAggregate(dateString);

        await using var conn = new SqliteConnection(_context.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ProcessId, ProcessName, WindowTitle, StartTimeUtc, DurationSeconds, IsAfk, Category
            FROM Activities
            WHERE StartTimeUtc LIKE @prefix
            ORDER BY StartTimeUtc ASC;
        ";
        cmd.Parameters.AddWithValue("@prefix", dateString + "%");

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var session = new ActivitySession(
                reader.GetInt64(0),
                (uint)reader.GetInt64(1),
                new ProcessInfo(reader.GetString(2), reader.IsDBNull(7) ? "Other" : reader.GetString(7)),
                reader.IsDBNull(3) ? "" : reader.GetString(3),
                DateTime.Parse(reader.GetString(4)),
                reader.GetDouble(5),
                reader.GetInt32(6) == 1
            );
            aggregate.AddSession(session);
        }

        return aggregate;
    }

    public async Task<int> PurgeOldSessionsAsync(int keepDays)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-keepDays);

        await using var conn = new SqliteConnection(_context.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Activities WHERE StartTimeUtc < @cutoff;";
        cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("O"));

        return await cmd.ExecuteNonQueryAsync();
    }
}
