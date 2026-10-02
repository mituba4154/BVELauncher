using BVELauncher.Core;

namespace BVELauncher.Application;

public sealed class LibraryScanner(IAssetReader reader, ILibraryStore store, ILocalLog log)
{
    private readonly Dictionary<string, (DateTime Stamp, long Size, AssetReadResult Result)> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<LibrarySnapshot> ScanAsync(IEnumerable<ScenarioRoot> roots, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(() => ScanCoreAsync(roots, progress, cancellationToken), cancellationToken).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    private async Task<LibrarySnapshot> ScanCoreAsync(IEnumerable<ScenarioRoot> roots, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var previous = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var assets = new List<AssetFile>(); var references = new List<AssetReference>(); var diagnostics = new List<DiagnosticItem>();
        var parsed = new Dictionary<string, AssetReadResult>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queued = new Queue<(string Path, AssetKind Hint)>();
        var usedIds = new HashSet<Guid>();
        var priorPaths = previous.Assets.ToDictionary(a => a.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.DistinctBy(r => Path.GetFullPath(r.Path), StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root.Path)) { diagnostics.Add(new(Severity.Warning, "RootMissing", root.Path)); continue; }
            foreach (var file in Directory.EnumerateFiles(root.Path, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System }))
                if (!Path.GetFileName(file).StartsWith(".BVELauncher-", StringComparison.OrdinalIgnoreCase)) queued.Enqueue((Path.GetFullPath(file), AssetKind.Unknown));
        }
        while (queued.TryDequeue(out var candidate))
        {
            string file = candidate.Path;
            cancellationToken.ThrowIfCancellationRequested();
            bool firstVisit = seen.Add(file);
            if (!File.Exists(file) || !firstVisit && (candidate.Hint == AssetKind.Unknown || parsed.ContainsKey(file) || assets.Any(a => a.Path.Equals(file, StringComparison.OrdinalIgnoreCase)))) continue;
            try
            {
                var info = new FileInfo(file);
                var prior = priorPaths.GetValueOrDefault(file);
                var binaryKind = BinaryKind(file);
                AssetReadResult? data = null;
                if (binaryKind == AssetKind.Unknown && info.Length <= 16 * 1024 * 1024)
                {
                    if (cache.TryGetValue(file, out var cached) && cached.Stamp == info.LastWriteTimeUtc && cached.Size == info.Length && !(cached.Result.Kind == AssetKind.Unknown && candidate.Hint != AssetKind.Unknown)) data = cached.Result;
                    else
                    {
                        // 先頭だけ確認し、未知バイナリーを文字列として全読み込みしない。
                        await using var stream = File.OpenRead(file);
                        byte[] prefix = new byte[256]; int length = await stream.ReadAsync(prefix, cancellationToken).ConfigureAwait(false);
                        var header = System.Text.Encoding.ASCII.GetString(prefix, 0, length);
                        if (candidate.Hint != AssetKind.Unknown || header.Contains("BveTs", StringComparison.OrdinalIgnoreCase) || header.Contains("B\0v\0e\0T\0s", StringComparison.OrdinalIgnoreCase))
                        {
                            data = await reader.ReadAsync(file, cancellationToken, candidate.Hint).ConfigureAwait(false);
                            cache[file] = (info.LastWriteTimeUtc, info.Length, data);
                        }
                    }
                }
                var kind = data?.Kind ?? binaryKind;
                if (kind == AssetKind.Unknown) continue;
                var identity = prior?.Id ?? FindMovedId(data?.Fingerprint, file) ?? Guid.NewGuid();
                if (!usedIds.Add(identity)) { identity = Guid.NewGuid(); usedIds.Add(identity); }
                assets.Add(new(identity, file, kind, info.Length, info.LastWriteTimeUtc, data?.Fingerprint));
                if (data != null)
                {
                    parsed[file] = data; diagnostics.AddRange(data.Diagnostics);
                    references.AddRange(data.References);
                    foreach (var reference in data.References.Where(r => r.Exists)) queued.Enqueue((reference.ResolvedPath, reference.TargetKind));
                }
                if (seen.Count % 32 == 0) progress?.Report(new(seen.Count, parsed.Values.Count(p => p.Kind == AssetKind.Scenario), file));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.DecoderFallbackException)
            {
                log.Write("Warning", "Asset scan failed: " + file, ex);
                diagnostics.Add(new(Severity.Warning, "ParseFailed", file, Detail: ex.Message));
                var old = previous.Scenarios.FirstOrDefault(s => s.Path.Equals(file, StringComparison.OrdinalIgnoreCase));
                if (old != null) parsed[file] = new(AssetKind.Scenario, new Dictionary<string, string> { ["Title"] = old.Title }, old.References, [diagnostics[^1]], old.Fingerprint);
            }
        }
        foreach (var reference in references.Where(r => !r.Exists)) diagnostics.Add(new(Severity.Warning, "ReferenceMissing", reference.SourcePath, reference.RawPath, reference.ResolvedPath));
        var scenarios = new List<ScenarioEntry>(); var vehicles = new List<VehicleEntry>();
        foreach (var asset in assets)
        {
            if (!parsed.TryGetValue(asset.Path, out var data)) continue;
            string Get(string key) => data.Metadata.GetValueOrDefault(key) ?? "";
            if (asset.Kind == AssetKind.Scenario)
            {
                var old = previous.Scenarios.FirstOrDefault(s => s.Id == asset.Id);
                var items = ReachableDiagnostics(asset.Path, references, diagnostics);
                scenarios.Add(new(asset.Id, asset.Path, Get("Title"), Get("RouteTitle"), Get("VehicleTitle"), Get("Author"), Get("Comment"),
                    data.References.FirstOrDefault(r => r.Key == "Image")?.ResolvedPath, data.References, asset.LastWriteUtc, data.Fingerprint)
                {
                    Alias = old?.Alias, CustomGroup = old?.CustomGroup, Favorite = old?.Favorite ?? false, Hidden = old?.Hidden ?? false,
                    RuntimeId = old?.RuntimeId, DefaultVehiclePath = old?.DefaultVehiclePath, LastLaunchUtc = old?.LastLaunchUtc,
                    Compatibility = new(items)
                });
            }
            else if (asset.Kind == AssetKind.Vehicle)
            {
                var titles = parsed.Values.Where(p => p.Kind == AssetKind.Scenario && p.References.Any(r => r.TargetKind == AssetKind.Vehicle && r.ResolvedPath.Equals(asset.Path, StringComparison.OrdinalIgnoreCase)))
                    .Select(p => p.Metadata.GetValueOrDefault("VehicleTitle") ?? "").Distinct().ToArray();
                vehicles.Add(new(asset.Id, asset.Path, Path.GetFileName(Path.GetDirectoryName(asset.Path)) ?? Path.GetFileName(asset.Path), titles, data.References, asset.LastWriteUtc, data.Fingerprint)
                { Alias = previous.Vehicles.FirstOrDefault(v => v.Id == asset.Id)?.Alias });
            }
        }
        foreach (var missing in cache.Keys.Where(k => !seen.Contains(k)).ToArray()) cache.Remove(missing);
        var result = new LibrarySnapshot(scenarios, vehicles, assets, references, diagnostics);
        await store.SaveAsync(result, cancellationToken).ConfigureAwait(false);
        progress?.Report(new(seen.Count, scenarios.Count, ""));
        return result;
        Guid? FindMovedId(string? fingerprint, string newPath)
        {
            if (fingerprint == null) return null;
            var candidates = previous.Assets.Where(a => a.Fingerprint == fingerprint && !File.Exists(a.Path) && !a.Path.Equals(newPath, StringComparison.OrdinalIgnoreCase)).ToArray();
            return candidates.Length == 1 ? candidates[0].Id : null;
        }
    }
    private static AssetKind BinaryKind(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    { ".dll" => AssetKind.Dll, ".png" or ".jpg" or ".jpeg" or ".bmp" => AssetKind.Image, ".wav" => AssetKind.Audio, ".x" => AssetKind.Model, _ => AssetKind.Unknown };
    public static IReadOnlyList<DiagnosticItem> ReachableDiagnostics(string path, IReadOnlyList<AssetReference> references, IReadOnlyList<DiagnosticItem> diagnostics)
    {
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
        var pending = new Queue<string>(); pending.Enqueue(path);
        var lookup = references.ToLookup(r => r.SourcePath, StringComparer.OrdinalIgnoreCase);
        while (pending.TryDequeue(out var current))
            foreach (var reference in lookup[current]) if (reachable.Add(reference.ResolvedPath)) pending.Enqueue(reference.ResolvedPath);
        return diagnostics.Where(d => reachable.Contains(d.SourcePath)).ToArray();
    }
}
