using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Haidou.ChildGuard.Domain.Auditing.Entities;
using Haidou.ChildGuard.Domain.Auditing.Repositories;
using Microsoft.Data.Sqlite;

namespace Haidou.ChildGuard.Infrastructure.Persistence;

public sealed class SqliteScreenAuditRepository : IScreenAuditRepository
{
    private readonly DatabaseContext _dbContext;

    public SqliteScreenAuditRepository(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveAuditRecordAsync(ScreenAuditRecord record)
    {
        using var conn = new SqliteConnection(_dbContext.ConnectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ScreenAudits 
            (Id, Timestamp, ProcessName, WindowTitle, Category, RiskLevel, MatchedKeywords, Snippet, IsAcknowledged)
            VALUES 
            (@id, @time, @pname, @title, @cat, @risk, @keywords, @snippet, @ack);
        ";
        cmd.Parameters.AddWithValue("@id", record.Id);
        cmd.Parameters.AddWithValue("@time", record.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@pname", record.ProcessName);
        cmd.Parameters.AddWithValue("@title", record.WindowTitle);
        cmd.Parameters.AddWithValue("@cat", (int)record.Category);
        cmd.Parameters.AddWithValue("@risk", (int)record.RiskLevel);
        cmd.Parameters.AddWithValue("@keywords", record.MatchedKeywords);
        cmd.Parameters.AddWithValue("@snippet", record.Snippet);
        cmd.Parameters.AddWithValue("@ack", record.IsAcknowledged ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<ScreenAuditRecord>> GetRecentAuditsAsync(int maxCount = 100)
    {
        var list = new List<ScreenAuditRecord>();
        using var conn = new SqliteConnection(_dbContext.ConnectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Timestamp, ProcessName, WindowTitle, Category, RiskLevel, MatchedKeywords, Snippet, IsAcknowledged
            FROM ScreenAudits
            ORDER BY Timestamp DESC
            LIMIT @limit;
        ";
        cmd.Parameters.AddWithValue("@limit", maxCount);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string id = reader.GetString(0);
            DateTime ts = DateTime.TryParse(reader.GetString(1), out var t) ? t : DateTime.UtcNow;
            string processName = reader.GetString(2);
            string windowTitle = reader.GetString(3);
            SensitiveCategory category = (SensitiveCategory)reader.GetInt32(4);
            AuditRiskLevel riskLevel = (AuditRiskLevel)reader.GetInt32(5);
            string keywords = reader.IsDBNull(6) ? "" : reader.GetString(6);
            string snippet = reader.IsDBNull(7) ? "" : reader.GetString(7);
            bool isAck = reader.GetInt32(8) == 1;

            list.Add(new ScreenAuditRecord(id, ts, processName, windowTitle, category, riskLevel, keywords, snippet, isAck));
        }

        return list;
    }

    public async Task<int> GetUnacknowledgedCountAsync()
    {
        using var conn = new SqliteConnection(_dbContext.ConnectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM ScreenAudits WHERE IsAcknowledged = 0;";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task MarkAuditAcknowledgedAsync(string auditId)
    {
        using var conn = new SqliteConnection(_dbContext.ConnectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE ScreenAudits SET IsAcknowledged = 1 WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", auditId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task PurgeOldAuditsAsync(int retentionDays = 7)
    {
        using var conn = new SqliteConnection(_dbContext.ConnectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays).ToString("O");
        cmd.CommandText = "DELETE FROM ScreenAudits WHERE Timestamp < @cutoff;";
        cmd.Parameters.AddWithValue("@cutoff", cutoff);
        await cmd.ExecuteNonQueryAsync();
    }
}
