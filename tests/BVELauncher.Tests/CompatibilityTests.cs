// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Bve;
using BVELauncher.Core;
namespace BVELauncher.Tests;
public sealed class CompatibilityTests
{
    [Theory]
    [InlineData(true, RuntimeKind.Unknown)]
    [InlineData(false, RuntimeKind.Bve5)]
    public async Task WeightedVehicleCandidatesRequireConsistentArchitecture(bool mixed, RuntimeKind expected)
    {
        var references = new[] { new AssetReference("scenario.txt", "Vehicle", "a.txt", "a.txt", AssetKind.Vehicle, true), new AssetReference("scenario.txt", "Vehicle", "b.txt", "b.txt", AssetKind.Vehicle, true) };
        var scenario = new ScenarioEntry(Guid.NewGuid(), "scenario.txt", "Test", "", "", "", "", null, references, DateTime.UtcNow, "");
        var a = new VehicleEntry(Guid.NewGuid(), "a.txt", "A", [], [new("a.txt", "Ats32", "32.dll", "32.dll", AssetKind.Dll, false)], DateTime.UtcNow, "");
        var b = new VehicleEntry(Guid.NewGuid(), "b.txt", "B", [], [new("b.txt", mixed ? "Ats64" : "Ats32", "plugin.dll", "plugin.dll", AssetKind.Dll, false)], DateTime.UtcNow, "");
        var result = await new CompatibilityEngine().DiagnoseAsync(scenario, new([scenario], [a,b], [], references, []), null);
        Assert.Equal(expected, result.RecommendedRuntime); Assert.True(result.HasWarnings);
    }
}
