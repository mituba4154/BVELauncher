// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Infrastructure;

public sealed class SettingsStore(AppPaths paths, ILocalLog log) : ISettingsStore
{
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.Settings)) return new();
        try
        {
            await using var stream = File.OpenRead(paths.Settings);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? new();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            log.Write("Warning", "Settings read failed; original retained.", ex);
            File.Copy(paths.Settings, paths.Settings + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), false);
            return new();
        }
    }
    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        => AtomicFile.WriteAsync(paths.Settings, JsonSerializer.SerializeToUtf8Bytes(settings, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
}
