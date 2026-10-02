// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using BVELauncher.Application;
using BVELauncher.Core;

namespace BVELauncher.Tests;

public sealed class LibraryQueryTests
{
    private static ScenarioEntry Scenario(string title, RuntimeKind kind = RuntimeKind.Unknown, bool favorite = false, bool hidden = false)
        => new(Guid.NewGuid(), title + ".txt", title, "テスト路線", "車両", "作者", "コメント", null, [], DateTime.UtcNow, title)
        { Favorite = favorite, Hidden = hidden, Compatibility = new([], kind) };
    [Fact]
    public void JapaneseSubstringSearchCoversUserMetadataAndHiddenState()
    {
        var items = new[] { Scenario("快速"), Scenario("普通", hidden: true), Scenario("急行") with { Author = "東京作者" } };
        Assert.Single(LibraryQuery.Apply(items, "東京", LibraryFilter.All, SortOrder.Name, CultureInfo.GetCultureInfo("ja-JP")));
        Assert.Equal(2, LibraryQuery.Apply(items, "路線", LibraryFilter.All, SortOrder.Route, CultureInfo.GetCultureInfo("ja-JP")).Count);
    }
    [Fact]
    public void UnknownCompatibilityRemainsVisibleInRuntimeFilters()
    {
        var items = new[] { Scenario("unknown"), Scenario("five", RuntimeKind.Bve5), Scenario("six", RuntimeKind.Bve6) };
        Assert.Equal(2, LibraryQuery.Apply(items, "", LibraryFilter.Bve6, SortOrder.Name, CultureInfo.InvariantCulture).Count);
        Assert.Equal(2, LibraryQuery.Apply(items, "", LibraryFilter.Bve5, SortOrder.Name, CultureInfo.InvariantCulture).Count);
    }
    [Fact]
    public void FavoritesAndRecentOrderingAreIndependentOfWarnings()
    {
        var old = Scenario("old", favorite: true) with { LastLaunchUtc = DateTime.UtcNow.AddDays(-1) };
        var recent = Scenario("recent", favorite: true) with { LastLaunchUtc = DateTime.UtcNow };
        Assert.Equal(recent.Id, LibraryQuery.Apply([old, recent, Scenario("other")], "", LibraryFilter.Favorites, SortOrder.Recent, CultureInfo.InvariantCulture)[0].Id);
    }
    [Fact]
    public void SourceFilesAreNeverModifiedByPresentationQuery()
    {
        var entry = Scenario("original") with { Alias = "alias", CustomGroup = "group" };
        var result = Assert.Single(LibraryQuery.Apply([entry], "alias", LibraryFilter.All, SortOrder.Name, CultureInfo.InvariantCulture));
        Assert.Equal("original", result.Title); Assert.Equal("alias", result.DisplayTitle); Assert.Equal("group", result.Group);
    }
}
