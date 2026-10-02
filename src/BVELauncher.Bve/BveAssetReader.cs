using System.Security.Cryptography;
using System.Text;
using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Bve;

public sealed class BveAssetReader : IAssetReader
{
    public async Task<AssetReadResult> ReadAsync(string path, CancellationToken cancellationToken, AssetKind expectedKind = AssetKind.Unknown)
    {
        var references = new List<AssetReference>();
        var diagnostics = new List<DiagnosticItem>();
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes.Length > 16 * 1024 * 1024) throw new InvalidDataException("Metadata file too large.");
        var document = BveDocument.Parse(bytes);
        var kind = Identify(document.Header, path);
        if (kind == AssetKind.Unknown && expectedKind != AssetKind.Unknown) kind = expectedKind;
        var fields = document.Fields;
        if (kind == AssetKind.Scenario)
        {
            foreach (var key in new[] { "Route", "Vehicle", "Image" })
                foreach (var reference in WeightedPaths(document.Get(key)))
                    Add(key, reference, key switch { "Route" => AssetKind.Map, "Vehicle" => AssetKind.Vehicle, _ => AssetKind.Image });
            if (!document.Header.StartsWith("BveTs Scenario 2.", StringComparison.OrdinalIgnoreCase))
                diagnostics.Add(new(Severity.Warning, "HeaderVersion", path));
            foreach (var required in new[] { "Route", "Vehicle" })
                if (document.Get(required).Length == 0) diagnostics.Add(new(Severity.Warning, "MissingField", path, required));
        }
        else if (kind == AssetKind.Vehicle)
        {
            foreach (var (key, type) in new Dictionary<string, AssetKind>
            {
                ["PerformanceCurve"] = AssetKind.Performance, ["Parameters"] = AssetKind.Parameters,
                ["Panel"] = AssetKind.Panel, ["Sound"] = AssetKind.VehicleSound,
                ["MotorNoise"] = AssetKind.MotorNoise, ["Ats32"] = AssetKind.Dll, ["Ats64"] = AssetKind.Dll
            }) if (document.Get(key).Length > 0) Add(key, document.Get(key), type);
        }
        else if (kind is AssetKind.Map)
        {
            foreach (var reference in MapReferenceScanner.Scan(document.Encoding.GetString(bytes).TrimStart('\uFEFF')))
                Add(reference.Key, reference.Path, reference.Kind);
        }
        else if (kind is AssetKind.Performance or AssetKind.StructureList or AssetKind.SoundList or AssetKind.VehicleSound or AssetKind.Panel or AssetKind.MotorNoise or AssetKind.Train)
        {
            var text = document.Encoding.GetString(bytes).TrimStart('\uFEFF');
            if (kind is AssetKind.Panel or AssetKind.Performance or AssetKind.Train)
            {
                foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                {
                    var pair = line.Split('#', ';')[0].Split('=', 2);
                    if (pair.Length != 2 || pair[1].Trim().Length == 0) continue;
                    string key = pair[0].Trim();
                    if (kind == AssetKind.Panel && (key.Equals("DaytimeImage", StringComparison.OrdinalIgnoreCase) || key.Equals("NighttimeImage", StringComparison.OrdinalIgnoreCase))) Add(key, pair[1].Trim(), AssetKind.Image);
                    else if (kind == AssetKind.Performance && new[] { "Force", "MaxForce", "Current", "MaxCurrent", "NoLoadCurrent", "Params" }.Contains(key, StringComparer.OrdinalIgnoreCase)) Add(key, pair[1].Trim(), AssetKind.Performance);
                    else if (kind == AssetKind.Train && new[] { "Structure", "Sound", "Model" }.Contains(key, StringComparer.OrdinalIgnoreCase)) Add(key, pair[1].Trim(), AssetKind.Unknown);
                }
            }
            else
            {
                foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Skip(1))
                {
                    var columns = CsvColumns(line);
                    if (columns.Count >= 2 && columns[1].Trim().Length > 0)
                        Add(columns[0], columns[1], kind == AssetKind.StructureList ? AssetKind.Model : AssetKind.Audio);
                }
            }
        }
        return new(kind, fields, references, diagnostics, Convert.ToHexString(SHA256.HashData(bytes)));
        void Add(string key, string raw, AssetKind target)
        {
            try
            {
                var resolved = Resolve(path, raw);
                references.Add(new(path, key, raw, resolved, target, File.Exists(resolved)));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            { diagnostics.Add(new(Severity.Warning, "InvalidPath", path, raw, Detail: ex.Message)); }
        }
    }

    public async Task<string> MaterializeAsync(string scenarioPath, VehicleOverride vehicle, string fallbackDirectory, CancellationToken cancellationToken)
    {
        var document = BveDocument.Parse(await File.ReadAllBytesAsync(scenarioPath, cancellationToken).ConfigureAwait(false));
        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(scenarioPath))!;
        string generated = Path.Combine(sourceDirectory, ".BVELauncher-" + Guid.NewGuid().ToString("N") + ".txt");
        document.Set("Vehicle", Path.GetRelativePath(sourceDirectory, vehicle.VehiclePath));
        document.Set("VehicleTitle", vehicle.Title);
        try
        {
            await using var stream = new FileStream(generated, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, true);
            await stream.WriteAsync(document.ToBytes(), cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return generated;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Directory.CreateDirectory(fallbackDirectory);
            // 別ドライブの絶対参照は未検証なので安全な同一ドライブだけ許可する。
            if (!Path.GetPathRoot(sourceDirectory)!.Equals(Path.GetPathRoot(fallbackDirectory), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Cross-drive generated scenarios require verified absolute references.", ex);
            foreach (var key in new[] { "Route", "Image" })
            {
                var original = document.Get(key);
                if (original.Length > 0) document.Set(key, RebaseWeighted(original, scenarioPath, fallbackDirectory));
            }
            document.Set("Vehicle", Path.GetRelativePath(fallbackDirectory, vehicle.VehiclePath));
            generated = Path.Combine(fallbackDirectory, ".BVELauncher-" + Guid.NewGuid().ToString("N") + ".txt");
            await File.WriteAllBytesAsync(generated, document.ToBytes(), cancellationToken).ConfigureAwait(false);
            return generated;
        }
    }
    public static string Resolve(string source, string reference) => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source))!, reference.Trim().Replace('\\', Path.DirectorySeparatorChar)));
    public static IEnumerable<string> WeightedPaths(string value) => value.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('*')[0].Trim()).Where(p => p.Length > 0);
    private static string RebaseWeighted(string value, string source, string destination) => string.Join(" | ", value.Split('|').Select(part =>
    {
        var star = part.LastIndexOf('*');
        var raw = star >= 0 ? part[..star].Trim() : part.Trim();
        return Path.GetRelativePath(destination, Resolve(source, raw)) + (star >= 0 ? " " + part[star..].Trim() : "");
    }));
    public static AssetKind Identify(string header, string path)
    {
        var formats = new (string Header, AssetKind Kind)[]
        {
            ("BveTs Scenario", AssetKind.Scenario), ("BveTs Map", AssetKind.Map),
            ("BveTs Vehicle Parameters", AssetKind.Parameters), ("BveTs Vehicle Performance", AssetKind.Performance),
            ("BveTs Vehicle Sound", AssetKind.VehicleSound), ("BveTs Vehicle", AssetKind.Vehicle),
            ("BveTs Instrument Panel", AssetKind.Panel), ("BveTs Panel", AssetKind.Panel), ("BveTs Motor Noise", AssetKind.MotorNoise),
            ("BveTs Structure List", AssetKind.StructureList), ("BveTs Sound List", AssetKind.SoundList),
            ("BveTs Station List", AssetKind.StationList), ("BveTs Signal Aspects List", AssetKind.SignalList),
            ("BveTs Train", AssetKind.Train)
        };
        foreach (var item in formats) if (header.StartsWith(item.Header + " ", StringComparison.OrdinalIgnoreCase)) return item.Kind;
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".dll" => AssetKind.Dll, ".png" or ".jpg" or ".jpeg" or ".bmp" => AssetKind.Image,
            ".wav" => AssetKind.Audio, ".x" => AssetKind.Model, _ => AssetKind.Unknown
        };
    }
    private static List<string> CsvColumns(string line)
    {
        var columns = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (line[i] == '#' && !quoted) break;
            else if (line[i] == ',' && !quoted) { columns.Add(current.ToString().Trim()); current.Clear(); }
            else current.Append(line[i]);
        }
        columns.Add(current.ToString().Trim());
        return columns;
    }
}
