using System.Text.Json;
using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Infrastructure;

public sealed class ImportService(AppPaths paths, ArchiveStager stager, IAssetReader reader, IBackupService backups, ILibraryStore store, ILocalLog log) : IImportService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public Task<ImportPlan> AnalyzeAsync(ImportSource source, string destination, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
        => Task.Run(() => AnalyzeCoreAsync(source, destination, progress, cancellationToken), cancellationToken);
    private async Task<ImportPlan> AnalyzeCoreAsync(ImportSource source, string destination, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        PathSafety.EnsureNoLinks(destination);
        var id = Guid.NewGuid(); var stage = Path.Combine(paths.Staging, id.ToString("N"));
        Directory.CreateDirectory(stage);
        await AtomicFile.WriteAsync(Path.Combine(stage, ".owner.json"), JsonSerializer.SerializeToUtf8Bytes(new { Id = id, Source = source.Path }), cancellationToken).ConfigureAwait(false);
        string content = Path.Combine(stage, "Content"); Directory.CreateDirectory(content);
        try
        {
            await stager.StageAsync(source.Path, content, cancellationToken).ConfigureAwait(false);
            var files = Directory.EnumerateFiles(content, "*", SearchOption.AllDirectories).ToArray();
            if (files.Length == 0) throw new InvalidDataException("ImportEmpty");
            var scenarioDirectories = Directory.EnumerateDirectories(content, "*", SearchOption.AllDirectories)
                .Where(d => Path.GetFileName(d).Equals("Scenarios", StringComparison.OrdinalIgnoreCase)).ToArray();
            string baseDirectory = content; string confidence = "PreserveStructure";
            if (scenarioDirectories.Length == 1 && files.All(f => f.StartsWith(scenarioDirectories[0] + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            { baseDirectory = scenarioDirectories[0]; confidence = "ScenariosRoot"; }
            var operations = new List<ImportOperation>(); var diagnostics = new List<DiagnosticItem>();
            int scenarios = 0, vehicles = 0;
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(baseDirectory, file);
                var target = PathSafety.ResolveInside(destination, relative);
                if (!destinations.Add(target)) throw new InvalidDataException("ImportDuplicatePath");
                if (Directory.Exists(target)) throw new InvalidDataException("ImportDirectoryConflict");
                var exists = File.Exists(target);
                operations.Add(new(file, relative, target, exists, exists ? await AtomicFile.HashAsync(target, cancellationToken).ConfigureAwait(false) : null,
                    await AtomicFile.HashAsync(file, cancellationToken).ConfigureAwait(false), new FileInfo(file).Length));
                if (new FileInfo(file).Length < 16 * 1024 * 1024 && !new[] { ".dll", ".exe", ".wav", ".png", ".jpg", ".x" }.Contains(Path.GetExtension(file).ToLowerInvariant()))
                {
                    try
                    {
                        var parsed = await reader.ReadAsync(file, cancellationToken).ConfigureAwait(false);
                        if (parsed.Kind == AssetKind.Scenario) scenarios++;
                        if (parsed.Kind == AssetKind.Vehicle) vehicles++;
                        diagnostics.AddRange(parsed.Diagnostics);
                        foreach (var reference in parsed.References.Where(r => !r.Exists)) diagnostics.Add(new(Severity.Warning, "ReferenceMissing", file, reference.RawPath, reference.ResolvedPath));
                    }
                    catch (Exception ex) when (ex is IOException or ArgumentException or System.Text.DecoderFallbackException)
                    { diagnostics.Add(new(Severity.Unknown, "ParseFailed", file, Detail: ex.Message)); }
                }
                progress?.Report(new(operations.Count, scenarios, relative));
            }
            return new(id, source, stage, Path.GetFullPath(destination), confidence, operations, diagnostics, scenarios, vehicles);
        }
        catch
        {
            PathSafety.DeleteOwnedDirectory(stage, paths.Staging);
            throw;
        }
    }

    public async Task<InstallationRecord> CommitAsync(ImportPlan plan, ConflictBehavior conflicts, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(() => CommitCoreAsync(plan, conflicts, progress, cancellationToken), cancellationToken).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    private async Task<InstallationRecord> CommitCoreAsync(ImportPlan plan, ConflictBehavior conflicts, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var journalPath = Path.Combine(plan.StagingDirectory, "transaction.json");
        var journal = new InstallJournal(plan.Id, plan.Source.Path, plan.Destination, false, []);
        var destinationSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Preview後に書き換わった入力と配置先を、書き込む前に検出する。
        foreach (var operation in plan.Operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PathSafety.ResolveInside(plan.Destination, operation.RelativePath) != Path.GetFullPath(operation.DestinationPath) || !destinationSeen.Add(operation.DestinationPath)) throw new InvalidDataException("ImportPlanInvalid");
            PathSafety.EnsureNoLinks(operation.StagedPath);
            if (!operation.StagedPath.StartsWith(Path.GetFullPath(plan.StagingDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ImportStageEscape");
            if (await AtomicFile.HashAsync(operation.StagedPath, cancellationToken).ConfigureAwait(false) != operation.NewHash) throw new IOException("ImportSourceChanged");
            if (File.Exists(operation.DestinationPath) != operation.Exists || operation.Exists && await AtomicFile.HashAsync(operation.DestinationPath, cancellationToken).ConfigureAwait(false) != operation.OriginalHash) throw new IOException("ImportDestinationChanged");
            if (operation.Exists && conflicts == ConflictBehavior.Ask && operation.Conflict == ConflictBehavior.Ask) throw new InvalidOperationException("ImportConflictConfirmationRequired");
        }
        await SaveJournalAsync().ConfigureAwait(false);
        try
        {
            foreach (var operation in plan.Operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var behavior = operation.Conflict == ConflictBehavior.Ask ? conflicts : operation.Conflict;
                if (operation.Exists && behavior == ConflictBehavior.Skip) continue;
                PathSafety.EnsureNoLinks(operation.DestinationPath);
                string? backup = null;
                if (operation.Exists)
                {
                    var record = await backups.CreateAsync(operation.DestinationPath, "Import", cancellationToken).ConfigureAwait(false);
                    if (record.Hash != operation.OriginalHash) throw new IOException("ImportDestinationChanged");
                    backup = record.BackupPath;
                }
                var installed = new InstallationFile(operation.DestinationPath, operation.NewHash, operation.OriginalHash, backup, operation.Exists ? Ownership.Existing : Ownership.Managed);
                journal.Files.Add(installed);
                await SaveJournalAsync().ConfigureAwait(false);
                Directory.CreateDirectory(Path.GetDirectoryName(operation.DestinationPath)!);
                await AtomicFile.WriteAsync(operation.DestinationPath, await File.ReadAllBytesAsync(operation.StagedPath, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
                if (await AtomicFile.HashAsync(operation.DestinationPath, cancellationToken).ConfigureAwait(false) != operation.NewHash) throw new IOException("ImportVerificationFailed");
                progress?.Report(new(journal.Files.Count, plan.Scenarios, operation.RelativePath));
            }
            var installation = new InstallationRecord(plan.Id, plan.Source.Path, plan.Destination, DateTime.UtcNow, journal.Files.ToArray());
            await store.SaveInstallationAsync(installation, cancellationToken).ConfigureAwait(false);
            journal = journal with { Committed = true };
            await SaveJournalAsync().ConfigureAwait(false);
            PathSafety.DeleteOwnedDirectory(plan.StagingDirectory, paths.Staging);
            return installation;
        }
        catch (Exception original)
        {
            log.Write("Error", "Import failed; rolling back.", original);
            await RollbackAsync(journal, CancellationToken.None).ConfigureAwait(false);
            var persisted = (await store.GetInstallationsAsync(CancellationToken.None).ConfigureAwait(false)).FirstOrDefault(i => i.Id == plan.Id);
            if (persisted != null) await store.SaveInstallationAsync(persisted with { RolledBack = true }, CancellationToken.None).ConfigureAwait(false);
            PathSafety.DeleteOwnedDirectory(plan.StagingDirectory, paths.Staging);
            throw;
        }
        Task SaveJournalAsync() => AtomicFile.WriteAsync(journalPath, JsonSerializer.SerializeToUtf8Bytes(journal), CancellationToken.None);
    }
    private async Task RollbackAsync(InstallJournal journal, CancellationToken cancellationToken)
    {
        foreach (var file in journal.Files.AsEnumerable().Reverse())
        {
            PathSafety.EnsureNoLinks(file.Path);
            if (!File.Exists(file.Path)) continue;
            var hash = await AtomicFile.HashAsync(file.Path, cancellationToken).ConfigureAwait(false);
            if (hash == file.OriginalHash) continue;
            if (hash != file.NewHash) throw new IOException("RollbackModifiedFile: " + file.Path);
            if (file.BackupPath != null)
            {
                if (await AtomicFile.HashAsync(file.BackupPath, cancellationToken).ConfigureAwait(false) != file.OriginalHash) throw new InvalidDataException("BackupIntegrity");
                await AtomicFile.WriteAsync(file.Path, await File.ReadAllBytesAsync(file.BackupPath, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            }
            else if (file.Ownership == Ownership.Managed) File.Delete(file.Path);
        }
    }
    public Task<UninstallResult> UninstallAsync(InstallationRecord record, LibrarySnapshot library, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        int removed = 0; var preserved = new List<string>();
        var otherInstallations = (await store.GetInstallationsAsync(cancellationToken).ConfigureAwait(false)).Where(r => r.Id != record.Id && !r.RolledBack).ToArray();
        var ownFiles = record.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var file in record.Files)
        {
            cancellationToken.ThrowIfCancellationRequested(); PathSafety.EnsureNoLinks(file.Path);
            if (!File.Exists(file.Path)) continue;
            bool shared = otherInstallations.Any(r => r.Files.Any(f => f.Path.Equals(file.Path, StringComparison.OrdinalIgnoreCase))) ||
                library.References.Any(r => r.ResolvedPath.Equals(file.Path, StringComparison.OrdinalIgnoreCase) && !ownFiles.Contains(r.SourcePath));
            if (file.Ownership != Ownership.Managed || shared || await AtomicFile.HashAsync(file.Path, cancellationToken).ConfigureAwait(false) != file.NewHash)
            { preserved.Add(file.Path); continue; }
            File.Delete(file.Path); removed++;
        }
        await store.SaveInstallationAsync(record with { RolledBack = true }, cancellationToken).ConfigureAwait(false);
        return new UninstallResult(removed, preserved);
    }, cancellationToken);
    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(paths.Staging)) return;
        foreach (var directory in Directory.EnumerateDirectories(paths.Staging))
        {
            if (!File.Exists(Path.Combine(directory, ".owner.json"))) continue;
            var file = Path.Combine(directory, "transaction.json");
            if (File.Exists(file))
            {
                var journal = JsonSerializer.Deserialize<InstallJournal>(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false)) ?? throw new InvalidDataException("InvalidJournal");
                if (!journal.Committed)
                {
                    var persisted = (await store.GetInstallationsAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(r => r.Id == journal.Id && !r.RolledBack);
                    if (persisted == null) await RollbackAsync(journal, cancellationToken).ConfigureAwait(false);
                }
            }
            PathSafety.DeleteOwnedDirectory(directory, paths.Staging);
        }
    }
    public Task DiscardAsync(ImportPlan plan, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        PathSafety.DeleteOwnedDirectory(plan.StagingDirectory, paths.Staging);
    }, cancellationToken);
    private sealed record InstallJournal(Guid Id, string Source, string Destination, bool Committed, List<InstallationFile> Files);
}
