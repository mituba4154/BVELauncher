// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Xml;
using BVELauncher.Application;
using BVELauncher.Core;
using Microsoft.Win32;

namespace BVELauncher.Bve;

[SupportedOSPlatform("windows")]
public sealed class RuntimeDetector : IRuntimeDetector
{
    public Task<IReadOnlyList<BveRuntime>> DetectAsync(IEnumerable<BveRuntime> registered, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<BveRuntime>>(() =>
    {
        var paths = registered.Select(r => r.ExecutablePath).Concat(new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "mackoy", "BveTs5", "BveTs.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "mackoy", "BveTs6", "BveTs.exe")
        }).Concat(RegistryPaths()).Distinct(StringComparer.OrdinalIgnoreCase);
        var result = new List<BveRuntime>();
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path)) continue;
            try
            {
                var runtime = Inspect(path); var existing = registered.FirstOrDefault(r => r.ExecutablePath.Equals(path, StringComparison.OrdinalIgnoreCase));
                result.Add(existing == null ? runtime : runtime with { Id = existing.Id, PreferencesPath = existing.PreferencesPath ?? runtime.PreferencesPath });
            }
            catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException)
            { result.Add(new(Guid.NewGuid(), RuntimeKind.Unknown, "", path, CpuArchitecture.Unknown, Path.GetDirectoryName(path)!, false)); }
        }
        return result;
    }, cancellationToken);
    public BveRuntime Inspect(string executablePath)
    {
        var full = Path.GetFullPath(executablePath); var info = PeInspector.Inspect(full);
        var version = FileVersionInfo.GetVersionInfo(full);
        var kind = version.FileMajorPart switch { 5 => RuntimeKind.Bve5, 6 => RuntimeKind.Bve6, _ => RuntimeKind.Unknown };
        bool valid = kind != RuntimeKind.Unknown && (info.Product.Contains("Bve", StringComparison.OrdinalIgnoreCase) || info.Company.Contains("mackoy", StringComparison.OrdinalIgnoreCase));
        var preferences = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BveTs", "Settings", kind == RuntimeKind.Bve6 ? "BveTs6.Preferences.xml" : "Preferences.xml");
        return new(Guid.NewGuid(), kind, info.Version, full, info.Architecture, Path.GetDirectoryName(full)!, valid, preferences);
    }
    private static IEnumerable<string> RegistryPaths()
    {
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall == null) continue;
            foreach (var name in uninstall.GetSubKeyNames())
            {
                using var key = uninstall.OpenSubKey(name);
                if (key?.GetValue("DisplayName") is not string display || !display.Contains("Bve", StringComparison.OrdinalIgnoreCase)) continue;
                if (key.GetValue("InstallLocation") is string location && location.Length > 0) yield return Path.Combine(location, "BveTs.exe");
                if (key.GetValue("DisplayIcon") is string icon)
                {
                    var path = icon.Split(',')[0].Trim('"'); if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) yield return path;
                }
            }
        }
    }
    public static IEnumerable<string> DiscoverScenarioRoots(IEnumerable<BveRuntime> runtimes)
    {
        var defaultRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BveTs", "Scenarios");
        if (Directory.Exists(defaultRoot)) yield return defaultRoot;
        foreach (var runtime in runtimes)
        {
            if (!File.Exists(runtime.PreferencesPath)) continue;
            string? path = ReadScenarioDirectory(runtime.PreferencesPath!);
            if (path != null && Directory.Exists(path)) yield return path;
        }
    }
    private static string? ReadScenarioDirectory(string path)
    {
        try
        {
            var document = new XmlDocument { XmlResolver = null }; document.Load(path);
            return document.SelectSingleNode("/Preferences/ScenarioForm/Address/string")?.InnerText;
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException) { System.Diagnostics.Debug.WriteLine(ex); return null; }
    }
}
