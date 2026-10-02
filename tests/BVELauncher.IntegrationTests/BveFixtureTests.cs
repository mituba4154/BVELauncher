using BVELauncher.Bve;
using BVELauncher.Core;

namespace BVELauncher.IntegrationTests;

public sealed class BveFixtureTests
{
    [Theory]
    [InlineData("Preferences.xml", RuntimeKind.Bve5)]
    [InlineData("BveTs6.Preferences.xml", RuntimeKind.Bve6)]
    public async Task ActualPreferencesExposeAllMappedNodesAndRoundTripChanges(string file, RuntimeKind runtime)
    {
        using var env = new TestEnvironment();
        var path = Path.Combine(env.Content, file); File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", file), path);
        BveSettingsAdapter adapter = runtime == RuntimeKind.Bve5 ? new Bve5SettingsAdapter(env.Backups) : new Bve6SettingsAdapter(env.Backups);
        var snapshot = await adapter.ReadAsync(path);
        Assert.Equal(117, snapshot.Definitions.Count);
        Assert.Equal(57, snapshot.Definitions.Count(d => d.Category == "Keyboard"));
        Assert.True(snapshot.Values["ScenarioDirectory"] is "" or "C:\\BVE\\Scenarios");
        var original = await File.ReadAllTextAsync(path);
        await adapter.SaveAsync(snapshot, new Dictionary<string, string> { ["HeadMotion"] = "42", ["FontName"] = "A&B", ["InputPlugins"] = "first|second", ["ScenarioDirectory"] = "C:\\BVE\\Scenarios" });
        var saved = await adapter.ReadAsync(path);
        Assert.Equal("42", saved.Values["HeadMotion"]); Assert.Equal("A&B", saved.Values["FontName"]); Assert.Equal("first|second", saved.Values["InputPlugins"]);
        Assert.Contains("<BackDrawDistance>50</BackDrawDistance>", await File.ReadAllTextAsync(path));
        await env.Backups.RestoreAsync(Assert.Single(await env.Backups.ListAsync()));
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }
}
