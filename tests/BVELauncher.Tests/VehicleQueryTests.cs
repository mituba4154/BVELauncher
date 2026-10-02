// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Tests;

public sealed class VehicleQueryTests
{
    private static VehicleEntry Vehicle(string folder, string file, params string[] names) => new(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), folder, "Vehicle", file), "Vehicle", names, [], DateTime.UtcNow, "hash");
    [Fact]
    public void GenericFoldersHaveIdentifiableNamesAndAliasesTakePrecedence()
    {
        var vehicle = Vehicle("E233", "vehicle15.txt");
        Assert.Contains("E233", vehicle.DisplayName); Assert.Contains("vehicle15", vehicle.DisplayName);
        Assert.Contains("vehicle15.txt", vehicle.SourceLabel);
        Assert.Equal("Custom", (vehicle with { Alias = "Custom", ScenarioTitles = ["E233系"] }).DisplayName);
    }
    [Fact]
    public void SearchIncludesAllScenarioNamesAndPathWithWidthNormalization()
    {
        var vehicle = Vehicle("E233", "vehicle15.txt", "E233系15両", "別の表示名");
        Assert.Single(VehicleQuery.Apply([vehicle], "ｅ２３３　ＶＥＨＩＣＬＥ１５", CultureInfo.GetCultureInfo("ja-JP")));
        Assert.Single(VehicleQuery.Apply([vehicle], "別の表示名", CultureInfo.GetCultureInfo("ja-JP")));
    }
    [Fact]
    public void AllTermsMustMatchAndAliasDoesNotHideOtherNames()
    {
        var vehicle = Vehicle("E233", "vehicle15.txt", "原名") with { Alias = "通勤車両" };
        Assert.Single(VehicleQuery.Apply([vehicle], "通勤 原名", CultureInfo.GetCultureInfo("ja-JP")));
        Assert.Empty(VehicleQuery.Apply([vehicle], "通勤 存在しない", CultureInfo.GetCultureInfo("ja-JP")));
    }
}
