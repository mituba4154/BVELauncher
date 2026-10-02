using BVELauncher.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BVELauncher.App.ViewModels;

public partial class ScenarioCardViewModel(ScenarioEntry entry) : ObservableObject
{
    public ScenarioEntry Entry { get; private set; } = entry;
    public Guid Id => Entry.Id;
    public string Title => Entry.DisplayTitle;
    public string VehicleTitle => Entry.VehicleTitle;
    public string RouteTitle => Entry.RouteTitle;
    public string? ImagePath => Entry.ImagePath;
    public string Warning => Entry.Compatibility.HasWarnings ? "⚠" : "";
    public string Favorite => Entry.Favorite ? "★" : "";
    [ObservableProperty] public partial bool IsRunning { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial string? ThumbnailPath { get; set; }
    public void Update(ScenarioEntry entry)
    {
        Entry = entry; OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(VehicleTitle)); OnPropertyChanged(nameof(RouteTitle));
        OnPropertyChanged(nameof(ImagePath)); OnPropertyChanged(nameof(Warning)); OnPropertyChanged(nameof(Favorite));
    }
}
public sealed class ScenarioGroup(string title, IEnumerable<ScenarioCardViewModel> items) : ObservableObject
{
    public string Title { get; } = title;
    public System.Collections.ObjectModel.ObservableCollection<ScenarioCardViewModel> Items { get; } = new(items);
}
