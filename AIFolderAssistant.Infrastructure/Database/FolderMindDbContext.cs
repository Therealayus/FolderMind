using Microsoft.Data.Sqlite;

namespace AIFolderAssistant.Infrastructure.Database;

/// <summary>
/// Lightweight SQLite data-access layer built on <see cref="Microsoft.Data.Sqlite"/>.
/// Owns schema creation (idempotent) and all CRUD operations used by the repositories.
/// </summary>
public sealed class FolderMindDbContext
{
    private readonly string _connectionString;
    private readonly string _dbFilePath;

    public FolderMindDbContext(string? databaseFilePath = null)
    {
        _dbFilePath = databaseFilePath ?? GetDefaultDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(_dbFilePath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        EnsureCreated();
    }

    public static string GetDefaultDatabasePath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(folder, "FolderMind", "database", "FolderMind.db");
    }

    public SqliteConnection CreateConnection() => new(_connectionString);

    /// <summary>
    /// Creates all tables if they do not exist. Safe to call on every startup.
    /// </summary>
    public void EnsureCreated()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SchemaVersion (
                Version INTEGER PRIMARY KEY,
                AppliedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS MonitoredFolders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Path TEXT NOT NULL UNIQUE,
                IsEnabled INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                LastScannedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_MonitoredFolders_Path ON MonitoredFolders(Path);
            CREATE TABLE IF NOT EXISTS FolderAnalyses (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MonitoredFolderId INTEGER NOT NULL,
                FolderPath TEXT NOT NULL,
                OriginalName TEXT NOT NULL,
                SuggestedName TEXT NULL,
                Confidence REAL NOT NULL,
                Reason TEXT NULL,
                FileCount INTEGER NOT NULL,
                ProcessedFileCount INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                ResolvedAt TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_FolderAnalyses_FolderPath ON FolderAnalyses(FolderPath);
            CREATE TABLE IF NOT EXISTS Suggestions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FolderPath TEXT NOT NULL,
                OriginalName TEXT NOT NULL,
                SuggestedName TEXT NOT NULL,
                Confidence REAL NOT NULL,
                Reason TEXT NULL,
                Status INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                ResolvedAt TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Suggestions_FolderPath ON Suggestions(FolderPath);
            CREATE INDEX IF NOT EXISTS IX_Suggestions_Status ON Suggestions(Status);
            CREATE TABLE IF NOT EXISTS RenameHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FolderPath TEXT NOT NULL,
                OriginalName TEXT NOT NULL,
                NewName TEXT NOT NULL,
                Success INTEGER NOT NULL,
                ErrorMessage TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_RenameHistory_FolderPath ON RenameHistory(FolderPath);
            CREATE TABLE IF NOT EXISTS Settings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Key TEXT NOT NULL UNIQUE,
                Value TEXT NOT NULL,
                LastModified TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS AIUsage (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Provider TEXT NOT NULL,
                Model TEXT NOT NULL,
                RequestCount INTEGER NOT NULL,
                TotalTokensUsed REAL NOT NULL,
                LastRequest TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Diagnostics (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Level TEXT NOT NULL,
                Message TEXT NOT NULL,
                Details TEXT NULL,
                Timestamp TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Diagnostics_Timestamp ON Diagnostics(Timestamp);
            """;
        cmd.ExecuteNonQuery();

        using var versionCmd = conn.CreateCommand();
        versionCmd.CommandText = "INSERT OR IGNORE INTO SchemaVersion (Version, AppliedAt) VALUES (1, @at)";
        versionCmd.Parameters.AddWithValue("@at", DateTime.UtcNow.ToString("o"));
        versionCmd.ExecuteNonQuery();
    }

    // ---------- MonitoredFolders ----------

    public void InsertMonitoredFolder(MonitoredFolder folder)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO MonitoredFolders (Path, IsEnabled, CreatedAt, UpdatedAt, LastScannedAt)
            VALUES (@path, @isEnabled, @createdAt, @updatedAt, @lastScannedAt)
            """;
        cmd.Parameters.AddWithValue("@path", folder.Path);
        cmd.Parameters.AddWithValue("@isEnabled", folder.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@createdAt", folder.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@updatedAt", folder.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@lastScannedAt", folder.LastScannedAt.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public List<MonitoredFolder> GetMonitoredFolders()
    {
        var result = new List<MonitoredFolder>();
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Path, IsEnabled, CreatedAt, UpdatedAt, LastScannedAt FROM MonitoredFolders ORDER BY Path";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new MonitoredFolder
            {
                Id = reader.GetInt32(0),
                Path = reader.GetString(1),
                IsEnabled = reader.GetInt32(2) != 0,
                CreatedAt = DateTime.Parse(reader.GetString(3)),
                UpdatedAt = DateTime.Parse(reader.GetString(4)),
                LastScannedAt = DateTime.Parse(reader.GetString(5))
            });
        }
        return result;
    }

    public void UpdateMonitoredFolder(MonitoredFolder folder)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE MonitoredFolders
            SET IsEnabled = @isEnabled, UpdatedAt = @updatedAt, LastScannedAt = @lastScannedAt
            WHERE Path = @path
            """;
        cmd.Parameters.AddWithValue("@isEnabled", folder.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@updatedAt", folder.UpdatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@lastScannedAt", folder.LastScannedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@path", folder.Path);
        cmd.ExecuteNonQuery();
    }

    public void DeleteMonitoredFolder(string path)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM MonitoredFolders WHERE Path = @path";
        cmd.Parameters.AddWithValue("@path", path);
        cmd.ExecuteNonQuery();
    }

    // ---------- FolderAnalyses ----------

    public void InsertFolderAnalysis(FolderAnalysis analysis)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO FolderAnalyses
                (MonitoredFolderId, FolderPath, OriginalName, SuggestedName, Confidence, Reason, FileCount, ProcessedFileCount, CreatedAt, ResolvedAt)
            VALUES
                (@monitoredFolderId, @folderPath, @originalName, @suggestedName, @confidence, @reason, @fileCount, @processedFileCount, @createdAt, NULL)
            """;
        cmd.Parameters.AddWithValue("@monitoredFolderId", analysis.MonitoredFolderId);
        cmd.Parameters.AddWithValue("@folderPath", analysis.FolderPath);
        cmd.Parameters.AddWithValue("@originalName", analysis.OriginalName);
        cmd.Parameters.AddWithValue("@suggestedName", (object?)analysis.SuggestedName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@confidence", analysis.Confidence);
        cmd.Parameters.AddWithValue("@reason", (object?)analysis.Reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fileCount", analysis.FileCount);
        cmd.Parameters.AddWithValue("@processedFileCount", analysis.ProcessedFileCount);
        cmd.Parameters.AddWithValue("@createdAt", analysis.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public List<FolderAnalysis> GetFolderAnalyses()
    {
        var result = new List<FolderAnalysis>();
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, MonitoredFolderId, FolderPath, OriginalName, SuggestedName, Confidence, Reason, FileCount, ProcessedFileCount, CreatedAt, ResolvedAt FROM FolderAnalyses ORDER BY CreatedAt DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new FolderAnalysis
            {
                Id = reader.GetInt32(0),
                MonitoredFolderId = reader.GetInt32(1),
                FolderPath = reader.GetString(2),
                OriginalName = reader.GetString(3),
                SuggestedName = reader.IsDBNull(4) ? null : reader.GetString(4),
                Confidence = reader.GetDouble(5),
                Reason = reader.IsDBNull(6) ? null : reader.GetString(6),
                FileCount = reader.GetInt32(7),
                ProcessedFileCount = reader.GetInt32(8),
                CreatedAt = DateTime.Parse(reader.GetString(9)),
                ResolvedAt = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10))
            });
        }
        return result;
    }

    // ---------- Suggestions ----------

    public long InsertSuggestion(Suggestion suggestion)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Suggestions (FolderPath, OriginalName, SuggestedName, Confidence, Reason, Status, CreatedAt, ResolvedAt)
            VALUES (@folderPath, @originalName, @suggestedName, @confidence, @reason, @status, @createdAt, NULL);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@folderPath", suggestion.FolderPath);
        cmd.Parameters.AddWithValue("@originalName", suggestion.OriginalName);
        cmd.Parameters.AddWithValue("@suggestedName", suggestion.SuggestedName);
        cmd.Parameters.AddWithValue("@confidence", suggestion.Confidence);
        cmd.Parameters.AddWithValue("@reason", (object?)suggestion.Reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", (int)suggestion.Status);
        cmd.Parameters.AddWithValue("@createdAt", suggestion.CreatedAt.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    public List<Suggestion> GetSuggestions()
    {
        var result = new List<Suggestion>();
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, FolderPath, OriginalName, SuggestedName, Confidence, Reason, Status, CreatedAt, ResolvedAt FROM Suggestions ORDER BY CreatedAt DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Suggestion
            {
                Id = reader.GetInt32(0),
                FolderPath = reader.GetString(1),
                OriginalName = reader.GetString(2),
                SuggestedName = reader.GetString(3),
                Confidence = reader.GetDouble(4),
                Reason = reader.IsDBNull(5) ? null : reader.GetString(5),
                Status = (SuggestionStatus)reader.GetInt32(6),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                ResolvedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8))
            });
        }
        return result;
    }

