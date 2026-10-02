// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using BVELauncher.Core;

namespace BVELauncher.Integrations;

public sealed class ExtensionDetector
{
    public Task<IReadOnlyList<ExtensionStatus>> DetectAsync(IEnumerable<BveRuntime> runtimes, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<ExtensionStatus>>(() =>
    {
        var result = new List<ExtensionStatus>();
        foreach (var runtime in runtimes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var directory in new[] { runtime.InstallDirectory, Path.Combine(runtime.InstallDirectory, "Input Devices"), Path.Combine(runtime.InstallDirectory, "Extensions"), Path.Combine(runtime.InstallDirectory, "BveEX") }.Where(Directory.Exists))
                foreach (var file in Directory.EnumerateFiles(directory, "BveEx*.dll", SearchOption.TopDirectoryOnly))
                {
                    var info = FileVersionInfo.GetVersionInfo(file);
                    result.Add(new("BveEX", file, info.FileVersion ?? "", runtime.Kind, true));
                }
        }
        return result.DistinctBy(s => s.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }, cancellationToken);
    public async Task RunLocalInstallerAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("InstallerNotExecutable");
        var info = FileVersionInfo.GetVersionInfo(path);
        if (!(info.ProductName + info.FileDescription).Contains("BveEX", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("InstallerNotRecognized");
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path)! }) ?? throw new IOException("Installer launch failed.");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }
}
