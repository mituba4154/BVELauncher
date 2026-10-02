using System.Text.Json;
using BVELauncher.Application;
using BVELauncher.Core;
using Microsoft.Data.Sqlite;

namespace BVELauncher.Infrastructure;

public sealed class SqliteLibraryStore(AppPaths paths, ILocalLog log) : ILibraryStore, IAssetCacheStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.Database, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }
    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        paths.EnsureDirectories();
        try { Initialize(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 11 or 26)
        {
            log.Write("Warning", "Database corruption; preserving database and rebuilding index.", ex);
            string preserved = paths.Database + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            File.Move(paths.Database, preserved);
            foreach (var suffix in new[] { "-wal", "-shm" }) if (File.Exists(paths.Database + suffix)) File.Move(paths.Database + suffix, preserved + suffix);
            Initialize();
        }
        void Initialize()
        {
            using var connection = Open();
            using var check = connection.CreateCommand();
            check.CommandText = "PRAGMA quick_check;";
            if (!string.Equals(check.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase)) throw new SqliteException("Database integrity failure.", 11);
            using var version = connection.CreateCommand();
            version.CommandText = "PRAGMA user_version;";
            int schema = Convert.ToInt32(version.ExecuteScalar());
            if (schema > 3) throw new InvalidDataException("Database schema is newer than this application.");
            if (schema is > 0 and < 3)
            {
                using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.Database + ".migration-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), Pooling = false }.ToString());
                backup.Open(); connection.BackupDatabase(backup);
            }
            using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                CREATE TABLE IF NOT EXISTS Entries(Kind TEXT NOT NULL, Id TEXT NOT NULL, Path TEXT NOT NULL COLLATE NOCASE, Json TEXT NOT NULL, PRIMARY KEY(Kind,Id), UNIQUE(Kind,Path));
                CREATE TABLE IF NOT EXISTS Installations(Id TEXT PRIMARY KEY, Json TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS Metadata(Key TEXT PRIMARY KEY, Json TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS LaunchHistory(Id INTEGER PRIMARY KEY, ScenarioId TEXT NOT NULL, TimeUtc TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS ScenarioOverrides(Id TEXT PRIMARY KEY, Json TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS AssetCache(Path TEXT PRIMARY KEY COLLATE NOCASE, Json TEXT NOT NULL);
                PRAGMA user_version=3;
                """;
            command.ExecuteNonQuery();
        }
    }, cancellationToken);

    public async Task<IReadOnlyList<CachedAssetRead>> LoadAssetCacheAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run<IReadOnlyList<CachedAssetRead>>(() =>
            {
                using var connection = Open(); using var command = connection.CreateCommand();
                command.CommandText = "SELECT Json FROM AssetCache";
                using var reader = command.ExecuteReader(); var entries = new List<CachedAssetRead>();
                while (reader.Read()) { cancellationToken.ThrowIfCancellationRequested(); entries.Add(JsonSerializer.Deserialize<CachedAssetRead>(reader.GetString(0))!); }
                return entries;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    public async Task SaveAssetCacheAsync(IEnumerable<CachedAssetRead> entries, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                using var connection = Open(); using var transaction = connection.BeginTransaction();
                using var temp = connection.CreateCommand(); temp.Transaction = transaction;
                temp.CommandText = "CREATE TEMP TABLE SeenCache(Path TEXT PRIMARY KEY COLLATE NOCASE);"; temp.ExecuteNonQuery();
                using var command = connection.CreateCommand(); command.Transaction = transaction;
                command.CommandText = "INSERT INTO AssetCache(Path,Json) VALUES($path,$json) ON CONFLICT(Path) DO UPDATE SET Json=excluded.Json WHERE Json<>excluded.Json; INSERT INTO SeenCache(Path) VALUES($path);";
                var path = command.Parameters.Add("$path", SqliteType.Text); var json = command.Parameters.Add("$json", SqliteType.Text);
                foreach (var entry in entries)
                { cancellationToken.ThrowIfCancellationRequested(); path.Value = entry.Path; json.Value = JsonSerializer.Serialize(entry); command.ExecuteNonQuery(); }
                using var cleanup = connection.CreateCommand(); cleanup.Transaction = transaction;
                cleanup.CommandText = "DELETE FROM AssetCache WHERE Path NOT IN (SELECT Path FROM SeenCache);"; cleanup.ExecuteNonQuery();
                transaction.Commit();
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task<LibrarySnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                using var connection = Open();
                var scenarios = Read<ScenarioEntry>("Scenario").Select(s => MergeOverrides(connection, s)).ToArray();
                var vehicles = Read<VehicleEntry>("Vehicle");
                var assets = Read<AssetFile>("Asset");
                using var metadata = connection.CreateCommand();
                metadata.CommandText = "SELECT Json FROM Metadata WHERE Key='Graph';";
                var graph = metadata.ExecuteScalar()?.ToString();
                var references = graph == null ? [] : JsonSerializer.Deserialize<AssetReference[]>(graph)!;
                return new LibrarySnapshot(scenarios, vehicles, assets, references, []);
                List<T> Read<T>(string kind)
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT Json FROM Entries WHERE Kind=$kind";
                    command.Parameters.AddWithValue("$kind", kind);
                    using var reader = command.ExecuteReader();
                    var items = new List<T>();
                    while (reader.Read()) { cancellationToken.ThrowIfCancellationRequested(); items.Add(JsonSerializer.Deserialize<T>(reader.GetString(0))!); }
                    return items;
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    public async Task SaveAsync(LibrarySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                using var connection = Open();
                using var transaction = connection.BeginTransaction();
                SaveRows("Scenario", snapshot.Scenarios.Select(s => (s.Id, s.Path, JsonSerializer.Serialize(MergeOverrides(connection, s, transaction)))));
                SaveRows("Vehicle", snapshot.Vehicles.Select(v => (v.Id, v.Path, JsonSerializer.Serialize(v))));
                SaveRows("Asset", snapshot.Assets.Select(a => (a.Id, a.Path, JsonSerializer.Serialize(a))));
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO Metadata(Key,Json) VALUES('Graph',$json) ON CONFLICT(Key) DO UPDATE SET Json=excluded.Json";
                command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(snapshot.References));
                command.ExecuteNonQuery();
                transaction.Commit();
                void SaveRows(string kind, IEnumerable<(Guid Id, string Path, string Json)> rows)
                {
                    // 変更のない行はUPDATEせず、走査に存在しない行だけ除去する。
                    using var temp = connection.CreateCommand();
                    temp.Transaction = transaction;
                    temp.CommandText = "CREATE TEMP TABLE IF NOT EXISTS Seen(Id TEXT PRIMARY KEY); DELETE FROM Seen;";
                    temp.ExecuteNonQuery();
                    foreach (var row in rows)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        using var entry = connection.CreateCommand();
                        entry.Transaction = transaction;
                        entry.CommandText = "INSERT INTO Entries(Kind,Id,Path,Json) VALUES($kind,$id,$path,$json) ON CONFLICT(Kind,Id) DO UPDATE SET Path=excluded.Path,Json=excluded.Json WHERE Json<>excluded.Json; INSERT INTO Seen(Id) VALUES($id);";
                        entry.Parameters.AddWithValue("$kind", kind); entry.Parameters.AddWithValue("$id", row.Id.ToString());
                        entry.Parameters.AddWithValue("$path", row.Path); entry.Parameters.AddWithValue("$json", row.Json); entry.ExecuteNonQuery();
                    }
                    using var cleanup = connection.CreateCommand();
                    cleanup.Transaction = transaction;
                    cleanup.CommandText = "DELETE FROM Entries WHERE Kind=$kind AND Id NOT IN (SELECT Id FROM Seen);";
                    cleanup.Parameters.AddWithValue("$kind", kind); cleanup.ExecuteNonQuery();
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    public async Task UpdateScenarioAsync(ScenarioEntry entry, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync("UPDATE Entries SET Json=$json WHERE Kind='Scenario' AND Id=$id; INSERT INTO ScenarioOverrides(Id,Json) VALUES($id,$json) ON CONFLICT(Id) DO UPDATE SET Json=excluded.Json;", entry.Id.ToString(), JsonSerializer.Serialize(entry), cancellationToken).ConfigureAwait(false);
    }
    public Task SaveInstallationAsync(InstallationRecord record, CancellationToken cancellationToken = default)
        => ExecuteAsync("INSERT INTO Installations(Id,Json) VALUES($id,$json) ON CONFLICT(Id) DO UPDATE SET Json=excluded.Json", record.Id.ToString(), JsonSerializer.Serialize(record), cancellationToken);
    public Task<IReadOnlyList<InstallationRecord>> GetInstallationsAsync(CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<InstallationRecord>>(() =>
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT Json FROM Installations";
        using var reader = command.ExecuteReader(); var result = new List<InstallationRecord>();
        while (reader.Read()) { cancellationToken.ThrowIfCancellationRequested(); result.Add(JsonSerializer.Deserialize<InstallationRecord>(reader.GetString(0))!); }
        return result;
    }, cancellationToken);
    public Task RecordLaunchAsync(Guid scenarioId, DateTime timeUtc, CancellationToken cancellationToken = default)
        => ExecuteAsync("INSERT INTO LaunchHistory(ScenarioId,TimeUtc) VALUES($id,$json)", scenarioId.ToString(), timeUtc.ToString("O"), cancellationToken);
    private async Task ExecuteAsync(string sql, string id, string json, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                using var connection = Open(); using var command = connection.CreateCommand(); command.CommandText = sql;
                command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$json", json); command.ExecuteNonQuery();
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    private static ScenarioEntry MergeOverrides(SqliteConnection connection, ScenarioEntry entry, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT Json FROM ScenarioOverrides WHERE Id=$id"; command.Parameters.AddWithValue("$id", entry.Id.ToString());
        var json = command.ExecuteScalar()?.ToString(); if (json == null) return entry;
        var preferences = JsonSerializer.Deserialize<ScenarioEntry>(json)!;
        return entry with { Alias = preferences.Alias, CustomGroup = preferences.CustomGroup, Favorite = preferences.Favorite,
            Hidden = preferences.Hidden, RuntimeId = preferences.RuntimeId, DefaultVehiclePath = preferences.DefaultVehiclePath,
            LastLaunchUtc = preferences.LastLaunchUtc };
    }
}