    public void UpdateSuggestionStatus(int id, SuggestionStatus status, DateTime? resolvedAt)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Suggestions SET Status = @status, ResolvedAt = @resolvedAt WHERE Id = @id";
        cmd.Parameters.AddWithValue("@status", (int)status);
        cmd.Parameters.AddWithValue("@resolvedAt", resolvedAt.HasValue ? resolvedAt.Value.ToString("o") : DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // ---------- RenameHistory ----------

    public void InsertRenameHistory(RenameHistory history)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO RenameHistory (FolderPath, OriginalName, NewName, Success, ErrorMessage, CreatedAt)
            VALUES (@folderPath, @originalName, @newName, @success, @errorMessage, @createdAt)
            """;
        cmd.Parameters.AddWithValue("@folderPath", history.FolderPath);
        cmd.Parameters.AddWithValue("@originalName", history.OriginalName);
        cmd.Parameters.AddWithValue("@newName", history.NewName);
        cmd.Parameters.AddWithValue("@success", history.Success ? 1 : 0);
        cmd.Parameters.AddWithValue("@errorMessage", (object?)history.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@createdAt", history.CreatedAt.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public List<RenameHistory> GetRenameHistory()
    {
        var result = new List<RenameHistory>();
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, FolderPath, OriginalName, NewName, Success, ErrorMessage, CreatedAt FROM RenameHistory ORDER BY CreatedAt DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new RenameHistory
            {
                Id = reader.GetInt32(0),
                FolderPath = reader.GetString(1),
                OriginalName = reader.GetString(2),
                NewName = reader.GetString(3),
                Success = reader.GetInt32(4) != 0,
                ErrorMessage = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAt = DateTime.Parse(reader.GetString(6))
            });
        }
        return result;
    }

    public void ClearRenameHistory()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM RenameHistory";
        cmd.ExecuteNonQuery();
    }

    // ---------- Settings ----------

    public void InsertOrUpdateSetting(Setting setting)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Settings (Key, Value, LastModified) VALUES (@key, @value, @lastModified)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, LastModified = excluded.LastModified
            """;
        cmd.Parameters.AddWithValue("@key", setting.Key);
        cmd.Parameters.AddWithValue("@value", setting.Value);
        cmd.Parameters.AddWithValue("@lastModified", setting.LastModified.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public string? GetSetting(string key)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        return cmd.ExecuteScalar() as string;
    }

    public List<KeyValuePair<string, string>> GetAllSettings()
    {
        var result = new List<KeyValuePair<string, string>>();
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM Settings ORDER BY Key";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new KeyValuePair<string, string>(reader.GetString(0), reader.GetString(1)));
        }
        return result;
    }

