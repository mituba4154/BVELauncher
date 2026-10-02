using System.Diagnostics;

namespace BVELauncher.App.Services;

public static class ExplorerInterop
{
    public static void OpenFolder(string path)
    { var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = true }; info.ArgumentList.Add(Path.GetFullPath(path)); Process.Start(info)?.Dispose(); }
    public static void OpenTextFile(string path)
    { var info = new ProcessStartInfo("notepad.exe") { UseShellExecute = false }; info.ArgumentList.Add(Path.GetFullPath(path)); Process.Start(info)?.Dispose(); }
}
