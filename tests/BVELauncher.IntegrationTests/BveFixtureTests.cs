// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Bve;
using BVELauncher.Core;

namespace BVELauncher.IntegrationTests;

public sealed class BveFixtureTests
{
    [Fact]
    public async Task InputPluginListChangesPreserveUnknownChildrenAttributesAndComments()
    {
        using var env = new TestEnvironment(); var path = Path.Combine(env.Content, "preferences.xml");
        await File.WriteAllTextAsync(path, "<Preferences>\r\n<InputPlugins future=\"keep\">\r\n<!--keep--><string hint=\"keep\">old</string><Future x=\"1\">opaque</Future><string>remove</string>\r\n</InputPlugins>\r\n</Preferences>");
        var adapter = new Bve6SettingsAdapter(env.Backups); var snapshot = await adapter.ReadAsync(path);
        await adapter.SaveAsync(snapshot, new Dictionary<string, string> { ["InputPlugins"] = "new" });
        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("<InputPlugins future=\"keep\">\r\n<!--keep--><string hint=\"keep\">new</string><Future x=\"1\">opaque</Future>", saved);
        Assert.DoesNotContain("remove", saved); Assert.Equal("new", (await adapter.ReadAsync(path)).Values["InputPlugins"]);
        await adapter.SaveAsync(await adapter.ReadAsync(path), new Dictionary<string, string> { ["InputPlugins"] = "" });
        Assert.Contains("<Future x=\"1\">opaque</Future>", await File.ReadAllTextAsync(path));
    }
    [Theory]
    [InlineData("Preferences.xml", RuntimeKind.Bve5)]
    [InlineData("BveTs6.Preferences.xml", RuntimeKind.Bve6)]
    public async Task ActualPreferencesExposeAllMappedNodesAndRoundTripChanges(string file, RuntimeKind runtime)
    {
        using var env = new TestEnvironment();
        var path = Path.Combine(env.Content, file); File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", file), path);
        BveSettingsAdapter adapter = runtime == RuntimeKind.Bve5 ? new Bve5SettingsAdapter(env.Backups) : new Bve6SettingsAdapter(env.Backups);
        var snapshot = await adapter.ReadAsync(path);
        Assert.Equal(118, snapshot.Definitions.Count);
        Assert.Equal(57, snapshot.Definitions.Count(d => d.Category == "Keyboard"));
        var mappingOutput = Environment.GetEnvironmentVariable("BVELAUNCHER_MAPPING_OUTPUT");
        if (!string.IsNullOrEmpty(mappingOutput))
            await File.WriteAllTextAsync(Path.Combine(mappingOutput, runtime + ".json"), System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Assert.True(snapshot.Values["ScenarioDirectory"] is "" or "C:\\BVE\\Scenarios");
        var original = await File.ReadAllTextAsync(path);
        await adapter.SaveAsync(snapshot, new Dictionary<string, string> { ["HeadMotion"] = "42", ["FontName"] = "A&B", ["FontSize"] = "24", ["InputPlugins"] = "first|second", ["ScenarioDirectory"] = "C:\\BVE\\Scenarios" });
        var saved = await adapter.ReadAsync(path);
        Assert.Equal("42", saved.Values["HeadMotion"]); Assert.Equal("A&B", saved.Values["FontName"]); Assert.Equal("first|second", saved.Values["InputPlugins"]);
        Assert.All(saved.Values.Where(p => p.Key.StartsWith("AssistSize_", StringComparison.Ordinal) || p.Key == "TimetableSize"), p => Assert.Equal("24", p.Value));
        Assert.Contains("<BackDrawDistance>50</BackDrawDistance>", await File.ReadAllTextAsync(path));
        await env.Backups.RestoreAsync(Assert.Single(await env.Backups.ListAsync()));
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }
}
