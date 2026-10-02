using BVELauncher.App.Services;
using BVELauncher.App.ViewModels;
using BVELauncher.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;

namespace BVELauncher.App;

public sealed class ScenarioSettingsWindow : Window
{
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Completion => completion.Task;

    public ScenarioSettingsWindow(LauncherSession session, LocalizationService l, ScenarioCardViewModel card)
    {
        Title = l["ScenarioSettings"] + " · " + App.Services.GetRequiredService<Branding>().DisplayName;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        SystemBackdrop = new MicaBackdrop();
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new(Math.Min(720, area.Width), Math.Min(820, area.Height)));
        var layout = new Grid { Padding = new(24), RowSpacing = 12 };
        layout.RequestedTheme = session.Settings.Theme switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
        foreach (var height in new[] { new GridLength(140), GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
            layout.RowDefinitions.Add(new() { Height = height });
        var metadata = new StackPanel { Spacing = 8 };
        foreach (var (key, value) in new[] { ("Title", card.Entry.Title), ("Route", card.Entry.RouteTitle), ("Vehicle", card.Entry.VehicleTitle), ("Author", card.Entry.Author), ("Comment", card.Entry.Comment), ("Path", card.Entry.Path) })
            metadata.Children.Add(UiFactory.Text(l[key] + ": " + value));
        Add(new ScrollViewer { Content = metadata, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 0);
        var alias = UiFactory.Identify(new TextBox { Text = card.Entry.Alias ?? "", Header = l["Alias"] }, "ScenarioAlias", l["Alias"]);
        var group = UiFactory.Identify(new TextBox { Text = card.Entry.CustomGroup ?? "", Header = l["Group"] }, "ScenarioGroup", l["Group"]);
        var names = new Grid { ColumnSpacing = 12 };
        names.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); names.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        names.Children.Add(alias); Grid.SetColumn(group, 1); names.Children.Add(group); Add(names, 1);
        var runtimes = session.Settings.Runtimes.ToArray();
        var runtime = UiFactory.Combo(new[] { l["Auto"] }.Concat(runtimes.Select(r => (r.Kind == RuntimeKind.Bve6 ? l["Bve6"] : l["Bve5"]) + " " + r.Version + " · " + Path.GetFileName(r.InstallDirectory))),
            card.Entry.RuntimeId == null ? 0 : Array.FindIndex(runtimes, r => r.Id == card.Entry.RuntimeId) + 1, "ScenarioRuntime", l["Runtime"]);
        runtime.Header = l["Runtime"]; Add(runtime, 2);
        var search = UiFactory.Identify(new TextBox { Header = l["DefaultVehicle"], PlaceholderText = l["Search"] }, "ScenarioVehicleSearch", l["DefaultVehicle"]); Add(search, 3);
        var candidates = session.Library.Vehicles.ToList();
        if (card.Entry.DefaultVehiclePath is string savedPath && !candidates.Any(v => v.Path.Equals(savedPath, StringComparison.OrdinalIgnoreCase)))
            candidates.Add(new(Guid.NewGuid(), savedPath, Path.GetFileNameWithoutExtension(savedPath), [], [], DateTime.MinValue, ""));
        var vehicles = new[] { new VehicleEntry(Guid.Empty, "", l["StandardVehicle"], [], [], DateTime.MinValue, "") }.Concat(candidates.OrderBy(v => v.DisplayName, StringComparer.CurrentCultureIgnoreCase)).ToArray();
        // 候補リストをPopup内の自動サイズ測定から切り離し、Gridの有限viewportで仮想化する。
        var vehicle = UiFactory.Identify(new ListView { ItemsSource = vehicles, DisplayMemberPath = "DisplayName", SelectionMode = ListViewSelectionMode.Single }, "ScenarioVehicle", l["DefaultVehicle"]);
        VehicleEntry chosen = vehicles.FirstOrDefault(v => v.Path.Equals(card.Entry.DefaultVehiclePath, StringComparison.OrdinalIgnoreCase)) ?? vehicles[0];
        vehicle.SelectedItem = chosen;
        vehicle.SelectionChanged += (_, _) => { if (vehicle.SelectedItem is VehicleEntry selected) chosen = selected; };
        search.TextChanged += (_, _) =>
        {
            vehicle.ItemsSource = vehicles.Where(v => v.Id == Guid.Empty || v.DisplayName.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase) || v.Path.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).ToArray();
            vehicle.SelectedItem = chosen;
        };
        Add(vehicle, 4);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right };
        var status = UiFactory.Identify(UiFactory.Text(""), "ScenarioSaveStatus", l["Save"]);
        footer.Children.Add(status);
        footer.Children.Add(UiFactory.Button(l["Save"], "PrimaryButton", async () =>
        {
            try
            {
                await session.UpdateScenarioAsync(card.Entry with { Alias = string.IsNullOrWhiteSpace(alias.Text) ? null : alias.Text, CustomGroup = string.IsNullOrWhiteSpace(group.Text) ? null : group.Text,
                    RuntimeId = runtime.SelectedIndex <= 0 ? null : runtimes[runtime.SelectedIndex - 1].Id, DefaultVehiclePath = chosen.Id == Guid.Empty ? null : chosen.Path });
                Close();
            }
            catch (Exception ex) { await session.ReportErrorAsync("Scenario settings", ex); status.Text = l.Format("OperationFailed", ex.Message); }
        }));
        footer.Children.Add(UiFactory.Button(l["Cancel"], "CloseButton", () => { Close(); return Task.CompletedTask; })); Add(footer, 5);
        Content = layout;
        Closed += (_, _) => completion.TrySetResult();
        void Add(FrameworkElement element, int row) { Grid.SetRow(element, row); layout.Children.Add(element); }
    }
}
