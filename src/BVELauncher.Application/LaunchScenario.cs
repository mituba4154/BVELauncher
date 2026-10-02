using BVELauncher.Core;

namespace BVELauncher.Application;

public sealed class LaunchScenario(IAssetReader reader, IProcessRunner runner, ILibraryStore store, IEnumerable<IBveRuntimeAdapter> adapters, ILocalLog? log = null)
{
    public async Task<LaunchPlan> PrepareAsync(ScenarioEntry scenario, IEnumerable<BveRuntime> runtimes, AppSettings settings,
        string generatedDirectory, VehicleOverride? vehicle = null, Guid? runtimeOverride = null, CancellationToken cancellationToken = default, bool useDefaultVehicle = true)
    {
        if (!File.Exists(scenario.Path)) throw new FileNotFoundException("ScenarioMissing", scenario.Path);
        var available = runtimes.Where(r => r.IsValid && File.Exists(r.ExecutablePath)).ToArray();
        var chosenId = runtimeOverride ?? scenario.RuntimeId;
        var runtime = chosenId != null ? available.FirstOrDefault(r => r.Id == chosenId) :
            available.FirstOrDefault(r => r.Kind == scenario.Compatibility.RecommendedRuntime) ?? available.FirstOrDefault(r => r.Id == settings.DefaultRuntimeId) ?? available.OrderByDescending(r => r.Kind).FirstOrDefault();
        if (runtime == null) throw new FileNotFoundException("RuntimeMissing");
        if (vehicle == null && useDefaultVehicle && scenario.DefaultVehiclePath != null) vehicle = new(scenario.DefaultVehiclePath, scenario.VehicleTitle);
        var generated = vehicle == null ? null : await reader.MaterializeAsync(scenario.Path, vehicle, generatedDirectory, cancellationToken).ConfigureAwait(false);
        return adapters.Single(a => a.Kind == runtime.Kind).CreatePlan(scenario, runtime, generated ?? scenario.Path, settings.LaunchBehavior, generated);
    }
    public async Task<RunningLaunch> ExecuteAsync(LaunchPlan plan, CancellationToken cancellationToken = default)
    {
        var launch = await runner.StartAsync(plan, cancellationToken).ConfigureAwait(false);
        // BVE起動後の履歴保存失敗で、起動失敗と誤表示したり終了追跡を失ったりしない。
        try { await store.RecordLaunchAsync(plan.ScenarioId, launch.StartedUtc, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { log?.Write("Warning", "Launch history could not be saved.", ex); }
        return launch;
    }
    public Task WaitAsync(RunningLaunch launch, CancellationToken cancellationToken = default) => runner.WaitForExitAsync(launch.ProcessId, cancellationToken);
}
