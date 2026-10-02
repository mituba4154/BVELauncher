// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Core;

namespace BVELauncher.Bve;

public static class MapReferenceScanner
{
    public static IReadOnlyList<(string Key, string Path, AssetKind Kind)> Scan(string text)
    {
        var tokens = Tokenize(text);
        var result = new List<(string, string, AssetKind)>();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Quoted) continue;
            var name = tokens[i].Value;
            if (name.Equals("include", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Count && tokens[i + 1].Quoted)
                result.Add((name, tokens[i + 1].Value, AssetKind.Map));
            if (!name.Equals("load", StringComparison.OrdinalIgnoreCase) || i < 2 || tokens[i - 1].Value != ".") continue;
            int root = i - 2;
            // Train['key'].Load と通常の Structure.Load の両方を扱う。
            if (tokens[root].Value == "]")
            {
                while (root >= 0 && tokens[root].Value != "[") root--;
                root--;
            }
            if (root < 0 || i + 2 >= tokens.Count || tokens[i + 1].Value != "(" || !tokens[i + 2].Quoted) continue;
            var key = tokens[root].Value;
            var kind = key.ToLowerInvariant() switch
            {
                "structure" => AssetKind.StructureList,
                "sound" or "sound3d" => AssetKind.SoundList,
                "station" => AssetKind.StationList,
                "signal" => AssetKind.SignalList,
                "train" => AssetKind.Train,
                _ => AssetKind.Unknown
            };
            if (kind != AssetKind.Unknown) result.Add((key + ".Load", tokens[i + 2].Value, kind));
        }
        return result;
    }
    internal static List<Token> Tokenize(string text)
    {
        var result = new List<Token>();
        for (int i = 0; i < text.Length;)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '#' || c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] is not ('\r' or '\n')) i++;
                continue;
            }
            if (c is '\'' or '"')
            {
                char quote = c;
                var value = new System.Text.StringBuilder();
                i++;
                bool closed = false;
                while (i < text.Length)
                {
                    if (text[i] == quote)
                    {
                        if (i + 1 < text.Length && text[i + 1] == quote) { value.Append(quote); i += 2; continue; }
                        i++; closed = true; break;
                    }
                    value.Append(text[i++]);
                }
                if (closed) result.Add(new(value.ToString(), true));
                continue;
            }
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                int start = i++;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                result.Add(new(text[start..i], false));
            }
            else { result.Add(new(c.ToString(), false)); i++; }
        }
        return result;
    }
    internal sealed record Token(string Value, bool Quoted);
}
