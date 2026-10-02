namespace BVELauncher.Infrastructure;

public static class PathSafety
{
    private static readonly HashSet<string> Devices = new(StringComparer.OrdinalIgnoreCase)
    { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³" };
    public static string ResolveInside(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.StartsWith('\\') || relative.StartsWith('/') || relative.Contains(':'))
            throw new InvalidDataException("ArchiveRootedPath");
        var segments = relative.Replace('\\', '/').Split('/');
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment is "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.') || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || segment.Any(c => c < 32) || Devices.Contains(segment.Split('.')[0]))
                throw new InvalidDataException("ArchiveInvalidPath: " + relative);
        }
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string full = Path.GetFullPath(Path.Combine(normalizedRoot, Path.Combine(segments)));
        if (!full.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || full.Length > 240)
            throw new InvalidDataException("ArchivePathEscapeOrTooLong");
        EnsureNoLinks(full);
        return full;
    }
    public static void EnsureNoLinks(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("ReparsePointEscape: " + current);
            current = Path.GetDirectoryName(current);
        }
    }
    public static void DeleteOwnedDirectory(string path, string allowedRoot)
    {
        string full = Path.GetFullPath(path);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowedRoot));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Path.GetDirectoryName(full) != root)
            throw new InvalidDataException("Unsafe cleanup target.");
        EnsureNoLinks(full);
        if (Directory.Exists(full))
        {
            foreach (var item in Directory.EnumerateFileSystemEntries(full, "*", SearchOption.AllDirectories)) EnsureNoLinks(item);
            Directory.Delete(full, true);
        }
    }
}
