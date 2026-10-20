using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Haidou.ChildGuard.Infrastructure.Persistence;

public class DatabaseContext : IDisposable
{
    public string ConnectionString { get; }

    public DatabaseContext(string? dbPath = null)
    {
        if (string.IsNullOrEmpty(dbPath))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string baseFolder = Path.Combine(appData, "HaidouChildGuard");
            Directory.CreateDirectory(baseFolder);
            dbPath = Path.Combine(baseFolder, "guardian_local.db");
        }

        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        Initialize();
    }

    private void Initialize()
    {
        using var conn = new SqliteConnection(ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS Activities (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProcessId INTEGER,
                ProcessName TEXT,
                WindowTitle TEXT,
                StartTimeUtc TEXT,
                DurationSeconds REAL,
                IsAfk INTEGER,
                Category TEXT
            );

            CREATE TABLE IF NOT EXISTS Policies (
                Id INTEGER PRIMARY KEY,
                DailyLimitMinutes INTEGER,
                SingleGameLimitMinutes INTEGER,
                CurfewStart TEXT,
                CurfewEnd TEXT,
                RestIntervalMinutes INTEGER,
                RestDurationMinutes INTEGER
            );

            CREATE TABLE IF NOT EXISTS ScreenAudits (
                Id TEXT PRIMARY KEY,
                Timestamp TEXT,
                ProcessName TEXT,
                WindowTitle TEXT,
                Category INTEGER,
                RiskLevel INTEGER,
                MatchedKeywords TEXT,
                Snippet TEXT,
                IsAcknowledged INTEGER
            );

            CREATE INDEX IF NOT EXISTS idx_act_time ON Activities(StartTimeUtc);
            CREATE INDEX IF NOT EXISTS idx_audit_time ON ScreenAudits(Timestamp);
        ";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() { }
}
