using System.Text;
using System.Text.RegularExpressions;

namespace BVELauncher.Bve;

// 読み取った書式を保ち、変更した値の区間だけ差し替える。
public sealed partial class BveDocument
{
    private readonly List<DocumentLine> lines;
    public Encoding Encoding { get; }
    public bool HasPreamble { get; }
    public string Header => lines.FirstOrDefault()?.Text.Trim() ?? "";
    public string NewLine => lines.Select(l => l.Ending).FirstOrDefault(e => e.Length > 0) ?? Environment.NewLine;
    private BveDocument(List<DocumentLine> lines, Encoding encoding, bool preamble)
        => (this.lines, Encoding, HasPreamble) = (lines, encoding, preamble);

    public static BveDocument Parse(byte[] bytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding encoding;
        int offset;
        if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) { encoding = new UTF8Encoding(true, true); offset = 3; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe, 0, 0 })) { encoding = new UTF32Encoding(false, true, true); offset = 4; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xfe, 0xff })) { encoding = new UTF32Encoding(true, true, true); offset = 4; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe })) { encoding = new UnicodeEncoding(false, true, true); offset = 2; }
        else if (bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff })) { encoding = new UnicodeEncoding(true, true, true); offset = 2; }
        else
        {
            offset = 0;
            var header = Encoding.ASCII.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 256))).Split('\r', '\n')[0];
            var colon = header.IndexOf(':');
            if (colon >= 0)
            {
                var name = header[(colon + 1)..].Split(',', '#', ';')[0].Trim();
                encoding = name.Equals("shift_jis", StringComparison.OrdinalIgnoreCase) || name.Equals("sjis", StringComparison.OrdinalIgnoreCase)
                    ? Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                    : Encoding.GetEncoding(name, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            }
            else
            {
                encoding = new UTF8Encoding(false, true);
                try { _ = encoding.GetString(bytes); }
                catch (DecoderFallbackException) { encoding = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback); }
            }
        }
        var text = encoding.GetString(bytes, offset, bytes.Length - offset);
        var lines = new List<DocumentLine>();
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('\r' or '\n')) continue;
            int length = text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
            lines.Add(new(text[start..i], text.Substring(i, length)));
            i += length - 1;
            start = i + 1;
        }
        if (start < text.Length || lines.Count == 0) lines.Add(new(text[start..], ""));
        return new(lines, encoding, offset > 0);
    }

    public IReadOnlyDictionary<string, string> Fields
    {
        get
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                var match = Assignment().Match(line.Text);
                if (match.Success) result[match.Groups[1].Value.Trim()] = match.Groups[2].Value.Trim();
            }
            return result;
        }
    }
    public string Get(string key) => Fields.GetValueOrDefault(key) ?? "";
    public void Set(string key, string value)
    {
        if (value.IndexOfAny(['\r', '\n', '#', ';']) >= 0) throw new ArgumentException("Invalid BVE value.", nameof(value));
        bool found = false;
        for (int i = 1; i < lines.Count; i++)
        {
            var match = Assignment().Match(lines[i].Text);
            if (!match.Success || !match.Groups[1].Value.Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
            var group = match.Groups[2];
            var raw = group.Value;
            int lead = raw.Length - raw.TrimStart().Length;
            int trail = raw.Length - raw.TrimEnd().Length;
            if (lead + trail > raw.Length) trail = 0;
            int begin = group.Index + lead;
            int end = group.Index + raw.Length - trail;
            lines[i] = lines[i] with { Text = lines[i].Text[..begin] + value + lines[i].Text[end..] };
            found = true;
        }
        if (found) return;
        if (lines.Count > 0 && lines[^1].Ending.Length == 0) lines[^1] = lines[^1] with { Ending = NewLine };
        lines.Add(new(key + " = " + value, NewLine));
    }
    public byte[] ToBytes()
    {
        var content = Encoding.GetBytes(string.Concat(lines.Select(l => l.Text + l.Ending)));
        return HasPreamble ? Encoding.GetPreamble().Concat(content).ToArray() : content;
    }
    [GeneratedRegex(@"^\s*([^=;#]+?)\s*=(.*?)(?:[;#].*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Assignment();
    private sealed record DocumentLine(string Text, string Ending);
}
