using System.Text.Json;
using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Infrastructure;

public sealed class BackupService(AppPaths paths) : IBackupService
{
    public async Task<BackupRecord> CreateAsync(string path, string purpose, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var directory = Path.Combine(paths.Backups, id.ToString("N"));
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, Path.GetFileName(path));
        await using (var input = File.OpenRead(path))
        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true))
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        var hash = await AtomicFile.HashAsync(destination, cancellationToken).ConfigureAwait(false);
        var record = new BackupRecord(id, Path.GetFullPath(path), destination, hash, DateTime.UtcNow, purpose);
        await AtomicFile.WriteAsync(Path.Combine(directory, "backup.json"), JsonSerializer.SerializeToUtf8Bytes(record), cancellationToken).ConfigureAwait(false);
        return record;
    }
    public async Task RestoreAsync(BackupRecord record, CancellationToken cancellationToken = default)
    {
        if (await AtomicFile.HashAsync(record.BackupPath, cancellationToken).ConfigureAwait(false) != record.Hash) throw new InvalidDataException("Backup integrity check failed.");
        await AtomicFile.WriteAsync(record.OriginalPath, await File.ReadAllBytesAsync(record.BackupPath, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }
    public async Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<BackupRecord>();
        if (!Directory.Exists(paths.Backups)) return records;
        foreach (var file in Directory.EnumerateFiles(paths.Backups, "backup.json", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = JsonSerializer.Deserialize<BackupRecord>(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false));
            if (record != null) records.Add(record);
        }
        return records.OrderByDescending(r => r.CreatedUtc).ToArray();
    }
    public async Task ApplyRetentionAsync(int days, IReadOnlySet<string> protectedBackupPaths, CancellationToken cancellationToken = default)
    {
        var records = await ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var record in records.Where(r => r.CreatedUtc < DateTime.UtcNow.AddDays(-Math.Max(1, days)) && !protectedBackupPaths.Contains(r.BackupPath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 自動削除の対象は管理されたバックアップ領域だけ。
            PathSafety.DeleteOwnedDirectory(Path.GetDirectoryName(record.BackupPath)!, paths.Backups);
        }
    }
}
