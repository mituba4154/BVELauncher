// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using BVELauncher.Core;

namespace BVELauncher.Application;

public enum LibraryFilter { All, Bve6, Bve5, Favorites, Warnings }
public static class LibraryQuery
{
    public static IReadOnlyList<ScenarioEntry> Apply(IEnumerable<ScenarioEntry> scenarios, string search, LibraryFilter filter, SortOrder sort, CultureInfo culture)
    {
        var visible = scenarios.Where(s => !s.Hidden);
        if (!string.IsNullOrWhiteSpace(search)) visible = visible.Where(s => new[] { s.Title, s.RouteTitle, s.VehicleTitle, s.Author, s.Comment, s.Path, s.Alias ?? "" }
            .Any(t => culture.CompareInfo.IndexOf(t, search, CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth) >= 0));
        visible = filter switch
        {
            LibraryFilter.Favorites => visible.Where(s => s.Favorite),
            LibraryFilter.Warnings => visible.Where(s => s.Compatibility.HasWarnings),
            LibraryFilter.Bve5 => visible.Where(s => s.Compatibility.RecommendedRuntime is RuntimeKind.Unknown or RuntimeKind.Bve5),
            LibraryFilter.Bve6 => visible.Where(s => s.Compatibility.RecommendedRuntime is RuntimeKind.Unknown or RuntimeKind.Bve6),
            _ => visible
        };
        var comparer = StringComparer.Create(culture, true);
        return (sort switch
        {
            SortOrder.Route => visible.OrderBy(s => s.RouteTitle, comparer).ThenBy(s => s.DisplayTitle, comparer),
            SortOrder.Vehicle => visible.OrderBy(s => s.VehicleTitle, comparer).ThenBy(s => s.DisplayTitle, comparer),
            SortOrder.Recent => visible.OrderByDescending(s => s.LastLaunchUtc).ThenBy(s => s.DisplayTitle, comparer),
            SortOrder.Modified => visible.OrderByDescending(s => s.LastWriteUtc),
            _ => visible.OrderBy(s => s.DisplayTitle, comparer)
        }).ToArray();
    }
}
