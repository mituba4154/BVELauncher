// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Compression;
using SharpCompress.Archives;

namespace BVELauncher.Infrastructure;

public sealed record ImportLimits(int MaxFiles = 50000, long MaxBytes = 4L * 1024 * 1024 * 1024,
    long MaxFileBytes = 1024L * 1024 * 1024, int MaxExpansionRatio = 1000);
public sealed class ArchiveStager(ImportLimits limits)
{
    public async Task StageAsync(string source, string destination, CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0; int count = 0;
        if (Directory.Exists(source))
        {
            PathSafety.EnsureNoLinks(source);
            foreach (var path in Directory.EnumerateFileSystemEntries(source, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested(); PathSafety.EnsureNoLinks(path);
                var relative = Path.GetRelativePath(source, path);
                var target = PathSafety.ResolveInside(destination, relative);
                if (Directory.Exists(path)) { RegisterDirectory(relative); continue; }
                await using var input = File.OpenRead(path);
                await CopyAsync(relative, input, input.Length, input.Length).ConfigureAwait(false);
            }
        }
        else if (Path.GetExtension(source).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ZipFile.OpenRead(source);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int unixType = (entry.ExternalAttributes >> 16) & 0xf000;
                if (unixType is 0xa000 or 0x6000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("ArchiveLink");
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    var directory = entry.FullName.TrimEnd('/', '\\');
                    if (directory.Length > 0) RegisterDirectory(directory);
                    continue;
                }
                await using var input = entry.Open();
                await CopyAsync(entry.FullName, input, entry.Length, entry.CompressedLength).ConfigureAwait(false);
            }
        }
        else if (Path.GetExtension(source).Equals(".7z", StringComparison.OrdinalIgnoreCase))
        {
            using var archive = ArchiveFactory.OpenArchive(source);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(entry.LinkTarget)) throw new InvalidDataException("ArchiveLink");
                if (entry.IsDirectory) { RegisterDirectory(entry.Key!.TrimEnd('/', '\\')); continue; }
                await using var input = entry.OpenEntryStream();
                await CopyAsync(entry.Key!, input, entry.Size, entry.CompressedSize).ConfigureAwait(false);
            }
        }
        else
        {
            PathSafety.EnsureNoLinks(source);
            await using var input = File.OpenRead(source);
            await CopyAsync(Path.GetFileName(source), input, input.Length, input.Length).ConfigureAwait(false);
        }
        void RegisterDirectory(string relative)
        {
            var target = PathSafety.ResolveInside(destination, relative);
            if (!seen.Add(target)) throw new InvalidDataException("ArchiveDuplicatePath");
            if (++count > limits.MaxFiles) throw new InvalidDataException("ArchiveLimit");
            Directory.CreateDirectory(target);
        }
        async Task CopyAsync(string relative, Stream input, long size, long compressed)
        {
            var target = PathSafety.ResolveInside(destination, relative);
            if (!seen.Add(target)) throw new InvalidDataException("ArchiveDuplicatePath");
            if (++count > limits.MaxFiles || size > limits.MaxFileBytes || size < 0 || size > limits.MaxBytes - total ||
                size > 1024 * 1024 && compressed > 0 && (double)size / compressed > limits.MaxExpansionRatio)
                throw new InvalidDataException("ArchiveLimit");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
            var buffer = new byte[65536]; long actual = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                actual += read; total += read;
                if (actual > limits.MaxFileBytes || total > limits.MaxBytes || actual > size) throw new InvalidDataException("ArchiveLimit");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            if (actual != size) throw new InvalidDataException("ArchiveSizeMismatch");
        }
    }
}
