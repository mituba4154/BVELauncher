// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Bve;

public sealed class Bve5RuntimeAdapter : IBveRuntimeAdapter
{
    public RuntimeKind Kind => RuntimeKind.Bve5;
    public LaunchPlan CreatePlan(ScenarioEntry scenario, BveRuntime runtime, string path, LaunchBehavior behavior, string? generatedPath)
        => new(scenario.Id, runtime, Path.GetFullPath(path), runtime.InstallDirectory, behavior, generatedPath);
}
public sealed class Bve6RuntimeAdapter : IBveRuntimeAdapter
{
    public RuntimeKind Kind => RuntimeKind.Bve6;
    public LaunchPlan CreatePlan(ScenarioEntry scenario, BveRuntime runtime, string path, LaunchBehavior behavior, string? generatedPath)
        => new(scenario.Id, runtime, Path.GetFullPath(path), runtime.InstallDirectory, behavior, generatedPath);
}
