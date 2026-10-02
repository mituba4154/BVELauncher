using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace BVELauncher.App.Services;

public static class UiFactory
{
    private static readonly SemaphoreSlim dialogGate = new(1, 1);
    public static async Task<ContentDialogResult> ShowQueuedAsync(this ContentDialog dialog)
    {
        // 閉じるアニメーション中の連続操作でも、二重ShowAsyncによる例外を起こさない。
        await dialogGate.WaitAsync();
        try { return await dialog.ShowAsync(); }
        finally { dialogGate.Release(); }
    }
    public static T Identify<T>(T control, string id, string label) where T : FrameworkElement
    { AutomationProperties.SetAutomationId(control, id); AutomationProperties.SetName(control, label); return control; }
    public static StackPanel Form() => new() { Spacing = 16, Padding = new Thickness(16), MinWidth = 460 };
    public static TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    public static Grid Row(string label, FrameworkElement control)
    {
        var grid = new Grid { ColumnSpacing = 16 }; grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(280) });
        var text = Text(label); text.VerticalAlignment = VerticalAlignment.Center; grid.Children.Add(text); Grid.SetColumn(control, 1); grid.Children.Add(control); return grid;
    }
    public static ComboBox Combo(IEnumerable<string> items, int index, string id, string label)
    { var combo = Identify(new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch }, id, label); foreach (var item in items) combo.Items.Add(item); combo.SelectedIndex = index; return combo; }
    public static Button Button(string text, string id, Func<Task> click)
    {
        var button = Identify(new Button { Content = text }, id, text);
        button.Click += async (_, _) => await click(); return button;
    }
    public static ContentDialog Dialog(XamlRoot root, string title, string close, object content)
        => new() { XamlRoot = root, Title = title, CloseButtonText = close, Content = content, DefaultButton = ContentDialogButton.Close };
}
