// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Application;

namespace BVELauncher.Infrastructure;

public sealed class LibraryWatcher(ILocalLog log) : IDisposable
{
    private readonly List<FileSystemWatcher> watchers = [];
    private System.Threading.Timer? timer;
    private Action? onChanged;
    private bool disposed;
    public void Watch(IEnumerable<string> roots, Action changed)
    {
        foreach (var watcher in watchers) watcher.Dispose(); watchers.Clear();
        onChanged = changed;
        timer ??= new System.Threading.Timer(_ => { if (!disposed) onChanged?.Invoke(); }, null, Timeout.Infinite, Timeout.Infinite);
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))
        {
            var watcher = new FileSystemWatcher(root) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size, InternalBufferSize = 32 * 1024 };
            watcher.Changed += Changed; watcher.Created += Changed; watcher.Deleted += Changed; watcher.Renamed += Changed;
            watcher.Error += (_, e) => { log.Write("Warning", "Watcher overflow; scheduling complete rescan.", e.GetException()); Schedule(); };
            watcher.EnableRaisingEvents = true; watchers.Add(watcher);
        }
    }
    private void Changed(object sender, FileSystemEventArgs e)
    {
        if (!Path.GetFileName(e.FullPath).StartsWith(".BVELauncher-", StringComparison.OrdinalIgnoreCase)) Schedule();
    }
    private void Schedule()
    {
        if (disposed) return;
        timer?.Change(600, Timeout.Infinite);
    }
    public void Dispose()
    {
        disposed = true; timer?.Dispose(); foreach (var watcher in watchers) watcher.Dispose();
    }
}
