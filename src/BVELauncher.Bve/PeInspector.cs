using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using BVELauncher.Core;

namespace BVELauncher.Bve;

public sealed record PeInfo(CpuArchitecture Architecture, bool Managed, string Version, string Product, string Company, IReadOnlyList<string> AssemblyReferences);
public static class PeInspector
{
    public static PeInfo Inspect(string path)
    {
        using var stream = File.OpenRead(path); using var reader = new PEReader(stream);
        var architecture = reader.PEHeaders.CoffHeader.Machine switch { Machine.I386 => CpuArchitecture.X86, Machine.Amd64 => CpuArchitecture.X64, Machine.Arm64 => CpuArchitecture.Arm64, _ => CpuArchitecture.Unknown };
        var references = new List<string>();
        if (reader.HasMetadata)
        {
            var metadata = reader.GetMetadataReader();
            foreach (var handle in metadata.AssemblyReferences) references.Add(metadata.GetString(metadata.GetAssemblyReference(handle).Name));
        }
        var version = FileVersionInfo.GetVersionInfo(path);
        return new(architecture, reader.HasMetadata, version.FileVersion ?? "", version.ProductName ?? "", version.CompanyName ?? "", references);
    }
}
