// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Application;
using BVELauncher.Bve;
using BVELauncher.Core;

namespace BVELauncher.IntegrationTests;

public sealed class ScanCacheTests
{
    [Fact]
    public async Task IncludedMapsUseMainMapBaseAndDoNotLeakDiagnosticsBetweenContexts()
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        var a = Path.Combine(env.Content, "A"); var b = Path.Combine(env.Content, "B"); var shared = Path.Combine(env.Content, "Shared");
        foreach (var folder in new[] { a, b, shared }) Directory.CreateDirectory(folder);
        foreach (var folder in new[] { a, b })
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "scenario.txt"), "BveTs Scenario 2.00\nRoute=main.txt\nVehicle=../vehicle.txt\n");
            await File.WriteAllTextAsync(Path.Combine(folder, "main.txt"), "BveTs Map 2.02:utf-8\ninclude '../Shared/include.txt';\n");
        }
        await File.WriteAllTextAsync(Path.Combine(shared, "include.txt"), "BveTs Map 2.02:utf-8\ninclude 'leaf.txt';\nStructure.Load('structures.csv');\n");
        await File.WriteAllTextAsync(Path.Combine(a, "leaf.txt"), "BveTs Map 2.02\ninclude '../Shared/include.txt';\n");
        await File.WriteAllTextAsync(Path.Combine(a, "structures.csv"), "BveTs Structure List 1.00\n");
        await File.WriteAllTextAsync(Path.Combine(b, "structures.csv"), "BveTs Structure List 1.00\n");
        await File.WriteAllTextAsync(Path.Combine(env.Content, "vehicle.txt"), "BveTs Vehicle 2.00\n");
        var scanner = new LibraryScanner(new BveAssetReader(), env.Store, env.Log);
        var snapshot = await scanner.ScanAsync([new(Guid.NewGuid(), env.Content)]);
        Assert.Equal(2, snapshot.Scenarios.Count);
        var valid = snapshot.Scenarios.Single(s => Path.GetDirectoryName(s.Path) == a);
        var missing = snapshot.Scenarios.Single(s => Path.GetDirectoryName(s.Path) == b);
        Assert.DoesNotContain(valid.Compatibility.Items, d => d.Code == "ReferenceMissing");
        var warning = Assert.Single(missing.Compatibility.Items, d => d.Code == "ReferenceMissing");
        Assert.Equal(Path.Combine(b, "leaf.txt"), warning.ResolvedPath);
        Assert.Equal(b, warning.MapBaseDirectory);
        Assert.Equal(snapshot.Assets.Count, snapshot.Assets.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var loaded = await env.Store.LoadAsync();
        Assert.Contains(loaded.References, r => r.SourcePath == Path.Combine(shared, "include.txt") && r.ResolvedPath == Path.Combine(a, "leaf.txt") && r.Exists);
        var restarted = await new LibraryScanner(new BveAssetReader(), env.Store, env.Log).ScanAsync([new(Guid.NewGuid(), env.Content)]);
        Assert.DoesNotContain(restarted.Scenarios.Single(s => s.Id == valid.Id).Compatibility.Items, d => d.Code == "ReferenceMissing");
    }
    [Fact]
    public async Task NewScannerReusesPersistentParsingAndDetectsChangesAndMissingReferences()
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        var scenario = Path.Combine(env.Content, "scenario.txt"); var map = Path.Combine(env.Content, "map.txt");
        await File.WriteAllTextAsync(scenario, "BveTs Scenario 2.00\nTitle=Before\nRoute=map.txt\nVehicle=missing.txt\n");
        await File.WriteAllTextAsync(map, "BveTs Map 2.02\n0;\n");
        await File.WriteAllTextAsync(Path.Combine(env.Content, "readme.txt"), "Unknown text");
        var reader = new CountingReader(); var roots = new[] { new ScenarioRoot(Guid.NewGuid(), env.Content) };
        var first = await new LibraryScanner(reader, env.Store, env.Log).ScanAsync(roots);
        Assert.Equal(2, reader.Reads); Assert.Equal(3, (await env.Store.LoadAssetCacheAsync()).Count);
        var restarted = new CountingReader();
        await new LibraryScanner(restarted, env.Store, env.Log).ScanAsync(roots); Assert.Equal(0, restarted.Reads);
        File.Delete(map);
        var deleted = await new LibraryScanner(restarted, env.Store, env.Log).ScanAsync(roots);
        Assert.False(Assert.Single(deleted.Scenarios).References.Single(r => r.Key == "Route").Exists);
        Assert.Equal(0, restarted.Reads); Assert.Equal(2, (await env.Store.LoadAssetCacheAsync()).Count);
        await File.WriteAllTextAsync(scenario, "BveTs Scenario 2.00\nTitle=Changed title\nRoute=map.txt\nVehicle=missing.txt\n");
        var changed = await new LibraryScanner(restarted, env.Store, env.Log).ScanAsync(roots);
        Assert.Equal(1, restarted.Reads); Assert.Equal("Changed title", Assert.Single(changed.Scenarios).Title);
        Assert.Equal(first.Scenarios[0].Id, changed.Scenarios[0].Id);
        await File.WriteAllTextAsync(map, "BveTs Map 2.02\n0;\n");
        var restored = await new LibraryScanner(restarted, env.Store, env.Log).ScanAsync(roots);
        Assert.True(restored.Scenarios[0].References.Single(r => r.Key == "Route").Exists);
    }
    private sealed class CountingReader : IAssetReader
    {
        private readonly BveAssetReader inner = new();
        public int Reads { get; private set; }
        public Task<AssetReadResult> ReadAsync(string path, CancellationToken cancellationToken, AssetKind expectedKind = AssetKind.Unknown)
        { Reads++; return inner.ReadAsync(path, cancellationToken, expectedKind); }
        public Task<string> MaterializeAsync(string scenarioPath, VehicleOverride vehicle, string fallbackDirectory, CancellationToken cancellationToken) => inner.MaterializeAsync(scenarioPath, vehicle, fallbackDirectory, cancellationToken);
    }
}
