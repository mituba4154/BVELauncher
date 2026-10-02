// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.App.Services;
using BVELauncher.App.ViewModels;
using BVELauncher.Application;
using BVELauncher.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace BVELauncher.App;

public sealed partial class SettingsWindow : Window
{
    private readonly LauncherSession session;
    private readonly LocalizationService l;
    private readonly MainPageViewModel vm;
    private readonly List<Func<AppSettings, AppSettings>> updates = [];
    public SettingsWindow(LauncherSession session, LocalizationService localization, MainPageViewModel vm)
    {
        (this.session, l, this.vm) = (session, localization, vm); InitializeComponent();
        Title = l["Settings"] + " · " + App.Services.GetRequiredService<Branding>().DisplayName;
        AppWindow.Resize(new(1100, 850)); SaveButton.Content = l["Save"];
        foreach (var key in new[] { "General", "Appearance", "Bve", "Scenarios", "Import", "Advanced", "About" })
            SettingsNavigation.MenuItems.Add(UiFactory.Identify(new NavigationViewItem { Content = l[key], Tag = key }, "Settings" + key, l[key]));
        SettingsNavigation.SelectedItem = SettingsNavigation.MenuItems[0];
    }
    private async void Navigation_Changed(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;
        await GuardAsync(async () =>
        {
            updates.Clear(); SettingsContent.Children.Clear(); SettingsStatus.Text = ""; SaveButton.Visibility = Visibility.Visible;
            var key = (string)item.Tag;
            SettingsContent.Children.Add(new TextBlock { Text = l[key], Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["TitleTextBlockStyle"] });
            switch (key)
            {
                case "General":
                    Pick("ClickBehavior", [l["ClickLaunch"], l["ClickDetails"], l["ClickSelect"]], (int)session.Settings.ClickBehavior, (s, i) => s with { ClickBehavior = (ClickBehavior)i });
                    Pick("LaunchBehavior", [l["KeepVisible"], l["Minimize"], l["HideWindow"]], (int)session.Settings.LaunchBehavior, (s, i) => s with { LaunchBehavior = (LaunchBehavior)i });
                    Pick("Language", [l["JapaneseLanguage"], l["EnglishLanguage"]], l.Language == "en-US" ? 1 : 0, (s, i) => s with { Language = i == 1 ? "en-US" : "ja-JP" }); break;
                case "Appearance":
                    Pick("Theme", [l["System"], l["Light"], l["Dark"]], Array.IndexOf(new[] { "System", "Light", "Dark" }, session.Settings.Theme), (s, i) => s with { Theme = new[] { "System", "Light", "Dark" }[i] });
                    Pick("DefaultView", [l["Grid"], l["List"]], session.Settings.GridView ? 0 : 1, (s, i) => s with { GridView = i == 0 }); break;
                case "Bve": await BuildRuntimesAsync(); break;
                case "Scenarios": BuildRoots(); break;
                case "Import":
                    Pick("DefaultRoot", session.Settings.Roots.Select(r => r.Path).ToArray(), Math.Max(0, session.Settings.Roots.FindIndex(r => r.Id == session.Settings.DefaultRootId)), (s, i) => s with { DefaultRootId = i < s.Roots.Count ? s.Roots[i].Id : null });
                    Pick("Conflict", [l["Ask"], l["Skip"], l["Overwrite"]], (int)session.Settings.ImportConflicts, (s, i) => s with { ImportConflicts = (ConflictBehavior)i });
                    var days = UiFactory.Identify(new NumberBox { Value = session.Settings.BackupRetentionDays, Minimum = 1, Maximum = 3650, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact }, "BackupDays", l["BackupDays"]);
                    NumberBoxAutomation.Attach(days, "BackupDays", l["BackupDays"]);
                    SettingsContent.Children.Add(UiFactory.Row(l["BackupDays"], days)); updates.Add(s => s with { BackupRetentionDays = int.Parse(NumberBoxAutomation.ReadInteger(days), System.Globalization.CultureInfo.InvariantCulture) });
                    SettingsContent.Children.Add(Action("ImportHistory", ShowInstallationsAsync)); SettingsContent.Children.Add(Action("Backups", ShowBackupsAsync)); break;
                case "Advanced":
                    SettingsContent.Children.Add(Action("Rebuild", async () => { await session.ScanAsync(null, CancellationToken.None); SettingsStatus.Text = l["Saved"]; }));
                    SettingsContent.Children.Add(Action("ClearCache", async () =>
                    {
                        if (!await ConfirmAsync("CacheConfirm")) return;
                        await Task.Run(() => { foreach (var path in Directory.EnumerateFiles(session.Paths.Cache)) File.Delete(path); }); SettingsStatus.Text = l["Saved"];
                    }));
                    SettingsContent.Children.Add(Action("DataFolder", () => { ExplorerInterop.OpenFolder(session.Paths.Root); return Task.CompletedTask; }));
                    SettingsContent.Children.Add(Action("LogFolder", () => { ExplorerInterop.OpenFolder(session.Paths.Logs); return Task.CompletedTask; }));
                    SettingsContent.Children.Add(Action("RestoreHidden", async () => { foreach (var hidden in session.Library.Scenarios.Where(s => s.Hidden).ToArray()) await session.UpdateScenarioAsync(hidden with { Hidden = false }); SettingsStatus.Text = l["Saved"]; }));
                    SettingsContent.Children.Add(Action("ResetSettings", async () => { if (await ConfirmAsync("ResetConfirm")) { await session.SaveSettingsAsync(new AppSettings { Roots = session.Settings.Roots, Runtimes = session.Settings.Runtimes }); SettingsStatus.Text = l["Saved"]; } })); break;
                case "About":
                    var branding = App.Services.GetRequiredService<Branding>(); SettingsContent.Children.Add(UiFactory.Text(branding.DisplayName + " " + branding.Version));
                    SettingsContent.Children.Add(UiFactory.Text("Copyright (C) 2026 BVELauncher contributors · GPL-3.0-or-later"));
                    SettingsContent.Children.Add(UiFactory.Text(l["GplNotice"])); SettingsContent.Children.Add(UiFactory.Text(l["Unofficial"]));
                    SettingsContent.Children.Add(new HyperlinkButton { Content = "GitHub", NavigateUri = new Uri("https://github.com/mituba4154/BVELauncher") });
                    SettingsContent.Children.Add(Action("LicenseText", () => { ExplorerInterop.OpenTextFile(Path.Combine(AppContext.BaseDirectory, "LICENSE")); return Task.CompletedTask; }));
                    SettingsContent.Children.Add(Action("Notices", () => { ExplorerInterop.OpenTextFile(Path.Combine(AppContext.BaseDirectory, "THIRD_PARTY_NOTICES.md")); return Task.CompletedTask; })); SaveButton.Visibility = Visibility.Collapsed; break;
            }
        });
    }
    private void Pick(string key, string[] items, int selected, Func<AppSettings, int, AppSettings> update)
    {
        var combo = UiFactory.Combo(items, items.Length == 0 ? -1 : Math.Clamp(selected, 0, items.Length - 1), "Setting" + key, l[key]);
        SettingsContent.Children.Add(UiFactory.Row(l[key], combo)); updates.Add(s => combo.SelectedIndex < 0 ? s : update(s, combo.SelectedIndex));
    }
    private Button Action(string key, Func<Task> action) => UiFactory.Button(l[key], "Setting" + key, () => GuardAsync(action));
    public void SelectBvePage() => SettingsNavigation.SelectedItem = SettingsNavigation.MenuItems.Cast<NavigationViewItem>().Single(item => (string)item.Tag == "Bve");
    private async Task BuildRuntimesAsync()
    {
        var runtimes = session.Settings.Runtimes;
        SettingsContent.Children.Add(UiFactory.Text(l["NativeSettingsHint"]));
        SettingsContent.Children.Add(UiFactory.Identify(new HyperlinkButton { Content = l["InstallBve"], NavigateUri = new("https://www.bvets.net/jp/download/") }, "BveOfficialDownload", l["InstallBve"]));
        SettingsContent.Children.Add(Action("DetectBve", async () => { await session.RefreshRuntimesAsync(); await vm.RescanCommand.ExecuteAsync(null); await BuildBvePageAsync(); }));
        Pick("DefaultRuntime", runtimes.Select(r => r.Kind + " " + r.Version).ToArray(), Math.Max(0, runtimes.FindIndex(r => r.Id == session.Settings.DefaultRuntimeId)), (s, i) => s with { DefaultRuntimeId = runtimes[i].Id });
        SettingsContent.Children.Add(Action("AddRuntime", async () =>
        {
            var picker = new FileOpenPicker(AppWindow.Id); picker.FileTypeFilter.Add(".exe"); var file = await picker.PickSingleFileAsync(); if (file == null) return;
            var runtime = await Task.Run(() => App.Services.GetRequiredService<IRuntimeDetector>().Inspect(file.Path));
            if (!runtime.IsValid) throw new InvalidDataException("RuntimeMissing");
            var updated = runtimes.Where(r => !r.ExecutablePath.Equals(runtime.ExecutablePath, StringComparison.OrdinalIgnoreCase)).Append(runtime).ToList();
            await session.SaveSettingsAsync(session.Settings with { Runtimes = updated }); await BuildBvePageAsync(); SettingsStatus.Text = l["Saved"];
        }));
        foreach (var runtime in runtimes)
        {
            var panel = UiFactory.Form(); panel.Children.Add(UiFactory.Text(runtime.Kind + " " + runtime.Version));
            panel.Children.Add(UiFactory.Button(l["OpenNativeBveSettings"], "OpenBve" + runtime.Kind, () => GuardAsync(() =>
            {
                if (!App.Services.GetRequiredService<IRuntimeDetector>().Inspect(runtime.ExecutablePath).IsValid) throw new InvalidDataException("RuntimeMissing");
                var info = new System.Diagnostics.ProcessStartInfo(runtime.ExecutablePath) { WorkingDirectory = runtime.InstallDirectory, UseShellExecute = false };
                System.Diagnostics.Process.Start(info)?.Dispose(); SettingsStatus.Text = l["NativeSettingsHint"]; return Task.CompletedTask;
            })));
            var details = UiFactory.Form(); details.Children.Add(UiFactory.Text(runtime.ExecutablePath)); details.Children.Add(UiFactory.Text(runtime.PreferencesPath ?? ""));
            panel.Children.Add(new Expander { Header = l["ShowDetails"], Content = details, HorizontalAlignment = HorizontalAlignment.Stretch });
            SettingsContent.Children.Add(panel);
        }
        var statuses = await session.Extensions.DetectAsync(runtimes); SettingsContent.Children.Add(UiFactory.Text(l["Extensions"]));
        foreach (var extension in statuses)
        {
            SettingsContent.Children.Add(UiFactory.Text(extension.Name + " " + extension.Version));
            SettingsContent.Children.Add(new Expander { Header = l["ShowDetails"], Content = UiFactory.Text(extension.Path), HorizontalAlignment = HorizontalAlignment.Stretch });
        }
        if (statuses.Count == 0) SettingsContent.Children.Add(UiFactory.Text(l["NoExtension"]));
        SettingsContent.Children.Add(new Expander { Header = l["ExtensionCatalog"], Content = ExtensionCatalog.Build(session, l, this), IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch });
        SettingsContent.Children.Add(Action("LocalInstaller", async () =>
        {
            var picker = new FileOpenPicker(AppWindow.Id); picker.FileTypeFilter.Add(".exe"); var file = await picker.PickSingleFileAsync(); if (file == null || !await ConfirmAsync("InstallerConfirm")) return;
            await session.Extensions.RunLocalInstallerAsync(file.Path); SettingsStatus.Text = string.Join(Environment.NewLine, (await session.Extensions.DetectAsync(runtimes)).Select(s => s.Name + " " + s.Version));
        }));
    }
    private async Task BuildBvePageAsync()
    {
        updates.Clear(); SettingsContent.Children.Clear(); await BuildRuntimesAsync(); vm.RefreshVisible();
    }
    private void BuildRoots()
    {
        foreach (var root in session.Settings.Roots)
        {
            var button = UiFactory.Button(l["RemoveRoot"], "RemoveRoot" + root.Id.ToString("N"), () => GuardAsync(async () => { await session.SaveSettingsAsync(session.Settings with { Roots = session.Settings.Roots.Where(r => r.Id != root.Id).ToList() }); await vm.RescanCommand.ExecuteAsync(null); SettingsStatus.Text = l["Saved"]; }));
            SettingsContent.Children.Add(UiFactory.Row(root.Path, button));
        }
        SettingsContent.Children.Add(Action("AddFolder", async () =>
        {
            var folder = await new FolderPicker(AppWindow.Id).PickSingleFolderAsync(); if (folder == null) return;
            if (!session.Settings.Roots.Any(r => r.Path.Equals(folder.Path, StringComparison.OrdinalIgnoreCase))) await session.SaveSettingsAsync(session.Settings with { Roots = session.Settings.Roots.Append(new(Guid.NewGuid(), folder.Path)).ToList() });
            await vm.RescanCommand.ExecuteAsync(null); SettingsStatus.Text = l["Saved"];
        }));
    }
    private async void Save_Click(object sender, RoutedEventArgs e) => await GuardAsync(async () =>
    {
        var settings = updates.Aggregate(session.Settings, (current, update) => update(current)); await session.SaveSettingsAsync(settings);
        if (settings.Language.Length > 0) l.SetLanguage(settings.Language); vm.GridView = settings.GridView; App.Window.ApplyTheme();
        SettingsStatus.Text = l["Saved"];
    });
    private async Task ShowInstallationsAsync()
    {
        var records = (await session.Store.GetInstallationsAsync()).Where(r => !r.RolledBack).OrderByDescending(r => r.InstalledUtc).ToArray();
        var list = UiFactory.Identify(new ListView { ItemsSource = records, DisplayMemberPath = "Source", MaxHeight = 360, MinWidth = 560 }, "Installations", l["ImportHistory"]);
        var dialog = UiFactory.Dialog(SettingsContent.XamlRoot, l["ImportHistory"], l["Close"], list); dialog.PrimaryButtonText = l["Uninstall"];
        if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary && list.SelectedItem is InstallationRecord selected && await ConfirmAsync("UninstallConfirm"))
        { var result = await session.Imports.UninstallAsync(selected, session.Library); await vm.RescanCommand.ExecuteAsync(null); SettingsStatus.Text = l.Format("UninstallResult", result.Removed, result.Preserved.Count); }
    }
    private async Task ShowBackupsAsync()
    {
        var service = App.Services.GetRequiredService<IBackupService>(); var list = UiFactory.Identify(new ListView { ItemsSource = await service.ListAsync(), DisplayMemberPath = "OriginalPath", MaxHeight = 360, MinWidth = 560 }, "BackupList", l["Backups"]);
        var dialog = UiFactory.Dialog(SettingsContent.XamlRoot, l["Backups"], l["Close"], list); dialog.PrimaryButtonText = l["Restore"];
        if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary && list.SelectedItem is BackupRecord record && await ConfirmAsync("RestoreBackupConfirm"))
        { if (File.Exists(record.OriginalPath)) await service.CreateAsync(record.OriginalPath, "BeforeRestore"); await service.RestoreAsync(record); SettingsStatus.Text = l["Saved"]; }
    }
    private async Task<bool> ConfirmAsync(string key)
    { var dialog = UiFactory.Dialog(SettingsContent.XamlRoot, l["Confirm"], l["Cancel"], UiFactory.Text(l[key])); dialog.PrimaryButtonText = l["OK"]; return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary; }
    private async Task GuardAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            await session.ReportErrorAsync("Settings", ex);
            try { SettingsStatus.Text = l[ex.Message.Split(':')[0]]; }
            catch (Exception missing) when (missing is System.Runtime.InteropServices.COMException or ArgumentException)
            { SettingsStatus.Text = l.Format("OperationFailed", ex.Message); }
        }
    }
}
