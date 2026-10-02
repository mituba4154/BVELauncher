using BVELauncher.App.ViewModels;
using BVELauncher.Core;

namespace BVELauncher.Tests;

public sealed class ViewModelTests
{
    [Fact]
    public void UpdatedMetadataRaisesNotificationsAndRetainsRunningState()
    {
        var entry = new ScenarioEntry(Guid.NewGuid(), "scenario.txt", "Original", "Route", "Vehicle", "", "", null, [], DateTime.UtcNow, "hash");
        var card = new ScenarioCardViewModel(entry) { IsRunning = true };
        var changed = new List<string?>(); card.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        card.Update(entry with { Alias = "Custom", Favorite = true });
        Assert.Equal("Custom", card.Title); Assert.Equal("★", card.Favorite); Assert.True(card.IsRunning);
        Assert.Contains(nameof(card.Title), changed); Assert.Contains(nameof(card.Favorite), changed);
    }
    [Fact]
    public void WarningIsAdvisoryAndDoesNotRemoveScenario()
    {
        var entry = new ScenarioEntry(Guid.NewGuid(), "scenario.txt", "Title", "", "", "", "", null, [], DateTime.UtcNow, "hash")
        { Compatibility = new([new(Severity.Error, "ReferenceMissing", "scenario.txt")]) };
        var card = new ScenarioCardViewModel(entry);
        Assert.Equal("⚠", card.Warning); Assert.Equal(entry.Id, card.Id); Assert.Equal("Title", card.Title);
    }
}
