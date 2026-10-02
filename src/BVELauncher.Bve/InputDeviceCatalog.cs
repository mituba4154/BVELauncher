using System.Diagnostics;

namespace BVELauncher.Bve;

public sealed record InputDeviceFile(string Key, string Name, string Version, string Path);
public static class InputDeviceCatalog
{
    public static IReadOnlyList<InputDeviceFile> Read(string installDirectory)
    {
        string directory = Path.Combine(installDirectory, "Input Devices");
        if (!Directory.Exists(directory)) return [];
        var result = new List<InputDeviceFile>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
            var metadata = FileVersionInfo.GetVersionInfo(path);
            var key = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            result.Add(new(key, string.IsNullOrWhiteSpace(metadata.FileDescription) ? Path.GetFileNameWithoutExtension(path) : metadata.FileDescription, metadata.FileVersion ?? "", path));
        }
        return result.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}
