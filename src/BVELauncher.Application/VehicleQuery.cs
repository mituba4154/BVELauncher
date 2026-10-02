// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using BVELauncher.Core;

namespace BVELauncher.Application;

public static class VehicleQuery
{
    public static IReadOnlyList<VehicleEntry> Apply(IEnumerable<VehicleEntry> vehicles, string search, CultureInfo culture)
    {
        var terms = search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return vehicles.Where(v => terms.All(term => new[] { v.DisplayName, v.Name, v.Path, v.Ats32 ?? "", v.Ats64 ?? "" }.Concat(v.ScenarioTitles)
            .Any(value => culture.CompareInfo.IndexOf(value, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth | CompareOptions.IgnoreKanaType) >= 0)))
            .OrderBy(v => v.DisplayName, StringComparer.Create(culture, true)).ThenBy(v => v.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
