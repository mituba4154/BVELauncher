using BVELauncher.Core;

namespace BVELauncher.Application;

public sealed class LibraryScanner(IAssetReader reader, ILibraryStore store, ILocalLog log)
{
    private readonly Dictionary<string, CachedAssetRead> cache = new(StringComparer.OrdinalIgnoreCase);
    private bool cacheLoaded;
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<LibrarySnapshot> ScanAsync(IEnumerable<ScenarioRoot> roots, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(() => ScanCoreAsync(roots, progress, cancellationToken), cancellationToken).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    private async Task<LibrarySnapshot> ScanCoreAsync(IEnumerable<ScenarioRoot> roots, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew(); int cacheHits = 0, parsedFiles = 0, headerReads = 0, scenarioCount = 0;
        if (!cacheLoaded && store is IAssetCacheStore persisted)
        { foreach (var entry in await persisted.LoadAssetCacheAsync(cancellationToken).ConfigureAwait(false)) cache[entry.Path] = entry; cacheLoaded = true; }
        var previous = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var assets = new List<AssetFile>(); var references = new List<AssetReference>(); var diagnostics = new List<DiagnosticItem>();
        var parsed = new Dictionary<string, AssetReadResult>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var assetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existence = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var queued = new Queue<(string Path, AssetKind Hint)>();
        var usedIds = new HashSet<Guid>();
        var priorPaths = previous.Assets.ToDictionary(a => a.Path, StringComparer.OrdinalIgnoreCase);
        var discovered = new List<ScenarioEntry>();
        var priorScenarios = previous.Scenarios.ToDictionary(s => s.Id);
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
            if (!Exists(file) || !firstVisit && (candidate.Hint == AssetKind.Unknown || parsed.ContainsKey(file) || assetPaths.Contains(file))) continue;
            bool scenarioHeader = false;
            try
            {
                var info = new FileInfo(file);
                var prior = priorPaths.GetValueOrDefault(file);
                var binaryKind = BinaryKind(file);
                AssetReadResult? data = null;
                if (binaryKind == AssetKind.Unknown && info.Length <= 16 * 1024 * 1024)
                {
                    if (cache.TryGetValue(file, out var cached) && cached.Stamp == info.LastWriteTimeUtc && cached.Size == info.Length && !((cached.Result == null || cached.Result.Kind == AssetKind.Unknown) && candidate.Hint != AssetKind.Unknown))
                    { data = cached.Result; cacheHits++; }
                    else
                    {
                        // 先頭だけ確認し、未知バイナリーを文字列として全読み込みしない。
                        await using var stream = File.OpenRead(file);
                        headerReads++;
                        byte[] prefix = new byte[256]; int length = await stream.ReadAsync(prefix, cancellationToken).ConfigureAwait(false);
                        var header = System.Text.Encoding.ASCII.GetString(prefix, 0, length);
                        scenarioHeader = header.Contains("BveTs Scenario", StringComparison.OrdinalIgnoreCase) || header.Contains("B\0v\0e\0T\0s\0 \0S\0c\0e\0n\0a\0r\0i\0o", StringComparison.OrdinalIgnoreCase);
                        if (candidate.Hint != AssetKind.Unknown || header.Contains("BveTs", StringComparison.OrdinalIgnoreCase) || header.Contains("B\0v\0e\0T\0s", StringComparison.OrdinalIgnoreCase))
                        {
                            data = await reader.ReadAsync(file, cancellationToken, candidate.Hint).ConfigureAwait(false);
                            parsedFiles++;
                        }
                        // 未知テキストも記録し、再起動時の先頭読み込みを避ける。
                        cache[file] = new(file, info.LastWriteTimeUtc, info.Length, data);
                    }
                }
                // 参照先の追加・削除は、元テキストが不変でも診断に反映する。
                if (data != null) data = data with { References = data.References.Select(r => r with { Exists = Exists(r.ResolvedPath) }).ToArray() };
                var kind = data?.Kind ?? binaryKind;
                if (kind == AssetKind.Unknown) continue;
                var identity = prior?.Id ?? FindMovedId(data?.Fingerprint, file) ?? Guid.NewGuid();
                if (!usedIds.Add(identity)) { identity = Guid.NewGuid(); usedIds.Add(identity); }
                assets.Add(new(identity, file, kind, info.Length, info.LastWriteTimeUtc, data?.Fingerprint));
                assetPaths.Add(file);
                if (data != null)
                {
                    parsed[file] = data; diagnostics.AddRange(data.Diagnostics);
                    references.AddRange(data.References);
                    foreach (var reference in data.References.Where(r => r.Exists)) queued.Enqueue((reference.ResolvedPath, reference.TargetKind));
                    if (kind == AssetKind.Scenario)
                    {
                        scenarioCount++;
                        var old = priorScenarios.GetValueOrDefault(identity);
                        string GetMetadata(string key) => data.Metadata.GetValueOrDefault(key) ?? "";
                        discovered.Add(new(identity, file, GetMetadata("Title"), GetMetadata("RouteTitle"), GetMetadata("VehicleTitle"), GetMetadata("Author"), GetMetadata("Comment"), data.References.FirstOrDefault(r => r.Key == "Image")?.ResolvedPath, data.References, info.LastWriteTimeUtc, data.Fingerprint)
                        { Alias = old?.Alias, CustomGroup = old?.CustomGroup, Favorite = old?.Favorite ?? false, Hidden = old?.Hidden ?? false, RuntimeId = old?.RuntimeId, DefaultVehiclePath = old?.DefaultVehiclePath, LastLaunchUtc = old?.LastLaunchUtc });
                        if (discovered.Count == 8) { progress?.Report(new(seen.Count, scenarioCount, file, discovered.ToArray())); discovered.Clear(); }
                    }
                }
                if (seen.Count % 256 == 0) progress?.Report(new(seen.Count, scenarioCount, file));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.DecoderFallbackException)
            {
                log.Write("Warning", "Asset scan failed: " + file, ex);
                diagnostics.Add(new(Severity.Warning, "ParseFailed", file, Detail: ex.Message));
                var old = previous.Scenarios.FirstOrDefault(s => s.Path.Equals(file, StringComparison.OrdinalIgnoreCase));
                if (old != null || scenarioHeader)
                {
                    var identity = old?.Id ?? Guid.NewGuid(); var info = new FileInfo(file);
                    assets.Add(new(identity, file, AssetKind.Scenario, info.Length, info.LastWriteTimeUtc, old?.Fingerprint));
                    assetPaths.Add(file); scenarioCount++;
                    parsed[file] = new(AssetKind.Scenario, new Dictionary<string, string> { ["Title"] = old?.Title ?? Path.GetFileNameWithoutExtension(file) }, old?.References ?? [], [diagnostics[^1]], old?.Fingerprint ?? "");
                }
            }
        }
        if (discovered.Count > 0) progress?.Report(new(seen.Count, scenarioCount, "", discovered.ToArray()));
        foreach (var reference in references.Where(r => !r.Exists)) diagnostics.Add(new(Severity.Warning, "ReferenceMissing", reference.SourcePath, reference.RawPath, reference.ResolvedPath));
        var scenarios = new List<ScenarioEntry>(); var vehicles = new List<VehicleEntry>();
        foreach (var asset in assets)
        {
            if (!parsed.TryGetValue(asset.Path, out var data)) continue;
            string Get(string key) => data.Metadata.GetValueOrDefault(key) ?? "";
            if (asset.Kind == AssetKind.Scenario)
            {
                var old = priorScenarios.GetValueOrDefault(asset.Id);
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
        if (store is IAssetCacheStore cacheStore) await cacheStore.SaveAssetCacheAsync(cache.Values, cancellationToken).ConfigureAwait(false);
        log.Write("Info", $"Scan completed: files={seen.Count}, scenarios={scenarios.Count}, parsed={parsedFiles}, cacheHits={cacheHits}, headerReads={headerReads}, elapsedMs={elapsed.ElapsedMilliseconds}.");
        progress?.Report(new(seen.Count, scenarios.Count, ""));
        return result;
        bool Exists(string path)
        { if (!existence.TryGetValue(path, out bool exists)) existence[path] = exists = File.Exists(path); return exists; }
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
