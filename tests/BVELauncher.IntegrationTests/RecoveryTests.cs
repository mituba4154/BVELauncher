using System.Diagnostics;
using System.Text.Json;
using BVELauncher.Application;
using BVELauncher.Bve;
using BVELauncher.Core;
using BVELauncher.Infrastructure;
using BVELauncher.Integrations;
using Microsoft.Data.Sqlite;

namespace BVELauncher.IntegrationTests;
public sealed class RecoveryTests
{
    [Fact]
    public async Task PreferencesWaitForTransientReaderBeforeAtomicReplace()
    {
        using var env = new TestEnvironment(); var path = Path.Combine(env.Content, "preferences.xml");
        await File.WriteAllTextAsync(path, "<Preferences><HeadMotion>25</HeadMotion></Preferences>");
        var adapter = new Bve6SettingsAdapter(env.Backups); var snapshot = await adapter.ReadAsync(path);
        using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var saving = adapter.SaveAsync(snapshot, new Dictionary<string, string> { ["HeadMotion"] = "40" });
        await Task.Delay(250); reader.Dispose(); await saving;
        Assert.Equal("40", (await adapter.ReadAsync(path)).Values["HeadMotion"]);
    }
    [Fact]
    public async Task PreferencesRetryStillRejectsExternalModification()
    {
        using var env = new TestEnvironment(); var path = Path.Combine(env.Content, "preferences.xml");
        await File.WriteAllTextAsync(path, "<Preferences><HeadMotion>25</HeadMotion></Preferences>");
        var adapter = new Bve6SettingsAdapter(env.Backups); var snapshot = await adapter.ReadAsync(path);
        using var blocker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var saving = adapter.SaveAsync(snapshot, new Dictionary<string, string> { ["HeadMotion"] = "40" });
        await Task.Delay(150);
        await File.WriteAllTextAsync(path, "<Preferences><HeadMotion>60</HeadMotion></Preferences>");
        blocker.Dispose();
        // 同時書込中の共有違反で中止する場合も、hash変更で拒否する場合も元データを守る。
        await Assert.ThrowsAsync<IOException>(() => saving);
        Assert.Equal("60", (await adapter.ReadAsync(path)).Values["HeadMotion"]);
    }
    [Fact]
    public async Task AtomicCopyRejectsUnexpectedContentBeforeReplacingExistingFile()
    {
        using var env = new TestEnvironment(); var source = Path.Combine(env.Root, "source.txt"); var destination = Path.Combine(env.Content, "keep.txt");
        await File.WriteAllTextAsync(source, "unexpected"); await File.WriteAllTextAsync(destination, "original");
        await Assert.ThrowsAsync<InvalidDataException>(() => AtomicFile.CopyAsync(source, destination, expectedHash: new string('0', 64)));
        Assert.Equal("original", await File.ReadAllTextAsync(destination)); Assert.Single(Directory.GetFiles(env.Content));
    }
    [Fact]
    public async Task InputDevicesUseNativeLowercaseExtensionlessKeysWithoutLoadingDll()
    {
        using var env = new TestEnvironment(); var directory = Path.Combine(env.Content, "Input Devices"); Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "Example.Input.dll"), "self authored invalid PE");
        var device = Assert.Single(InputDeviceCatalog.Read(env.Content)); Assert.Equal("example.input", device.Key);
    }
    [Fact]
    public async Task MarkerWriteFailureRemovesUnlaunchedGeneratedFile()
    {
        using var env = new TestEnvironment();
        var impossiblePaths = new AppPaths(Path.Combine(env.Root, "OtherApp"));
        Directory.CreateDirectory(impossiblePaths.Root); await File.WriteAllTextAsync(impossiblePaths.Generated, "blocks directory creation");
        var generated = Path.Combine(env.Content, ".BVELauncher-" + Guid.NewGuid().ToString("N") + ".txt"); await File.WriteAllTextAsync(generated, "owned");
        var runtime = new BveRuntime(Guid.NewGuid(), RuntimeKind.Bve6, "6", "missing.exe", CpuArchitecture.X64, env.Root, true);
        await Assert.ThrowsAnyAsync<IOException>(() => new ProcessRunner(impossiblePaths, env.Log).StartAsync(new(Guid.NewGuid(), runtime, generated, env.Root, LaunchBehavior.KeepVisible, generated), default));
        Assert.False(File.Exists(generated));
    }
    [Fact]
    public async Task HistoryFailureAfterLaunchStillReturnsTrackableProcess()
    {
        using var env = new TestEnvironment(); var runner = new RecordingRunner();
        var runtime = new BveRuntime(Guid.NewGuid(), RuntimeKind.Bve6, "6", "unused.exe", CpuArchitecture.X64, env.Root, true);
        var useCase = new LaunchScenario(new BveAssetReader(), runner, new BrokenHistoryStore(), [], env.Log);
        var result = await useCase.ExecuteAsync(new(Guid.NewGuid(), runtime, "unused.txt", env.Root, LaunchBehavior.Hide));
        Assert.Equal(12345, result.ProcessId); await useCase.WaitAsync(result); Assert.True(runner.Waited);
    }
    [Fact]
    public async Task MigrationBacksUpVersionOneAndRetainsUserOverridesDuringStaleScan()
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        using (var connection = new SqliteConnection("Data Source=" + env.Paths.Database))
        { await connection.OpenAsync(); using var command = connection.CreateCommand(); command.CommandText = "PRAGMA user_version=1"; await command.ExecuteNonQueryAsync(); }
        await env.Store.InitializeAsync(); Assert.Single(Directory.GetFiles(Path.GetDirectoryName(env.Paths.Database)!, "*.migration-*"));
        var path = Path.Combine(env.Content, "scenario.txt"); await File.WriteAllTextAsync(path, "BveTs Scenario 2.00\nTitle=Original\n");
        var scanner = new LibraryScanner(new BveAssetReader(), env.Store, env.Log);
        var snapshot = await scanner.ScanAsync([new(Guid.NewGuid(), env.Content)]);
        var scenario = Assert.Single(snapshot.Scenarios);
        await env.Store.UpdateScenarioAsync(scenario with { Favorite = true, Alias = "Kept" });
        await env.Store.SaveAsync(snapshot);
        var loaded = Assert.Single((await env.Store.LoadAsync()).Scenarios);
        Assert.True(loaded.Favorite); Assert.Equal("Kept", loaded.Alias);
    }
    [Fact]
    public async Task IncompleteJournalRestoresExistingFileAndRemovesOnlyManagedFile()
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        var existing = Path.Combine(env.Content, "existing.txt"); var managed = Path.Combine(env.Content, "managed.txt");
        await File.WriteAllTextAsync(existing, "original"); var backup = await env.Backups.CreateAsync(existing, "Import");
        await File.WriteAllTextAsync(existing, "replacement"); await File.WriteAllTextAsync(managed, "new");
        var files = new[] { new InstallationFile(existing, await AtomicFile.HashAsync(existing), backup.Hash, backup.BackupPath, Ownership.Existing), new InstallationFile(managed, await AtomicFile.HashAsync(managed), null, null, Ownership.Managed) };
        var id = Guid.NewGuid(); var stage = Path.Combine(env.Paths.Staging, id.ToString("N")); Directory.CreateDirectory(stage);
        await File.WriteAllTextAsync(Path.Combine(stage, ".owner.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(stage, "transaction.json"), JsonSerializer.Serialize(new { Id = id, Source = "source.zip", Destination = env.Content, Committed = false, Files = files }));
        var service = new ImportService(env.Paths, new(new()), new BveAssetReader(), env.Backups, env.Store, env.Log);
        await service.RecoverAsync();
        Assert.Equal("original", await File.ReadAllTextAsync(existing)); Assert.False(File.Exists(managed)); Assert.False(Directory.Exists(stage));
    }
    [Fact]
    public async Task TamperedJournalCannotRemoveOutsideDestination()
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        var outside = Path.Combine(env.Root, "outside.txt"); await File.WriteAllTextAsync(outside, "keep");
        var id = Guid.NewGuid(); var stage = Path.Combine(env.Paths.Staging, id.ToString("N")); Directory.CreateDirectory(stage);
        await File.WriteAllTextAsync(Path.Combine(stage, ".owner.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(stage, "transaction.json"), JsonSerializer.Serialize(new { Id = id, Source = "source.zip", Destination = env.Content, Committed = false, Files = new[] { new InstallationFile(outside, await AtomicFile.HashAsync(outside), null, null, Ownership.Managed) } }));
        var service = new ImportService(env.Paths, new(new()), new BveAssetReader(), env.Backups, env.Store, env.Log);
        await Assert.ThrowsAsync<InvalidDataException>(() => service.RecoverAsync()); Assert.Equal("keep", await File.ReadAllTextAsync(outside)); Assert.True(Directory.Exists(stage));
    }
    [Fact]
    public async Task GeneratedCleanupRetainsActiveLeaseAndDeletesExitedLease()
    {
        using var env = new TestEnvironment(); using var current = Process.GetCurrentProcess();
        string active = Path.Combine(env.Content, ".BVELauncher-active.txt"), stale = Path.Combine(env.Content, ".BVELauncher-stale.txt");
        await File.WriteAllTextAsync(active, "active"); await File.WriteAllTextAsync(stale, "stale");
        await File.WriteAllTextAsync(Path.Combine(env.Paths.Generated, "active.json"), JsonSerializer.Serialize(new { Path = active, ProcessId = current.Id, StartedUtc = current.StartTime.ToUniversalTime() }));
        await File.WriteAllTextAsync(Path.Combine(env.Paths.Generated, "stale.json"), JsonSerializer.Serialize(new { Path = stale, ProcessId = int.MaxValue, StartedUtc = DateTime.UtcNow }));
        await new ProcessRunner(env.Paths, env.Log).CleanupStaleGeneratedAsync(); Assert.True(File.Exists(active)); Assert.False(File.Exists(stale));
    }
    [Fact]
    public async Task ExplicitStandardVehicleBypassesSavedDefault()
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        var scenarioPath = Path.Combine(env.Content, "scenario.txt"); var exe = Path.Combine(env.Root, "BveTs.exe");
        await File.WriteAllTextAsync(scenarioPath, "BveTs Scenario 2.00\nVehicle=original.txt\n"); await File.WriteAllTextAsync(exe, "not executed");
        var scenario = new ScenarioEntry(Guid.NewGuid(), scenarioPath, "Test", "", "", "", "", null, [], DateTime.UtcNow, "") { DefaultVehiclePath = Path.Combine(env.Content, "override.txt") };
        var runtime = new BveRuntime(Guid.NewGuid(), RuntimeKind.Bve6, "6", exe, CpuArchitecture.X64, env.Root, true);
        var launch = new LaunchScenario(new BveAssetReader(), new ProcessRunner(env.Paths, env.Log), env.Store, [new Bve6RuntimeAdapter()]);
        var standard = await launch.PrepareAsync(scenario, [runtime], new(), env.Paths.Generated, useDefaultVehicle: false);
        Assert.Null(standard.GeneratedPath); Assert.Equal(scenarioPath, standard.ScenarioPath);
        var saved = await launch.PrepareAsync(scenario, [runtime], new(), env.Paths.Generated);
        Assert.NotNull(saved.GeneratedPath); Assert.Equal("original.txt", BveDocument.Parse(await File.ReadAllBytesAsync(scenarioPath)).Get("Vehicle"));
    }
    [Fact]
    public async Task ExtensionDetectionReadsPresenceWithoutLoadingDllAndRejectsUnrelatedInstaller()
    {
        using var env = new TestEnvironment(); var input = Path.Combine(env.Content, "Input Devices"); Directory.CreateDirectory(input);
        var dll = Path.Combine(input, "BveEx.test.dll"); await File.WriteAllTextAsync(dll, "self authored invalid PE");
        var runtime = new BveRuntime(Guid.NewGuid(), RuntimeKind.Bve6, "6", "", CpuArchitecture.X64, env.Content, true);
        var detector = new ExtensionDetector(); Assert.Equal(dll, Assert.Single(await detector.DetectAsync([runtime])).Path);
        await Assert.ThrowsAsync<InvalidDataException>(() => detector.RunLocalInstallerAsync(dll));
    }
    private sealed class RecordingRunner : IProcessRunner
    {
        public bool Waited { get; private set; }
        public Task<RunningLaunch> StartAsync(LaunchPlan plan, CancellationToken token) => Task.FromResult(new RunningLaunch(12345, plan, DateTime.UtcNow));
        public Task WaitForExitAsync(int id, CancellationToken token) { Waited = true; return Task.CompletedTask; }
        public bool IsRuntimeRunning(BveRuntime runtime) => false;
    }
    private sealed class BrokenHistoryStore : ILibraryStore
    {
        public Task RecordLaunchAsync(Guid id, DateTime time, CancellationToken token = default) => throw new IOException("disk failure");
        public Task InitializeAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task<LibrarySnapshot> LoadAsync(CancellationToken token = default) => throw new NotSupportedException();
        public Task SaveAsync(LibrarySnapshot snapshot, CancellationToken token = default) => throw new NotSupportedException();
        public Task UpdateScenarioAsync(ScenarioEntry entry, CancellationToken token = default) => throw new NotSupportedException();
        public Task SaveInstallationAsync(InstallationRecord record, CancellationToken token = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<InstallationRecord>> GetInstallationsAsync(CancellationToken token = default) => throw new NotSupportedException();
    }
}
