using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Infrastructure;

public sealed class ProcessRunner(AppPaths paths, ILocalLog log) : IProcessRunner
{
    private readonly ConcurrentDictionary<int, Process> processes = new();
    public async Task<RunningLaunch> StartAsync(LaunchPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? marker = null;
        Process? started = null;
        try
        {
            if (plan.GeneratedPath != null)
            {
                marker = Path.Combine(paths.Generated, Guid.NewGuid().ToString("N") + ".json");
                await AtomicFile.WriteAsync(marker, JsonSerializer.SerializeToUtf8Bytes(new GeneratedLease(plan.GeneratedPath, 0, null)), cancellationToken).ConfigureAwait(false);
            }
            var process = Process.Start(CreateStartInfo(plan)) ?? throw new IOException("Process creation failed.");
            started = process;
            processes[process.Id] = process;
            if (marker != null)
            {
                try { await AtomicFile.WriteAsync(marker, JsonSerializer.SerializeToUtf8Bytes(new GeneratedLease(plan.GeneratedPath!, process.Id, process.StartTime.ToUniversalTime())), CancellationToken.None).ConfigureAwait(false); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
                { log.Write("Warning", "Generated lease update deferred until runtime exits.", ex); }
            }
            return new(process.Id, plan, DateTime.UtcNow);
        }
        catch
        {
            if (started == null && plan.GeneratedPath != null) CleanupGenerated(plan.GeneratedPath, marker);
            throw;
        }
    }
    public static ProcessStartInfo CreateStartInfo(LaunchPlan plan)
    {
        var info = new ProcessStartInfo(plan.Runtime.ExecutablePath) { WorkingDirectory = plan.WorkingDirectory, UseShellExecute = false };
        info.ArgumentList.Add(plan.ScenarioPath);
        return info;
    }
    public async Task WaitForExitAsync(int processId, CancellationToken cancellationToken)
    {
        if (!processes.TryGetValue(processId, out var process)) return;
        try { await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false); }
        finally
        {
            if (process.HasExited)
            {
                processes.TryRemove(processId, out _); process.Dispose();
                await CleanupStaleGeneratedAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
    public bool IsRuntimeRunning(BveRuntime runtime)
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(runtime.ExecutablePath)))
        {
            using (process)
            {
                try { if (process.MainModule?.FileName.Equals(runtime.ExecutablePath, StringComparison.OrdinalIgnoreCase) == true) return true; }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { log.Write("Warning", "Runtime process cannot be inspected.", ex); return true; }
            }
        }
        return false;
    }
    public async Task CleanupStaleGeneratedAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(paths.Generated)) return;
        foreach (var marker in Directory.EnumerateFiles(paths.Generated, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var lease = JsonSerializer.Deserialize<GeneratedLease>(await File.ReadAllTextAsync(marker, cancellationToken).ConfigureAwait(false));
                if (lease == null) continue;
                // 起動直後のクラッシュでPIDを記録できなかった場合は、BVE終了まで保守的に保持する。
                if (lease.ProcessId == 0 && Process.GetProcessesByName("BveTs").Any(p => { using (p) return !p.HasExited; })) continue;
                // PIDだけで別プロセスの所有権を誤認しない。
                if (lease.ProcessId != 0)
                {
                    try
                    {
                        using var process = Process.GetProcessById(lease.ProcessId);
                        if (!process.HasExited && process.StartTime.ToUniversalTime() == lease.StartedUtc) continue;
                    }
                    catch (ArgumentException) { /* 終了済みPID。 */ }
                }
                CleanupGenerated(lease.Path, marker);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            { log.Write("Warning", "Generated scenario cleanup deferred.", ex); }
        }
    }
    private static void CleanupGenerated(string path, string? marker)
    {
        if (!Path.GetFileName(path).StartsWith(".BVELauncher-", StringComparison.Ordinal)) throw new InvalidDataException("Invalid generated lease.");
        if (File.Exists(path)) File.Delete(path);
        if (marker != null && File.Exists(marker)) File.Delete(marker);
    }
    private sealed record GeneratedLease(string Path, int ProcessId, DateTime? StartedUtc);
}
