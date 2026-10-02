using System.IO.Compression;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.SevenZip;
using BVELauncher.Application;
using BVELauncher.Bve;
using BVELauncher.Core;
using BVELauncher.Infrastructure;

namespace BVELauncher.IntegrationTests;

public sealed class ImportTests
{
    [Fact]
    public async Task SevenZipIsStagedAndInstalled()
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        var source = Path.Combine(env.Root, "addon.7z");
        using (var stream = File.Create(source))
        using (var writer = WriterFactory.OpenWriter(stream, ArchiveType.SevenZip, new SevenZipWriterOptions()))
        using (var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("BveTs Vehicle 2.00\nPanel = panel.txt\n")))
            writer.Write("Scenarios/Vehicle/vehicle.txt", content, DateTime.UtcNow);
        var service = Service(env); var plan = await service.AnalyzeAsync(new(source), env.Content);
        Assert.Equal(1, plan.Vehicles);
        await service.CommitAsync(plan, ConflictBehavior.Skip);
        Assert.True(File.Exists(Path.Combine(env.Content, "Vehicle", "vehicle.txt")));
    }
    [Theory]
    [InlineData("Scenarios/route/scenario.txt", "route/scenario.txt")]
    [InlineData("package/Scenarios/route/scenario.txt", "route/scenario.txt")]
    [InlineData("vehicle/data.txt", "vehicle/data.txt")]
    public async Task ZipInferencePreservesLayoutAndInstalls(string entry, string relative)
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        string zip = CreateZip(env, (entry, "BveTs Scenario 2.00\nTitle = Import\n"));
        var service = Service(env); var plan = await service.AnalyzeAsync(new(zip), env.Content);
        Assert.Equal(relative.Replace('/',Path.DirectorySeparatorChar), Assert.Single(plan.Operations).RelativePath);
        var installation = await service.CommitAsync(plan, ConflictBehavior.Skip);
        Assert.Single(installation.Files); Assert.True(File.Exists(plan.Operations[0].DestinationPath)); Assert.True(File.Exists(zip));
        Assert.Equal(1,(await service.UninstallAsync(installation,LibrarySnapshot.Empty)).Removed);
    }
    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("..\\escape.txt")]
    [InlineData("/escape.txt")]
    [InlineData("C:/escape.txt")]
    [InlineData("\\\\server\\share\\escape.txt")]
    [InlineData("CON.txt")]
    [InlineData("a/NUL")]
    [InlineData("a./file.txt")]
    [InlineData("a/file.txt:stream")]
    [InlineData("a/<bad>.txt")]
    public async Task HostileArchiveIsRejectedBeforeDestinationWrite(string entry)
    {
        using var env = new TestEnvironment();await env.Store.InitializeAsync();
        var service=Service(env);string zip=CreateZip(env,(entry,"bad"));
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.AnalyzeAsync(new(zip),env.Content));
        Assert.Empty(Directory.GetFiles(env.Content));Assert.Empty(Directory.GetDirectories(env.Paths.Staging));
    }
    [Fact]
    public async Task CaseInsensitiveDuplicateRejected()
    {
        using var env=new TestEnvironment();await env.Store.InitializeAsync();
        string zip=CreateZip(env,("Data.txt","one"),("data.TXT","two"));
        await Assert.ThrowsAsync<InvalidDataException>(()=>Service(env).AnalyzeAsync(new(zip),env.Content));
    }
    [Fact]
    public async Task ExistingFileRequiresDecisionAndOverwriteHasBackup()
    {
        using var env=new TestEnvironment();await env.Store.InitializeAsync();
        string path=Path.Combine(env.Content,"data.txt");await File.WriteAllTextAsync(path,"original");
        var service=Service(env);var plan=await service.AnalyzeAsync(new(CreateZip(env,("data.txt","new"))),env.Content);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CommitAsync(plan,ConflictBehavior.Ask));
        Assert.Equal("original",await File.ReadAllTextAsync(path));
        var installation=await service.CommitAsync(plan,ConflictBehavior.Overwrite);
        Assert.Equal("original",await File.ReadAllTextAsync(installation.Files[0].BackupPath!));
        var uninstall=await service.UninstallAsync(installation,LibrarySnapshot.Empty);
        Assert.Equal(0,uninstall.Removed);Assert.Single(uninstall.Preserved);
    }
    [Fact]
    public async Task PartialFailureRollsBackAddedFiles()
    {
        using var env=new TestEnvironment();await env.Store.InitializeAsync();
        var service=Service(env);var plan=await service.AnalyzeAsync(new(CreateZip(env,("first.txt","one"),("second.txt","two"))),env.Content);
        using var cancellation=new CancellationTokenSource();
        var progress=new SynchronousProgress(_=>cancellation.Cancel());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.CommitAsync(plan,ConflictBehavior.Skip,progress,cancellation.Token));
        Assert.Empty(Directory.GetFiles(env.Content));
    }
    [Fact]
    public async Task ModifiedAndReferencedManagedFilesArePreserved()
    {
        using var env=new TestEnvironment();await env.Store.InitializeAsync();var service=Service(env);
        var plan=await service.AnalyzeAsync(new(CreateZip(env,("changed.txt","one"),("shared.txt","two"))),env.Content);
        var installed=await service.CommitAsync(plan,ConflictBehavior.Skip);
        await File.AppendAllTextAsync(Path.Combine(env.Content,"changed.txt"),"user");
        var graph=new LibrarySnapshot([],[],[],[new("outside.txt","Sound","shared.txt",Path.Combine(env.Content,"shared.txt"),AssetKind.Audio,true)],[]);
        var result=await service.UninstallAsync(installed,graph);Assert.Equal(0,result.Removed);Assert.Equal(2,result.Preserved.Count);
    }
    [Fact]
    public async Task FileCountAndSizeLimitsAreEnforced()
    {
        using var env=new TestEnvironment();await env.Store.InitializeAsync();
        var service=Service(env,new(MaxFiles:1,MaxBytes:32,MaxFileBytes:32));
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.AnalyzeAsync(new(CreateZip(env,("one","a"),("two","b"))),env.Content));
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.AnalyzeAsync(new(CreateZip(env,("one",new string('x',64)))),env.Content));
    }
    [Fact]
    public async Task FolderAndSingleScenarioCanBeStaged()
    {
        using var env=new TestEnvironment();await env.Store.InitializeAsync();var service=Service(env);
        string source=Path.Combine(env.Root,"Folder");Directory.CreateDirectory(source);
        string vehicle=Path.Combine(source,"vehicle.dat");await File.WriteAllTextAsync(vehicle,"BveTs Vehicle 2.00\nAts64 = plugin.dll\n");
        var folder=await service.AnalyzeAsync(new(source),env.Content);Assert.Equal(1,folder.Vehicles);await service.DiscardAsync(folder);
        var single=await service.AnalyzeAsync(new(vehicle),env.Content);Assert.Single(single.Operations);await service.DiscardAsync(single);
    }
    [Fact]
    public async Task PreviewRejectsChangedDestination()
    {
        using var env=new TestEnvironment();await env.Store.InitializeAsync();var service=Service(env);
        string path=Path.Combine(env.Content,"data.txt");await File.WriteAllTextAsync(path,"one");
        var plan=await service.AnalyzeAsync(new(CreateZip(env,("data.txt","new"))),env.Content);
        await File.WriteAllTextAsync(path,"changed");
        await Assert.ThrowsAsync<IOException>(()=>service.CommitAsync(plan,ConflictBehavior.Overwrite));Assert.Equal("changed",await File.ReadAllTextAsync(path));
    }
    private static ImportService Service(TestEnvironment env,ImportLimits? limits=null)
        =>new(env.Paths,new ArchiveStager(limits??new()),new BveAssetReader(),env.Backups,env.Store,env.Log);
    private static string CreateZip(TestEnvironment env,params (string Name,string Content)[] entries)
    {
        string path=Path.Combine(env.Root,Guid.NewGuid()+".zip");using var archive=ZipFile.Open(path,ZipArchiveMode.Create);
        foreach(var entry in entries){using var writer=new StreamWriter(archive.CreateEntry(entry.Name).Open());writer.Write(entry.Content);}return path;
    }
    private sealed class SynchronousProgress(Action<ScanProgress> report):IProgress<ScanProgress>{public void Report(ScanProgress value)=>report(value);}
}
