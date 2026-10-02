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
        using var env = new TestEnvironment(); var dll = Path.Combine(env.Content, "BveEx.test.dll"); await File.WriteAllTextAsync(dll, "self authored invalid PE");
        var runtime = new BveRuntime(Guid.NewGuid(), RuntimeKind.Bve6, "6", "", CpuArchitecture.X64, env.Content, true);
        var detector = new ExtensionDetector(); Assert.Equal(dll, Assert.Single(await detector.DetectAsync([runtime])).Path);
        await Assert.ThrowsAsync<InvalidDataException>(() => detector.RunLocalInstallerAsync(dll));
    }
}
