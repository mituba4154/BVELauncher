// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Application;
using BVELauncher.Bve;
using BVELauncher.Core;

namespace BVELauncher.IntegrationTests;

public sealed class AssetReferenceTests
{
    [Fact]
    public async Task MotorNoiseDefinitionReferencesTablesButTheirNumbersAreNotPaths()
    {
        using var env = new TestEnvironment();
        var definition = Path.Combine(env.Content, "motor.txt");
        await File.WriteAllTextAsync(definition, "[Power]\nVolume=PowerVol.csv\nFrequency=PowerFreq.csv\n[Brake]\nVolume=BrakeVol.csv\nFrequency=BrakeFreq.csv\n");
        var reader = new BveAssetReader(); var data = await reader.ReadAsync(definition, default, AssetKind.MotorNoise);
        Assert.Equal(4, data.References.Count);
        Assert.All(data.References, r => Assert.Equal(AssetKind.MotorNoise, r.TargetKind));
        foreach (var reference in data.References)
        {
            await File.WriteAllTextAsync(reference.ResolvedPath, "BveTs Motor Noise Table 1.00\n0,0.5,0.6\n10,1,0.9\n");
            var table = await reader.ReadAsync(reference.ResolvedPath, default, reference.TargetKind);
            Assert.Empty(table.References); Assert.Empty(table.Diagnostics);
        }
    }
    [Fact]
    public async Task SoundIniRecognizesEachSectionAndPreservesUnknownFields()
    {
        using var env = new TestEnvironment(); var path = Path.Combine(env.Content, "sound.txt");
        await File.WriteAllTextAsync(path, "BveTs Vehicle Sound 3.01\n[Run]\n0=run.wav ; comment\n[Door]\nOpenLeft = door.wav\n[Ats]\n0=bell.wav\n[Future]\nOpaque = 0.5\n");
        await File.WriteAllBytesAsync(Path.Combine(env.Content, "run.wav"), []);
        var data = await new BveAssetReader().ReadAsync(path, default);
        Assert.Equal(3, data.References.Count);
        Assert.True(data.References.Single(r => r.Key == "Run.0").Exists);
        Assert.False(data.References.Single(r => r.Key == "Ats.0").Exists);
        Assert.Equal("0.5", data.Metadata["Opaque"]);
    }
    [Fact]
    public async Task OlderParserCacheIsReplacedEvenWhenSourceHasNotChanged()
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        var path = Path.Combine(env.Content, "table.csv");
        await File.WriteAllTextAsync(path, "BveTs Motor Noise Table 1.00\n0,0.5,0.6\n");
        var info = new FileInfo(path);
        var stale = new AssetReadResult(AssetKind.MotorNoise, new Dictionary<string,string>(), [new(path, "0", "0.5", Path.Combine(env.Content, "0.5"), AssetKind.Audio, false)], [], "old");
        await env.Store.SaveAssetCacheAsync([new(path, info.LastWriteTimeUtc, info.Length, stale)]);
        var snapshot = await new LibraryScanner(new BveAssetReader(), env.Store, env.Log).ScanAsync([new(Guid.NewGuid(), env.Content)]);
        Assert.Empty(snapshot.References); Assert.Empty(snapshot.Diagnostics);
        Assert.Equal(LibraryScanner.ParserVersion, Assert.Single(await env.Store.LoadAssetCacheAsync()).ParserVersion);
    }
}
