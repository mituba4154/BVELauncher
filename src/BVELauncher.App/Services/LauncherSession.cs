using BVELauncher.Application;
using BVELauncher.Bve;
using BVELauncher.Core;
using BVELauncher.Infrastructure;
using BVELauncher.Integrations;

namespace BVELauncher.App.Services;

public sealed class LauncherSession(AppPaths paths, ILibraryStore store, ISettingsStore settingsStore,
    IRuntimeDetector detector, LibraryScanner scanner, CompatibilityEngine diagnostics, IImportService imports,
    ProcessRunner runner, LibraryWatcher watcher, ExtensionDetector extensions, ILocalLog log, BackupService backups)
{
    public AppPaths Paths => paths;
    public ILibraryStore Store => store;
    public IImportService Imports => imports;
    public ProcessRunner Runner => runner;
    public ExtensionDetector Extensions => extensions;
    public AppSettings Settings { get; private set; } = new();
    public LibrarySnapshot Library { get; private set; } = LibrarySnapshot.Empty;
    public event Action? LibraryChanged;
    private Action? watchCallback;
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await store.InitializeAsync(cancellationToken);
        Settings = await settingsStore.LoadAsync(cancellationToken);
        Library = await store.LoadAsync(cancellationToken);
        LibraryChanged?.Invoke();
        await imports.RecoverAsync(cancellationToken);
        await runner.CleanupStaleGeneratedAsync(cancellationToken);
        var runtimes = await detector.DetectAsync(Settings.Runtimes, cancellationToken);
        var roots = Settings.Roots.ToList();
        if (roots.Count == 0)
            roots.AddRange(RuntimeDetector.DiscoverScenarioRoots(runtimes).Distinct(StringComparer.OrdinalIgnoreCase).Select(p => new ScenarioRoot(Guid.NewGuid(), p)));
        Settings = Settings with { Runtimes = runtimes.ToList(), Roots = roots };
        await settingsStore.SaveAsync(Settings, cancellationToken);
        var protectedBackups = (await store.GetInstallationsAsync(cancellationToken)).SelectMany(i => i.Files).Where(f => f.BackupPath != null).Select(f => f.BackupPath!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await backups.ApplyRetentionAsync(Settings.BackupRetentionDays, protectedBackups, cancellationToken);
        if (log is LocalLog localLog) await Task.Run(() => localLog.ApplyRetention(Settings.LogRetentionDays), cancellationToken);
    }
    public async Task ScanAsync(IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        Library = await scanner.ScanAsync(Settings.Roots, progress, cancellationToken);
        LibraryChanged?.Invoke();
        var reports = await Task.WhenAll(Library.Scenarios.Select(s => diagnostics.DiagnoseAsync(s, Library, Settings.Runtimes.FirstOrDefault(r => r.Id == s.RuntimeId), cancellationToken)));
        Library = Library with { Scenarios = Library.Scenarios.Select((s, i) => s with { Compatibility = reports[i] }).ToArray() };
        await store.SaveAsync(Library, cancellationToken);
        LibraryChanged?.Invoke();
    }
    public void Watch(Action changed) { watchCallback = changed; watcher.Watch(Settings.Roots.Select(r => r.Path), changed); }
    public void StopWatching() => watcher.Dispose();
    public void AcceptDiscovered(IReadOnlyList<ScenarioEntry> entries)
    {
        var ids = Library.Scenarios.Select(s => s.Id).ToHashSet();
        var added = entries.Where(s => !ids.Contains(s.Id)).ToArray();
        if (added.Length == 0) return;
        Library = Library with { Scenarios = Library.Scenarios.Concat(added).ToArray() };
        LibraryChanged?.Invoke();
    }
    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        bool changedRoots = !Settings.Roots.Select(r => r.Path).SequenceEqual(settings.Roots.Select(r => r.Path), StringComparer.OrdinalIgnoreCase);
        await settingsStore.SaveAsync(settings, cancellationToken); Settings = settings;
        if (changedRoots && watchCallback != null) watcher.Watch(Settings.Roots.Select(r => r.Path), watchCallback);
    }
    public async Task UpdateScenarioAsync(ScenarioEntry entry, CancellationToken cancellationToken = default)
    {
        await store.UpdateScenarioAsync(entry, cancellationToken);
        Library = Library with { Scenarios = Library.Scenarios.Select(s => s.Id == entry.Id ? entry : s).ToArray() };
        LibraryChanged?.Invoke();
    }
    public async Task ReportErrorAsync(string operation, Exception error)
    { log.Write("Error", operation, error); await Task.CompletedTask; }
}

