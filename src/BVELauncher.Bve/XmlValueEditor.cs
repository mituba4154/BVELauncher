using System.Security;
using System.Xml;

namespace BVELauncher.Bve;

// XMLを検証した上で対象要素の文字区間だけ変更し、未知要素と改行を保持する。
internal static class XmlValueEditor
{
    public static string Replace(string original, XmlDocument document, IReadOnlyDictionary<XmlNode, string> values, IReadOnlySet<XmlNode>? append = null, IReadOnlyDictionary<XmlNode, IReadOnlyList<string>>? stringLists = null)
    {
        var spans = new List<ElementSpan>(); var stack = new Stack<ElementSpan>();
        for (int i = 0; i < original.Length;)
        {
            int start = original.IndexOf('<', i); if (start < 0) break;
            if (original.AsSpan(start).StartsWith("<!--")) { i = original.IndexOf("-->", start, StringComparison.Ordinal) + 3; continue; }
            if (original.AsSpan(start).StartsWith("<![CDATA[")) { i = original.IndexOf("]]>", start, StringComparison.Ordinal) + 3; continue; }
            if (original.AsSpan(start).StartsWith("<?")) { i = original.IndexOf("?>", start, StringComparison.Ordinal) + 2; continue; }
            bool closing = original[start + 1] == '/';
            int end = start + 1; char quote = '\0';
            for (; end < original.Length; end++)
            {
                if (quote != '\0') { if (original[end] == quote) quote = '\0'; }
                else if (original[end] is '\'' or '"') quote = original[end];
                else if (original[end] == '>') break;
            }
            if (closing) { var span = stack.Pop(); span.EndStart = start; span.End = end + 1; }
            else
            {
                var span = new ElementSpan(start, end + 1, original[end - 1] == '/'); spans.Add(span);
                if (span.Empty) { span.EndStart = end - 1; span.End = end + 1; }
                else stack.Push(span);
            }
            i = end + 1;
        }
        var elements = document.SelectNodes("//*")!.Cast<XmlNode>().ToArray();
        if (elements.Length != spans.Count) throw new InvalidDataException("XmlSpanMismatch");
        var replacements = new List<(int Start, int Length, string Value)>();
        var locations = elements.Select((node, i) => (node, span: spans[i])).ToDictionary(p => p.node, p => p.span);
        for (int i = 0; i < elements.Length; i++)
        {
            var span = spans[i];
            string? value = null;
            if (stringLists?.TryGetValue(elements[i], out var names) == true)
            {
                var children = elements[i].ChildNodes.Cast<XmlNode>().Where(n => n.NodeType == XmlNodeType.Element && n.Name == "string").ToArray();
                var inner = span.Empty ? "" : original[span.OpenEnd..span.EndStart];
                var edits = new List<(int Start, int Length, string Value)>();
                for (int childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    var child = children[childIndex]; var childSpan = locations[child];
                    if (childIndex >= names.Count) edits.Add((childSpan.Start - span.OpenEnd, childSpan.End - childSpan.Start, ""));
                    else if (childSpan.Empty)
                    {
                        var opening = original[childSpan.Start..childSpan.OpenEnd];
                        edits.Add((childSpan.Start - span.OpenEnd, childSpan.End - childSpan.Start, opening[..opening.LastIndexOf('/')].TrimEnd() + ">" + Escape(names[childIndex]) + "</string>"));
                    }
                    else edits.Add((childSpan.OpenEnd - span.OpenEnd, childSpan.EndStart - childSpan.OpenEnd, Escape(names[childIndex])));
                }
                foreach (var edit in edits.OrderByDescending(e => e.Start)) inner = inner[..edit.Start] + edit.Value + inner[(edit.Start + edit.Length)..];
                value = inner + string.Concat(names.Skip(children.Length).Select(name => "<string>" + Escape(name) + "</string>"));
            }
            else if (values.TryGetValue(elements[i], out var replacement)) value = replacement;
            if (value == null) continue;
            if (span.Empty)
            {
                var opening = original[span.Start..span.OpenEnd];
                int slash = opening.LastIndexOf('/');
                replacements.Add((span.Start, span.End - span.Start, opening[..slash].TrimEnd() + ">" + value + "</" + elements[i].Name + ">"));
            }
            else replacements.Add((span.OpenEnd, span.EndStart - span.OpenEnd, (append?.Contains(elements[i]) == true ? original[span.OpenEnd..span.EndStart] : "") + value));
        }
        foreach (var replacement in replacements.OrderByDescending(r => r.Start))
            original = original[..replacement.Start] + replacement.Value + original[(replacement.Start + replacement.Length)..];
        return original;
    }
    public static string Escape(string value) => SecurityElement.Escape(value) ?? "";
    private sealed class ElementSpan(int start, int openEnd, bool empty)
    {
        public int Start { get; } = start;
        public int OpenEnd { get; } = openEnd;
        public bool Empty { get; } = empty;
        public int EndStart { get; set; }
        public int End { get; set; }
    }
}
