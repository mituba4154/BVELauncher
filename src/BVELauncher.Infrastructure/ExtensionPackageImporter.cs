// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection.PortableExecutable;
using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Infrastructure;

public sealed class ExtensionPackageImporter(IImportService imports)
{
    public async Task<ImportPlan> AnalyzeAsync(string source, string destination, string dllName, CancellationToken cancellationToken = default)
    {
        if (Path.GetFileName(dllName) != dllName || !dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(nameof(dllName));
        var plan = await imports.AnalyzeAsync(new(source), destination, cancellationToken: cancellationToken);
        try
        {
            var candidates = plan.Operations.Where(o => Path.GetFileName(o.RelativePath).Equals(dllName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (candidates.Length != 1) throw new InvalidDataException("ExtensionPackageMissing");
            var item = candidates[0];
            try
            {
                using var stream = File.OpenRead(item.StagedPath);
                using var pe = new PEReader(stream);
                if (pe.PEHeaders.PEHeader == null || !pe.PEHeaders.CoffHeader.Characteristics.HasFlag(Characteristics.Dll)) throw new InvalidDataException("ExtensionPackageInvalid");
            }
            catch (BadImageFormatException ex) { throw new InvalidDataException("ExtensionPackageInvalid", ex); }
            var target = PathSafety.ResolveInside(destination, dllName); bool exists = File.Exists(target);
            var operation = item with { RelativePath = dllName, DestinationPath = target, Exists = exists, OriginalHash = exists ? await AtomicFile.HashAsync(target, cancellationToken) : null };
            // 作者が指定したDLLだけを配置し、ZIP内のexeや別のDLLは実行・配置しない。
            return plan with { Operations = [operation], Diagnostics = [], Scenarios = 0, Vehicles = 0 };
        }
        catch { await imports.DiscardAsync(plan, CancellationToken.None); throw; }
    }
}
