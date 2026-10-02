using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Bve;

public sealed class CompatibilityEngine
{
    public Task<CompatibilityReport> DiagnoseAsync(ScenarioEntry scenario, LibrarySnapshot library, BveRuntime? runtime, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var items = LibraryScanner.ReachableDiagnostics(scenario.Path, library.References, library.Diagnostics).ToList();
        var vehicle = library.Vehicles.FirstOrDefault(v => scenario.References.Any(r => r.TargetKind == AssetKind.Vehicle && r.ResolvedPath.Equals(v.Path, StringComparison.OrdinalIgnoreCase)));
        RuntimeKind recommended = RuntimeKind.Unknown;
        if (vehicle != null)
        {
            if (vehicle.Ats32 != null && vehicle.Ats64 == null) { recommended = RuntimeKind.Bve5; items.Add(new(Severity.Info, "Ats32Only", vehicle.Path)); }
            if (vehicle.Ats64 != null && vehicle.Ats32 == null) { recommended = RuntimeKind.Bve6; items.Add(new(Severity.Info, "Ats64Only", vehicle.Path)); }
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
        if (runtime != null && recommended != RuntimeKind.Unknown && recommended != runtime.Kind) items.Add(new(Severity.Warning, "RuntimeMismatch", scenario.Path));
        return new CompatibilityReport(items, recommended);
    }, cancellationToken);
}
