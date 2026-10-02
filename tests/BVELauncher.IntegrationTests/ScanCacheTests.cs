using BVELauncher.Application;
using BVELauncher.Bve;
using BVELauncher.Core;

namespace BVELauncher.IntegrationTests;

public sealed class ScanCacheTests
{
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
