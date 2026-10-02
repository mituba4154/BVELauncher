// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Bve;

public sealed class CompatibilityEngine
{
    public Task<CompatibilityReport> DiagnoseAsync(ScenarioEntry scenario, LibrarySnapshot library, BveRuntime? runtime, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var items = LibraryScanner.ReachableDiagnostics(scenario.Path, library.References, library.Diagnostics).ToList();
        var vehicles = library.Vehicles.Where(v => scenario.References.Any(r => r.TargetKind == AssetKind.Vehicle && r.ResolvedPath.Equals(v.Path, StringComparison.OrdinalIgnoreCase))).ToArray();
        RuntimeKind recommended = RuntimeKind.Unknown;
        var candidates = new List<RuntimeKind>();
        foreach (var vehicle in vehicles)
        {
            var candidate = RuntimeKind.Unknown;
            if (vehicle.Ats32 != null && vehicle.Ats64 == null) { candidate = RuntimeKind.Bve5; items.Add(new(Severity.Info, "Ats32Only", vehicle.Path)); }
            if (vehicle.Ats64 != null && vehicle.Ats32 == null) { candidate = RuntimeKind.Bve6; items.Add(new(Severity.Info, "Ats64Only", vehicle.Path)); }
            candidates.Add(candidate);
            if (!vehicle.References.Any(r => r.Key.Equals("Panel", StringComparison.OrdinalIgnoreCase))) items.Add(new(Severity.Warning, "PanelMissing", vehicle.Path));
            if (!vehicle.References.Any(r => r.Key.Equals("Sound", StringComparison.OrdinalIgnoreCase))) items.Add(new(Severity.Warning, "SoundMissing", vehicle.Path));
            foreach (var reference in vehicle.References.Where(r => r.TargetKind == AssetKind.Dll && r.Exists))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var pe = PeInspector.Inspect(reference.ResolvedPath);
                    var expected = reference.Key.Equals("Ats64", StringComparison.OrdinalIgnoreCase) ? CpuArchitecture.X64 : CpuArchitecture.X86;
                    if (pe.Architecture != expected) items.Add(new(Severity.Warning, "DllArchitecture", reference.SourcePath, reference.RawPath, reference.ResolvedPath, pe.Architecture.ToString()));
                }
                catch (Exception ex) when (ex is IOException or BadImageFormatException) { items.Add(new(Severity.Unknown, "DllUnknown", reference.SourcePath, reference.RawPath, reference.ResolvedPath, ex.Message)); }
            }
        }
        // 重み付き候補が異なる環境を要求する場合は、確定した推奨にしない。
        int referencedVehicles = scenario.References.Where(r => r.TargetKind == AssetKind.Vehicle).Select(r => r.ResolvedPath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (candidates.Count > 0 && candidates.Count == referencedVehicles && candidates.Distinct().Count() == 1) recommended = candidates[0];
        if (runtime != null && recommended != RuntimeKind.Unknown && recommended != runtime.Kind) items.Add(new(Severity.Warning, "RuntimeMismatch", scenario.Path));
        return new CompatibilityReport(items, recommended);
    }, cancellationToken);
}
