namespace BVELauncher.Infrastructure;

public sealed class AppPaths
{
    public string Root { get; }
    public string Data => Path.Combine(Root, "Data");
    public string Database => Path.Combine(Data, "bvelauncher.db");
    public string Settings => Path.Combine(Root, "Settings", "appsettings.json");
    public string Cache => Path.Combine(Root, "Cache", "Thumbnails");
    public string Generated => Path.Combine(Root, "Generated");
    public string Backups => Path.Combine(Root, "Backups");
    public string Staging => Path.Combine(Root, "Staging");
    public string Logs => Path.Combine(Root, "Logs");
    public AppPaths(string? root = null) => Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BVELauncher");
    public void EnsureDirectories()
    {
        foreach (var directory in new[] { Data, Path.GetDirectoryName(Settings)!, Cache, Generated, Backups, Staging, Logs }) Directory.CreateDirectory(directory);
    }
}
