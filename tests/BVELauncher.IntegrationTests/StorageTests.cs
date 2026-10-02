using System.Text;
using BVELauncher.Application;
using BVELauncher.Bve;
using BVELauncher.Core;
using BVELauncher.Infrastructure;
using Microsoft.Data.Sqlite;
namespace BVELauncher.IntegrationTests;
public sealed class StorageTests
{
    [Fact]
    public async Task DatabasePreservesFavoritesAndIdentityAfterMove()
    {
        using var env = new TestEnvironment(); await env.Store.InitializeAsync();
        var root = env.Content; var scenario = Path.Combine(root,"日本語 scenario.txt");
        await File.WriteAllTextAsync(scenario,"BveTs Scenario 2.00\nTitle = 試験\nRoute = map.txt\nVehicle = vehicle.txt\n");
        await File.WriteAllTextAsync(Path.Combine(root,"map.txt"),"BveTs Map 2.02\n0;");
        await File.WriteAllTextAsync(Path.Combine(root,"vehicle.txt"),"BveTs Vehicle 2.00\nAts32 = missing.dll\n");
        var scanner = new LibraryScanner(new BveAssetReader(),env.Store,env.Log);
        var first = await scanner.ScanAsync([new(Guid.NewGuid(),root)]);
        Assert.Single(first.Scenarios); Assert.Single(first.Vehicles);
        var favorite = first.Scenarios[0] with {Favorite=true,Alias="独自名"};
        await env.Store.UpdateScenarioAsync(favorite);
        File.Move(scenario,Path.Combine(root,"移動.txt"));
        var moved = await scanner.ScanAsync([new(Guid.NewGuid(),root)]);
        Assert.Equal(favorite.Id,moved.Scenarios[0].Id);
        Assert.True(moved.Scenarios[0].Favorite); Assert.Equal("独自名",moved.Scenarios[0].DisplayTitle);
        Assert.Contains(moved.Diagnostics,d=>d.Code=="ReferenceMissing");
        var same = await scanner.ScanAsync([new(Guid.NewGuid(),root),new(Guid.NewGuid(),root)]);
        Assert.Single(same.Scenarios);
        File.Delete(moved.Scenarios[0].Path);
        Assert.Empty((await scanner.ScanAsync([new(Guid.NewGuid(),root)])).Scenarios);
    }
    [Fact]
    public async Task CorruptDatabaseIsPreservedAndRecovered()
    {
        using var env = new TestEnvironment();
        await File.WriteAllTextAsync(env.Paths.Database,"corrupt!");
        await env.Store.InitializeAsync(); Assert.Empty((await env.Store.LoadAsync()).Scenarios);
        Assert.Single(Directory.GetFiles(env.Paths.Data,"*.corrupt-*"));
    }
    [Fact]
    public async Task PreferencesModifyKnownNodePreserveUnknownAndMakeBackup()
    {
        using var env = new TestEnvironment();
        string path=Path.Combine(env.Content,"preferences.xml");
        await File.WriteAllTextAsync(path,"<?xml version=\"1.0\"?>\r\n<Preferences>\r\n  <Windowed>true</Windowed>\r\n  <HeadMotion>25</HeadMotion>\r\n  <Future x=\"keep\">opaque</Future>\r\n</Preferences>");
        var adapter=new Bve6SettingsAdapter(env.Backups); var before=await adapter.ReadAsync(path);
        await adapter.SaveAsync(before,new Dictionary<string,string>{{"HeadMotion","40"}});
        Assert.Equal("40",(await adapter.ReadAsync(path)).Values["HeadMotion"]);
        var text=await File.ReadAllTextAsync(path); Assert.Contains("\r\n  <Future x=\"keep\">opaque</Future>\r\n",text);
        var backup=Assert.Single(await env.Backups.ListAsync()); await env.Backups.RestoreAsync(backup);
        Assert.Equal("25",(await adapter.ReadAsync(path)).Values["HeadMotion"]);
        await File.WriteAllTextAsync(path,"<broken>");
        await Assert.ThrowsAsync<System.Xml.XmlException>(()=>adapter.ReadAsync(path));
    }
    [Fact]
    public async Task PreferencesRejectConcurrentEditAndOutOfRange()
    {
        using var env=new TestEnvironment(); string path=Path.Combine(env.Content,"preferences.xml");
        await File.WriteAllTextAsync(path,"<Preferences><HeadMotion>25</HeadMotion></Preferences>");
        var adapter=new Bve5SettingsAdapter(env.Backups);var snapshot=await adapter.ReadAsync(path);
        await Assert.ThrowsAsync<InvalidDataException>(()=>adapter.SaveAsync(snapshot,new Dictionary<string,string>{{"HeadMotion","101"}}));
        await File.AppendAllTextAsync(path," ");
        await Assert.ThrowsAsync<IOException>(()=>adapter.SaveAsync(snapshot,new Dictionary<string,string>{{"HeadMotion","50"}}));
    }
    [Fact]
    public async Task VehicleMaterializationPreservesOriginalAndRelativeReferences()
    {
        using var env=new TestEnvironment(); string scenario=Path.Combine(env.Content,"space 日本語.txt");
        var bytes=Encoding.UTF8.GetBytes("BveTs Scenario 2.00\nVehicle = old.txt #keep\nRoute = map.txt\nUnknown = opaque\n");
        await File.WriteAllBytesAsync(scenario,bytes);
        var reader=new BveAssetReader(); string generated=await reader.MaterializeAsync(scenario,new(Path.Combine(env.Content,"new.txt"),"新車"),env.Paths.Generated,default);
        Assert.Equal(bytes,await File.ReadAllBytesAsync(scenario));
        var document=BveDocument.Parse(await File.ReadAllBytesAsync(generated));
        Assert.Equal("new.txt",document.Get("Vehicle")); Assert.Equal("map.txt",document.Get("Route"));Assert.Equal("opaque",document.Get("Unknown"));
        var runtime=new BveRuntime(Guid.NewGuid(),RuntimeKind.Bve6,"6",@"C:\BVE space\BveTs.exe",CpuArchitecture.X64,@"C:\BVE space",true);
        var plan=new LaunchPlan(Guid.NewGuid(),runtime,generated,runtime.InstallDirectory,LaunchBehavior.Hide,generated);
        var info=ProcessRunner.CreateStartInfo(plan); Assert.Equal(runtime.ExecutablePath,info.FileName);Assert.Equal(runtime.InstallDirectory,info.WorkingDirectory);
        Assert.Single(info.ArgumentList);Assert.Equal(generated,info.ArgumentList[0]);Assert.False(info.UseShellExecute);
    }
}
internal sealed class TestEnvironment : IDisposable
{
    public string Root {get;}=Path.Combine(Path.GetTempPath(),"BVELauncher.Tests-"+Guid.NewGuid().ToString("N"));
    public string Content {get;}
    public AppPaths Paths {get;}
    public LocalLog Log {get;}
    public SqliteLibraryStore Store {get;}
    public BackupService Backups {get;}
    public TestEnvironment()
    {
        Paths=new(Path.Combine(Root,"App"));Paths.EnsureDirectories();Content=Path.Combine(Root,"Content");Directory.CreateDirectory(Content);
        Log=new(Paths);Store=new(Paths,Log);Backups=new(Paths);
    }
    public void Dispose(){SqliteConnection.ClearAllPools();if(Directory.Exists(Root))Directory.Delete(Root,true);}
}