    public void ResetSettings()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Settings";
        cmd.ExecuteNonQuery();
    }

    // ---------- AIUsage ----------

    public void InsertAIUsage(AIUsage usage)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AIUsage (Provider, Model, RequestCount, TotalTokensUsed, LastRequest)
            VALUES (@provider, @model, @requestCount, @totalTokensUsed, @lastRequest)
            """;
        cmd.Parameters.AddWithValue("@provider", usage.Provider);
        cmd.Parameters.AddWithValue("@model", usage.Model);
        cmd.Parameters.AddWithValue("@requestCount", usage.RequestCount);
        cmd.Parameters.AddWithValue("@totalTokensUsed", usage.TotalTokensUsed);
        cmd.Parameters.AddWithValue("@lastRequest", usage.LastRequest.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public List<AIUsage> GetAIUsage()
    {
        var result = new List<AIUsage>();
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Provider, Model, RequestCount, TotalTokensUsed, LastRequest FROM AIUsage ORDER BY LastRequest DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new AIUsage
            {
                Id = reader.GetInt32(0),
                Provider = reader.GetString(1),
                Model = reader.GetString(2),
                RequestCount = reader.GetInt32(3),
                TotalTokensUsed = reader.GetDouble(4),
                LastRequest = DateTime.Parse(reader.GetString(5))
            });
        }
        return result;
    }

    // ---------- Diagnostics ----------

    public void InsertDiagnostic(Diagnostic diagnostic)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Diagnostics (Level, Message, Details, Timestamp) VALUES (@level, @message, @details, @timestamp)";
        cmd.Parameters.AddWithValue("@level", diagnostic.Level);
        cmd.Parameters.AddWithValue("@message", diagnostic.Message);
        cmd.Parameters.AddWithValue("@details", (object?)diagnostic.Details ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@timestamp", diagnostic.Timestamp.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public List<Diagnostic> GetDiagnostics(int maxRows = 500)
    {
        var result = new List<Diagnostic>();
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Level, Message, Details, Timestamp FROM Diagnostics ORDER BY Id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", maxRows);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Diagnostic
            {
                Id = reader.GetInt32(0),
                Level = reader.GetString(1),
                Message = reader.GetString(2),
                Details = reader.IsDBNull(3) ? null : reader.GetString(3),
                Timestamp = DateTime.Parse(reader.GetString(4))
            });
        }
        return result;
    }

    public void ClearDiagnostics()
    {
        using var conn = CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Diagnostics";
        cmd.ExecuteNonQuery();
    }
}
