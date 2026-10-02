using System.Globalization;
using BVELauncher.Application;
using BVELauncher.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using BVELauncher.Bve;
using Microsoft.Windows.Storage.Pickers;

namespace BVELauncher.App.Services;

public sealed class BveSettingsEditor(LauncherSession session, LocalizationService l, BveRuntime runtime) : StackPanel
{
    private BveSettingsSnapshot? snapshot;
    private IBveSettingsAdapter? adapter;
    private readonly Dictionary<string, Func<string>> values = [];
    private readonly Dictionary<string, string> pending = [];
    private string category = "Graphics";
    private IReadOnlyList<InputDeviceFile> inputDevices = [];
    public async Task LoadAsync()
    {
        adapter = App.Services.GetServices<IBveSettingsAdapter>().Single(a => a.Kind == runtime.Kind);
        snapshot = await adapter.ReadAsync(runtime.PreferencesPath!);
        try { inputDevices = await Task.Run(() => InputDeviceCatalog.Read(runtime.InstallDirectory)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { await session.ReportErrorAsync("Input device metadata read", ex); inputDevices = []; }
        Build();
    }
    private void Build()
    {
        if (snapshot == null) return;
        foreach (var pair in values) pending[pair.Key] = pair.Value(); values.Clear(); Children.Clear(); Spacing = 16;
        Children.Add(UiFactory.Text(runtime.Kind + " · " + l["BveSettings"]));
        var categories = snapshot.Definitions.Select(d => d.Category).Distinct().ToArray();
        var picker = UiFactory.Combo(categories.Select(c => l[c]).ToArray(), Math.Max(0, Array.IndexOf(categories, category)), "BveCategory", l["BveSettings"]);
        picker.SelectionChanged += (_, _) => { if (picker.SelectedIndex >= 0) { category = categories[picker.SelectedIndex]; Build(); } }; Children.Add(picker);
        if (category == "Assistants") Children.Add(UiFactory.Button(l["ResetAssistantPositions"], "BveResetAssistantPositions", () =>
        {
            foreach (var pair in values) pending[pair.Key] = pair.Value(); values.Clear();
            foreach (var definition in snapshot.Definitions.Where(d => d.Property.StartsWith("AssistAuto_", StringComparison.Ordinal)))
            {
                var visible = "Assist_" + definition.Property["AssistAuto_".Length..];
                if ((pending.GetValueOrDefault(visible) ?? snapshot.Values[visible]) == "true") pending[definition.Property] = "true";
            }
            Build(); return Task.CompletedTask;
        }));
        if (category == "Keyboard") Children.Add(UiFactory.Button(l["KeyboardPreset"], "BveKeyboardPreset", async () =>
        {
            try
            {
                var filePicker = new FileOpenPicker(App.Window!.AppWindow.Id); filePicker.FileTypeFilter.Add(".txt");
                var file = await filePicker.PickSingleFileAsync(); if (file == null) return;
                var preset = KeyboardPresetReader.Read(await File.ReadAllTextAsync(file.Path));
                foreach (var pair in values) pending[pair.Key] = pair.Value(); values.Clear();
                foreach (var pair in preset) pending[pair.Key] = pair.Value;
                Build();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { await UiFactory.Dialog(XamlRoot, l["Error"], l["Close"], UiFactory.Text(l[ex.Message])).ShowQueuedAsync(); }
        }));
        foreach (var definition in snapshot.Definitions.Where(d => d.Category == category))
        {
            var label = l[definition.ResourceKey]; var current = pending.GetValueOrDefault(definition.Property) ?? snapshot.Values[definition.Property];
            Microsoft.UI.Xaml.FrameworkElement control;
            if (definition.Property == "InputPlugins")
            {
                var enabled = current.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var devices = inputDevices.ToList();
                foreach (var key in enabled.Where(key => !devices.Any(d => d.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))) devices.Add(new(key, key + " · " + l["NoExtension"], "", ""));
                var options = new Dictionary<string, CheckBox>(); var panel = new StackPanel { Spacing = 8 };
                foreach (var device in devices)
                {
                    var box = UiFactory.Identify(new CheckBox { Content = device.Name + (device.Version.Length == 0 ? "" : " · " + device.Version), IsChecked = enabled.Contains(device.Key) }, "BveInputDevice" + device.Key, device.Name);
                    ToolTipService.SetToolTip(box, device.Path); options[device.Key] = box; panel.Children.Add(box);
                }
                if (devices.Count == 0) panel.Children.Add(UiFactory.Text(l["NoExtension"]));
                values[definition.Property] = () => string.Join('|', options.Where(p => p.Value.IsChecked == true).Select(p => p.Key)); control = panel;
            }
            else if (definition.Type == SettingType.Boolean)
            {
                var toggle = UiFactory.Identify(new ToggleSwitch { IsOn = current == "true" }, "Bve" + definition.Property, label); values[definition.Property] = () => toggle.IsOn ? "true" : "false"; control = toggle;
            }
            else if (definition.Choices != null)
            {
                var choices = definition.Choices.ToList(); if (!choices.Contains(current)) choices.Insert(0, current);
                var combo = UiFactory.Combo(choices.ToArray(), choices.IndexOf(current), "Bve" + definition.Property, label); values[definition.Property] = () => choices[combo.SelectedIndex]; control = combo;
            }
            else if (definition.Type is SettingType.Integer or SettingType.Key)
            {
                var number = UiFactory.Identify(new NumberBox { Value = double.Parse(current, CultureInfo.InvariantCulture), Minimum = definition.Minimum ?? int.MinValue, Maximum = definition.Maximum ?? int.MaxValue, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact }, "Bve" + definition.Property, label);
                NumberBoxAutomation.Attach(number, "Bve" + definition.Property, label);
                values[definition.Property] = () => NumberBoxAutomation.ReadInteger(number); control = number;
                if (definition.Type == SettingType.Key)
                {
                    var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; number.Width = 140; panel.Children.Add(number);
                    var keyButton = UiFactory.Identify(new Button { Content = l["SetKey"] }, "Capture" + definition.Property, l["SetKey"]);
                    keyButton.KeyDown += (_, e) => { if (e.Key != VirtualKey.Tab) { number.Value = (int)e.Key; e.Handled = true; } }; panel.Children.Add(keyButton); control = panel;
                }
            }
            else
            {
                var text = UiFactory.Identify(new TextBox { Text = current }, "Bve" + definition.Property, label); values[definition.Property] = () => text.Text; control = text;
            }
            Children.Add(UiFactory.Row(label, control));
        }
    }
    public async Task SaveAsync()
    {
        if (snapshot == null || adapter == null) return;
        if (session.Settings.Runtimes.Where(r => r.Id == runtime.Id || string.Equals(r.PreferencesPath, runtime.PreferencesPath, StringComparison.OrdinalIgnoreCase)).Any(session.Runner.IsRuntimeRunning))
            throw new InvalidOperationException(l["RuntimeBusy"]);
        foreach (var pair in values) pending[pair.Key] = pair.Value();
        var changes = pending.Where(p => snapshot.Values[p.Key] != p.Value).ToDictionary(p => p.Key, p => p.Value);
        if (changes.Count > 0) await adapter.SaveAsync(snapshot, changes);
        snapshot = await adapter.ReadAsync(runtime.PreferencesPath!); pending.Clear(); values.Clear(); Build();
    }
}
