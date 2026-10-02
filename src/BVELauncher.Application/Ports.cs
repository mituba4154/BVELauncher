using BVELauncher.Core;

namespace BVELauncher.Application;

public interface IAssetReader
{
    Task<AssetReadResult> ReadAsync(string path, CancellationToken cancellationToken, AssetKind expectedKind = AssetKind.Unknown);
    Task<string> MaterializeAsync(string scenarioPath, VehicleOverride vehicle, string fallbackDirectory, CancellationToken cancellationToken);
}
public sealed record AssetReadResult(AssetKind Kind, IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<AssetReference> References, IReadOnlyList<DiagnosticItem> Diagnostics, string Fingerprint);
public interface ILibraryStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<LibrarySnapshot> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(LibrarySnapshot snapshot, CancellationToken cancellationToken = default);
    Task UpdateScenarioAsync(ScenarioEntry entry, CancellationToken cancellationToken = default);
    Task SaveInstallationAsync(InstallationRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InstallationRecord>> GetInstallationsAsync(CancellationToken cancellationToken = default);
    Task RecordLaunchAsync(Guid scenarioId, DateTime timeUtc, CancellationToken cancellationToken = default);
}
public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
public interface IRuntimeDetector
{
    Task<IReadOnlyList<BveRuntime>> DetectAsync(IEnumerable<BveRuntime> registered, CancellationToken cancellationToken = default);
    BveRuntime Inspect(string executablePath);
}
public interface IBveRuntimeAdapter
{
    RuntimeKind Kind { get; }
    LaunchPlan CreatePlan(ScenarioEntry scenario, BveRuntime runtime, string path, LaunchBehavior behavior, string? generatedPath);
}
public interface IProcessRunner
{
    Task<RunningLaunch> StartAsync(LaunchPlan plan, CancellationToken cancellationToken);
    Task WaitForExitAsync(int processId, CancellationToken cancellationToken);
    bool IsRuntimeRunning(BveRuntime runtime);
}
public interface IBveSettingsAdapter
{
    RuntimeKind Kind { get; }
    Task<BveSettingsSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default);
    Task SaveAsync(BveSettingsSnapshot snapshot, IReadOnlyDictionary<string, string> changes, CancellationToken cancellationToken = default);
}
public interface IBackupService
{
    Task<BackupRecord> CreateAsync(string path, string purpose, CancellationToken cancellationToken = default);
    Task RestoreAsync(BackupRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupRecord>> ListAsync(CancellationToken cancellationToken = default);
}
public interface IImportService
{
    Task<ImportPlan> AnalyzeAsync(ImportSource source, string destination, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<InstallationRecord> CommitAsync(ImportPlan plan, ConflictBehavior conflicts, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<UninstallResult> UninstallAsync(InstallationRecord record, LibrarySnapshot library, CancellationToken cancellationToken = default);
    Task RecoverAsync(CancellationToken cancellationToken = default);
    Task DiscardAsync(ImportPlan plan, CancellationToken cancellationToken = default);
}
public interface ILocalLog
{
    void Write(string level, string message, Exception? error = null);
}
