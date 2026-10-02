namespace BVELauncher.Core;

public enum RuntimeKind { Unknown, Bve5, Bve6 }
public enum CpuArchitecture { Unknown, X86, X64, Arm64 }
public enum Severity { Ok, Info, Warning, Error, Unknown }
public enum AssetKind { Unknown, Scenario, Map, Vehicle, Parameters, Performance, Panel, VehicleSound, MotorNoise, StructureList, SoundList, StationList, SignalList, Train, Dll, Image, Audio, Model }
public enum Ownership { Existing, Managed, Imported, Unknown }
public enum ClickBehavior { Launch, Details, Select }
public enum LaunchBehavior { KeepVisible, Minimize, Hide }
public enum ConflictBehavior { Ask, Skip, Overwrite }
public enum SortOrder { Name, Route, Vehicle, Recent, Modified }

public sealed record BveRuntime(Guid Id, RuntimeKind Kind, string Version, string ExecutablePath,
    CpuArchitecture Architecture, string InstallDirectory, bool IsValid, string? PreferencesPath = null);
public sealed record ScenarioRoot(Guid Id, string Path);
public sealed record AssetFile(Guid Id, string Path, AssetKind Kind, long Size, DateTime LastWriteUtc, string? Fingerprint = null);
public sealed record AssetReference(string SourcePath, string Key, string RawPath, string ResolvedPath, AssetKind TargetKind, bool Exists);
public sealed record DiagnosticItem(Severity Severity, string Code, string SourcePath, string Reference = "", string ResolvedPath = "", string Detail = "");
public sealed record CompatibilityReport(IReadOnlyList<DiagnosticItem> Items, RuntimeKind RecommendedRuntime = RuntimeKind.Unknown)
{
    public bool HasWarnings => Items.Any(i => i.Severity is Severity.Warning or Severity.Error);
}

public sealed record ScenarioEntry(Guid Id, string Path, string Title, string RouteTitle, string VehicleTitle,
    string Author, string Comment, string? ImagePath, IReadOnlyList<AssetReference> References,
    DateTime LastWriteUtc, string Fingerprint)
{
    public string? Alias { get; init; }
    public string? CustomGroup { get; init; }
    public bool Favorite { get; init; }
    public bool Hidden { get; init; }
    public Guid? RuntimeId { get; init; }
    public string? DefaultVehiclePath { get; init; }
    public DateTime? LastLaunchUtc { get; init; }
    public CompatibilityReport Compatibility { get; init; } = new([]);
    public string DisplayTitle => string.IsNullOrWhiteSpace(Alias) ? (string.IsNullOrWhiteSpace(Title) ? System.IO.Path.GetFileNameWithoutExtension(Path) : Title) : Alias;
    public string Group => CustomGroup ?? (string.IsNullOrWhiteSpace(RouteTitle) ? System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Path)) ?? "" : RouteTitle);
}
public sealed record VehicleEntry(Guid Id, string Path, string Name, IReadOnlyList<string> ScenarioTitles,
    IReadOnlyList<AssetReference> References, DateTime LastWriteUtc, string Fingerprint)
{
    public string? Alias { get; init; }
    public string DisplayName => ScenarioTitles.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? Alias ?? Name;
    public string? Ats32 => References.FirstOrDefault(r => r.Key.Equals("Ats32", StringComparison.OrdinalIgnoreCase))?.ResolvedPath;
    public string? Ats64 => References.FirstOrDefault(r => r.Key.Equals("Ats64", StringComparison.OrdinalIgnoreCase))?.ResolvedPath;
}

public sealed record LibrarySnapshot(IReadOnlyList<ScenarioEntry> Scenarios, IReadOnlyList<VehicleEntry> Vehicles,
    IReadOnlyList<AssetFile> Assets, IReadOnlyList<AssetReference> References, IReadOnlyList<DiagnosticItem> Diagnostics)
{
    public static LibrarySnapshot Empty => new([], [], [], [], []);
}
public sealed record ScanProgress(int Files, int Scenarios, string CurrentPath);
public sealed record VehicleOverride(string VehiclePath, string Title);
public sealed record LaunchPlan(Guid ScenarioId, BveRuntime Runtime, string ScenarioPath, string WorkingDirectory,
    LaunchBehavior Behavior, string? GeneratedPath = null);
public sealed record RunningLaunch(int ProcessId, LaunchPlan Plan, DateTime StartedUtc);
public sealed record ImportSource(string Path);
public sealed record ImportOperation(string StagedPath, string RelativePath, string DestinationPath,
    bool Exists, string? OriginalHash, string NewHash, long Size, ConflictBehavior Conflict = ConflictBehavior.Ask);
public sealed record ImportPlan(Guid Id, ImportSource Source, string StagingDirectory, string Destination,
    string Confidence, IReadOnlyList<ImportOperation> Operations, IReadOnlyList<DiagnosticItem> Diagnostics,
    int Scenarios, int Vehicles);
public sealed record InstallationFile(string Path, string NewHash, string? OriginalHash, string? BackupPath,
    Ownership Ownership);
public sealed record InstallationRecord(Guid Id, string Source, string Destination, DateTime InstalledUtc,
    IReadOnlyList<InstallationFile> Files, bool RolledBack = false);
public sealed record UninstallResult(int Removed, IReadOnlyList<string> Preserved);
public sealed record ExtensionStatus(string Name, string Path, string Version, RuntimeKind Runtime, bool Installed);
public sealed record BackupRecord(Guid Id, string OriginalPath, string BackupPath, string Hash, DateTime CreatedUtc, string Purpose);

public sealed record AppSettings
{
    public int Version { get; init; } = 1;
    public string Language { get; init; } = "";
    public string Theme { get; init; } = "System";
    public ClickBehavior ClickBehavior { get; init; } = ClickBehavior.Launch;
    public LaunchBehavior LaunchBehavior { get; init; } = LaunchBehavior.KeepVisible;
    public bool GridView { get; init; } = true;
    public SortOrder Sort { get; init; }
    public Guid? DefaultRuntimeId { get; init; }
    public Guid? DefaultRootId { get; init; }
    public ConflictBehavior ImportConflicts { get; init; } = ConflictBehavior.Ask;
    public List<ScenarioRoot> Roots { get; init; } = [];
    public List<BveRuntime> Runtimes { get; init; } = [];
    public List<string> CollapsedGroups { get; init; } = [];
    public int WindowWidth { get; init; } = 1200;
    public int WindowHeight { get; init; } = 800;
    public int? WindowX { get; init; }
    public int? WindowY { get; init; }
    public bool Maximized { get; init; }
    public int BackupRetentionDays { get; init; } = 90;
    public int LogRetentionDays { get; init; } = 14;
}

public enum SettingType { Boolean, Integer, Decimal, Text, Choice, Path, Key }
public sealed record BveSettingDefinition(string Property, string ResourceKey, string Category,
    string XmlPath, SettingType Type, string Default, double? Minimum = null, double? Maximum = null,
    IReadOnlyList<string>? Choices = null, bool Verified = false);
public sealed record BveSettingsSnapshot(string Path, RuntimeKind Runtime, string OriginalHash,
    IReadOnlyDictionary<string, string> Values, IReadOnlyList<BveSettingDefinition> Definitions);
