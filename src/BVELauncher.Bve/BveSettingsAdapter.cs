// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Bve;

public abstract class BveSettingsAdapter(IBackupService backups) : IBveSettingsAdapter
{
    public abstract RuntimeKind Kind { get; }
    public Task<BveSettingsSnapshot> ReadAsync(string path, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var document = Load(bytes);
        var definitions = Definitions(document);
        var values = definitions.ToDictionary(d => d.Property, d => ReadValue(document, d), StringComparer.Ordinal);
        return new BveSettingsSnapshot(path, Kind, Convert.ToHexString(SHA256.HashData(bytes)), values, definitions);
    }, cancellationToken);
    public Task SaveAsync(BveSettingsSnapshot snapshot, IReadOnlyDictionary<string, string> changes, CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        var bytes = await File.ReadAllBytesAsync(snapshot.Path, cancellationToken).ConfigureAwait(false);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != snapshot.OriginalHash) throw new IOException("SettingsChanged");
        var document = Load(bytes);
        var replacements = new Dictionary<XmlNode, string>();
        var stringLists = new Dictionary<XmlNode, IReadOnlyList<string>>();
        var append = new HashSet<XmlNode>();
        foreach (var (property, value) in changes)
        {
            var definition = snapshot.Definitions.Single(d => d.Property == property);
            Validate(definition, value);
            if (property == "FontSize")
            {
                foreach (XmlNode scale in document.SelectNodes("/Preferences/Assistants/AssistantSettings/Scale | /Preferences/Timetable/Scale")!) replacements[scale] = value;
                continue;
            }
            if (definition.Property == "InputPlugins")
            {
                var node = document.SelectSingleNode(definition.XmlPath) ?? throw new InvalidDataException("SettingsNodeMissing");
                var names = new List<string>();
                foreach (var name in value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (name.IndexOfAny(['/', '\\', ':']) >= 0) throw new InvalidDataException("InputPluginNameInvalid");
                    names.Add(name);
                }
                stringLists[node] = names;
            }
            else
            {
                var node = document.SelectSingleNode(definition.XmlPath);
                if (node == null && property == "ScenarioDirectory")
                {
                    var address = document.SelectSingleNode("/Preferences/ScenarioForm/Address") ?? throw new InvalidDataException("SettingsNodeMissing: " + property);
                    replacements[address] = "<string>" + XmlValueEditor.Escape(value) + "</string>";
                    append.Add(address); continue;
                }
                if (node == null) throw new InvalidDataException("SettingsNodeMissing: " + property);
                replacements[node] = XmlValueEditor.Escape(value);
            }
        }
        var encoding = DetectXmlEncoding(bytes);
        var text = encoding.GetString(bytes).TrimStart('\uFEFF');
        var patched = XmlValueEditor.Replace(text, document, replacements, append, stringLists);
        var content = encoding.GetPreamble().Concat(encoding.GetBytes(patched)).ToArray();
        _ = Load(content);
        cancellationToken.ThrowIfCancellationRequested();
        var backup = await backups.CreateAsync(snapshot.Path, "BvePreferences", cancellationToken).ConfigureAwait(false);
        if (backup.Hash != snapshot.OriginalHash) throw new IOException("SettingsChanged");
        string temporary = snapshot.Path + ".BVELauncher-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous | FileOptions.WriteThrough))
            { await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false); stream.Flush(true); }
            _ = Load(await File.ReadAllBytesAsync(temporary, cancellationToken).ConfigureAwait(false));
            cancellationToken.ThrowIfCancellationRequested();
            for (int attempt = 0; ; attempt++)
            {
                if (Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(snapshot.Path, cancellationToken).ConfigureAwait(false))) != snapshot.OriginalHash) throw new IOException("SettingsChanged");
                try { File.Replace(temporary, snapshot.Path, null); break; }
                // 一時的な読取ハンドルだけ待つ。再試行ごとに外部変更を確認し、上書きを避ける。
                catch (IOException ex) when (attempt < 4 && (ex.HResult & 0xffff) is 32 or 33 or 1175)
                { await Task.Delay(100 * (attempt + 1), cancellationToken).ConfigureAwait(false); }
            }
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }, cancellationToken);
    private static XmlDocument Load(byte[] bytes)
    {
        var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8 * 1024 * 1024 });
        document.Load(reader);
        if (document.DocumentElement?.Name != "Preferences") throw new InvalidDataException("SettingsRootInvalid");
        return document;
    }
    private static Encoding DetectXmlEncoding(byte[] bytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe })) return new UnicodeEncoding(false, true);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff })) return new UnicodeEncoding(true, true);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) return new UTF8Encoding(true);
        return new UTF8Encoding(false);
    }
    private static string ReadValue(XmlDocument document, BveSettingDefinition definition)
        => definition.Property == "InputPlugins" ? string.Join("|", document.SelectNodes(definition.XmlPath + "/string")!.Cast<XmlNode>().Select(n => n.InnerText)) : document.SelectSingleNode(definition.XmlPath)?.InnerText ?? definition.Default;
    public static void Validate(BveSettingDefinition definition, string value)
    {
        if (definition.Type == SettingType.Boolean && value is not ("true" or "false")) throw new InvalidDataException("SettingsInvalidBoolean");
        if (definition.Type is SettingType.Integer or SettingType.Key)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < definition.Minimum || number > definition.Maximum) throw new InvalidDataException("SettingsOutOfRange");
        }
        if (definition.Choices != null && !definition.Choices.Contains(value, StringComparer.Ordinal)) throw new InvalidDataException("SettingsInvalidChoice");
    }
    private static IReadOnlyList<BveSettingDefinition> Definitions(XmlDocument document)
    {
        var result = new List<BveSettingDefinition>();
        Add("Windowed", "Graphics", "Windowed", SettingType.Boolean, "true");
        Add("WindowWidth", "Graphics", "WindowSize/Width", SettingType.Integer, "800", 1, 9999);
        Add("WindowHeight", "Graphics", "WindowSize/Height", SettingType.Integer, "600", 1, 9999);
        Add("ScreenWidth", "Graphics", "ScreenSize/Width", SettingType.Integer, "0", 0, 9999);
        Add("ScreenHeight", "Graphics", "ScreenSize/Height", SettingType.Integer, "0", 0, 9999);
        Add("DrawDistance", "Graphics", "DrawDistance", SettingType.Choice, "800", choices: ["400", "600", "800", "1000", "1200", "1600", "2000", "2600", "3200", "4000", "5000", "6400", "8000", "10000"]);
        Add("Anisotropy", "Graphics", "Anisotropy", SettingType.Choice, "0", choices: ["0", "1", "2", "4", "8", "16"]);
        Add("FontName", "Assistants", "FontName", SettingType.Text, "");
        Add("FontStyle", "Assistants", "FontStyle", SettingType.Choice, "Bold", choices: ["Regular", "Bold"]);
        Add("FontSize", "Assistants", "Assistants/AssistantSettings[1]/Scale", SettingType.Integer, "18", 8, 96);
        Add("HeadMotion", "Simulation", "HeadMotion", SettingType.Integer, "25", 0, 100);
        Add("Language", "General", "Language", SettingType.Choice, "Japanese", choices: ["Japanese", "English"]);
        Add("ScenarioDirectory", "General", "ScenarioForm/Address/string[1]", SettingType.Path, "");
        Add("InputPlugins", "Input", "InputPlugins", SettingType.Text, "");
        Add("DevelopmentEnabled", "Advanced", "DevelopmentEnabled", SettingType.Boolean, "false");
        foreach (var name in new[] { "lever", "clock", "sta", "stop", "limit", "geom", "sway", "inst", "detail" })
        {
            string path = $"Assistants/AssistantSettings[Name='{name}']";
            Add("Assist_" + name, "Assistants", path + "/Visible", SettingType.Boolean, name == "detail" ? "false" : "true");
            Add("AssistAuto_" + name, "Assistants", path + "/AutoLocation", SettingType.Boolean, "true");
            Add("AssistSize_" + name, "Assistants", path + "/Scale", SettingType.Integer, "18", 8, 96);
            Add("AssistX_" + name, "Assistants", path + "/Location/X", SettingType.Integer, "0", -10000, 10000);
            Add("AssistY_" + name, "Assistants", path + "/Location/Y", SettingType.Integer, "0", -10000, 10000);
        }
        Add("TimetableSize", "Assistants", "Timetable/Scale", SettingType.Integer, "18", 8, 96);
        int[][] axisDefaults = [[40,0,38,0,0,0], [81,65,90,0,0,0], [0,188,190,0,0,191], [81,65,90,49,0,0]];
        for (int axis = 0; axis < 4; axis++)
            for (int action = 0; action < 6; action++)
                Add($"KeyAxis{axis}_{action}", "Keyboard", $"KeyAssignment/Axis/ArrayOfInt[{axis + 1}]/int[{action + 1}]", SettingType.Key, axisDefaults[axis][action].ToString(), 0, 65535);
        AddKeys("Switch", [13,107,8,109]);
        AddKeys("Ats", [32,45,46,36,35,33,34,50,51,52,53,54,55,56,57,48]);
        AddKeys("Simulation", [104,98,100,102,101,110,96,86,114,116,118,119,80]);
        return result;
        void Add(string property, string category, string relativePath, SettingType type, string defaultValue, double? min = null, double? max = null, string[]? choices = null)
            => result.Add(new(property, "Bve_" + property, category, "/Preferences/" + relativePath, type, defaultValue, min, max, choices, true));
        void AddKeys(string name, int[] defaults)
        {
            for (int i = 0; i < defaults.Length; i++) Add($"Key{name}{i}", "Keyboard", $"KeyAssignment/{name}/int[{i + 1}]", SettingType.Key, defaults[i].ToString(), 0, 65535);
        }
    }
}
public sealed class Bve5SettingsAdapter(IBackupService backups) : BveSettingsAdapter(backups) { public override RuntimeKind Kind => RuntimeKind.Bve5; }
public sealed class Bve6SettingsAdapter(IBackupService backups) : BveSettingsAdapter(backups) { public override RuntimeKind Kind => RuntimeKind.Bve6; }
