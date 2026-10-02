// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text;

namespace BVELauncher.Bve;

public static class KeyboardPresetReader
{
    // BVE標準プリセットはヘッダーなしのタブ区切り。未知の割り当ては適用しない。
    public static IReadOnlyDictionary<string, string> Read(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            var fields = trimmed.Split('\t');
            if (fields.Length != 2 || !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value is < 0 or > 65535)
                throw new InvalidDataException("SettingsOutOfRange");
            var parts = fields[0].Split('-');
            string? property = null;
            if (parts.Length == 3 && parts[0] == "axis" && int.TryParse(parts[1], out var axis) && axis is >= 0 and < 4 && int.TryParse(parts[2], out var action) && action is >= 0 and < 6)
                property = $"KeyAxis{axis}_{action}";
            else if (parts.Length == 2 && int.TryParse(parts[1], out var index))
                property = parts[0] switch { "sw" when index is >= 0 and < 4 => $"KeySwitch{index}", "ats" when index is >= 0 and < 16 => $"KeyAts{index}", "sim" when index is >= 0 and < 13 => $"KeySimulation{index}", _ => null };
            if (property == null || !result.TryAdd(property, value.ToString(CultureInfo.InvariantCulture))) throw new InvalidDataException("SettingsInvalidChoice");
        }
        if (result.Count == 0) throw new InvalidDataException("SettingsInvalidChoice");
        return result;
    }
}
