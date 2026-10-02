// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Compression;
using BVELauncher.Bve;
using BVELauncher.Core;
using BVELauncher.Infrastructure;

namespace BVELauncher.IntegrationTests;

public sealed class ExtensionPackageTests
{
    [Fact]
    public async Task PackageInstallsOnlySelectedDllAndProtectsExistingFileWithRestorableBackup()
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        var dll = await File.ReadAllBytesAsync(typeof(BveAssetReader).Assembly.Location);
        var source = Package(env, ("TsHud-1.00/TsHud.dll", dll), ("TsHud-1.00/unrelated.exe", dll), ("readme.txt", [1,2]));
        var target = Path.Combine(env.Content, "TsHud.dll"); await File.WriteAllBytesAsync(target, [4,5,6]);
        var service = Service(env); var planner = new ExtensionPackageImporter(service);
        var plan = await planner.AnalyzeAsync(source, env.Content, "TsHud.dll");
        var operation = Assert.Single(plan.Operations); Assert.Equal(target, operation.DestinationPath); Assert.True(operation.Exists);
        var installed = await service.CommitAsync(plan, ConflictBehavior.Overwrite);
        Assert.Equal(dll, await File.ReadAllBytesAsync(target)); Assert.Single(Directory.GetFiles(env.Content));
        Assert.True(File.Exists(source)); Assert.Single(installed.Files);
        var removal = await service.UninstallAsync(installed, LibrarySnapshot.Empty);
        Assert.Contains(target, removal.Preserved); Assert.Equal(dll, await File.ReadAllBytesAsync(target));
        await env.Backups.RestoreAsync(Assert.Single(await env.Backups.ListAsync()));
        Assert.Equal(new byte[] {4,5,6}, await File.ReadAllBytesAsync(target));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidOrAmbiguousDllIsRejectedWithoutWritingDestination(bool duplicate)
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        var source = duplicate ? Package(env, ("a/TsHud.dll", [1]), ("b/TsHud.dll", [2])) : Package(env, ("TsHud.dll", [1,2,3]));
        await Assert.ThrowsAnyAsync<Exception>(() => new ExtensionPackageImporter(Service(env)).AnalyzeAsync(source, env.Content, "TsHud.dll"));
        Assert.Empty(Directory.GetFiles(env.Content)); Assert.Empty(Directory.GetDirectories(env.Paths.Staging));
    }
    [Fact]
    public async Task DllPathCannotEscapeDestination()
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => new ExtensionPackageImporter(Service(env)).AnalyzeAsync("unused", env.Content, "../TsHud.dll"));
    }
    private static ImportService Service(TestEnvironment env) => new(env.Paths, new ArchiveStager(new()), new BveAssetReader(), env.Backups, env.Store, env.Log);
    private static string Package(TestEnvironment env, params (string Name, byte[] Bytes)[] files)
    {
        var path = Path.Combine(env.Root, Guid.NewGuid() + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var file in files) { using var output = zip.CreateEntry(file.Name).Open(); output.Write(file.Bytes); }
        return path;
    }
}
