// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Application;

namespace BVELauncher.Infrastructure;

public sealed class LocalLog : ILocalLog
{
    private readonly AppPaths paths;
    private readonly object gate = new();
    public LocalLog(AppPaths paths)
    {
        this.paths = paths;
        paths.EnsureDirectories();
    }
    public void Write(string level, string message, Exception? error = null)
    {
        lock (gate)
        {
            try
            {
                string path = Path.Combine(paths.Logs, DateTime.UtcNow.ToString("yyyy-MM-dd") + ".log");
                if (File.Exists(path) && new FileInfo(path).Length > 4 * 1024 * 1024) path = Path.Combine(paths.Logs, DateTime.UtcNow.ToString("yyyy-MM-dd-HHmm") + ".log");
                File.AppendAllText(path, $"{DateTime.UtcNow:O} {level} {message} {error}\n");
            }
            catch (IOException ex) { System.Diagnostics.Debug.WriteLine(ex); }
            catch (UnauthorizedAccessException ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }
    }
    public void ApplyRetention(int days)
    {
        foreach (var path in Directory.EnumerateFiles(paths.Logs, "*.log"))
            if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-Math.Max(1, days))) File.Delete(path);
    }
}
