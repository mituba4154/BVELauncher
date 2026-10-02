// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.App.ViewModels;
using BVELauncher.Core;
using BVELauncher.Infrastructure;
using BVELauncher.Application;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BVELauncher.App.Services;

public sealed class LibraryDialogs(LauncherSession session, LocalizationService l, MainPageViewModel vm, XamlRoot root)
{
    public async Task ErrorAsync(string message) => await UiFactory.Dialog(root, l["Error"], l["Close"], UiFactory.Text(message)).ShowQueuedAsync();
    public async Task DetailsAsync(ScenarioCardViewModel card)
    {
        if (scenarioSettingsWindow != null) { scenarioSettingsWindow.Activate(); return; }
        scenarioSettingsWindow = new(session, l, card);
        scenarioSettingsWindow.Activate();
        try { await scenarioSettingsWindow.Completion; }
        finally { scenarioSettingsWindow = null; }
    }
    private ScenarioSettingsWindow? scenarioSettingsWindow;
    public void CloseScenarioSettings() => scenarioSettingsWindow?.Close();
    public async Task GroupAsync(ScenarioCardViewModel card)
    {
        var field = UiFactory.Identify(new TextBox { Text = card.Entry.CustomGroup ?? "", Header = l["Group"] }, "GroupName", l["Group"]);
        var dialog = UiFactory.Dialog(root, l["MoveGroup"], l["Cancel"], field); dialog.PrimaryButtonText = l["Save"];
        if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary) await session.UpdateScenarioAsync(card.Entry with { CustomGroup = string.IsNullOrWhiteSpace(field.Text) ? null : field.Text });
    }
    public async Task VehicleAsync(ScenarioCardViewModel? card)
    {
        var panel = new Grid { RowSpacing = 12, MinWidth = 460, MaxHeight = 580 };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto }); panel.RowDefinitions.Add(new() { Height = new(280) }); panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var search = UiFactory.Identify(new TextBox { Header = l["Search"] }, "VehicleSearch", l["Search"]);
        var list = UiFactory.Identify(new ListView { SelectionMode = ListViewSelectionMode.Single, ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Application.Current.Resources["VehicleTemplate"] }, "VehicleList", l["Vehicles"]);
        var details = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, MaxHeight = 200 };
        UiFactory.Identify(details, "VehicleDetails", l["References"]);
        var vehicles = session.Library.Vehicles;
        var candidates = new List<VehicleEntry>();
        if (card != null) candidates.Add(new(Guid.Empty, "", l["StandardVehicle"], [], [], DateTime.MinValue, ""));
        candidates.AddRange(vehicles.OrderBy(v => v.DisplayName, StringComparer.CurrentCultureIgnoreCase));
        var options = candidates.ToDictionary(v => v.Id, v => new VehicleOption(v));
        list.ItemsSource = candidates.Select(v => options[v.Id]).ToArray(); list.SelectedIndex = candidates.Count > 0 ? 0 : -1;
        search.TextChanged += (_, _) => list.ItemsSource = VehicleQuery.Apply(candidates, search.Text, CultureInfo.GetCultureInfo(l.Language)).Select(v => options[v.Id]).ToArray();
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is not VehicleOption option) return;
            var selected = option.Entry;
            var usage = session.Library.Scenarios.Where(s => s.References.Any(r => r.ResolvedPath.Equals(selected.Path, StringComparison.OrdinalIgnoreCase))).Select(s => s.DisplayTitle);
            details.Text = selected.Path + Environment.NewLine + "ATS32: " + (selected.Ats32 ?? "") + Environment.NewLine + "ATS64: " + (selected.Ats64 ?? "") + Environment.NewLine +
                l["UsedBy"] + ": " + string.Join(", ", usage) + Environment.NewLine + string.Join(Environment.NewLine, selected.References.Select(r => (r.Exists ? "✓ " : "⚠ ") + r.Key + ": " + r.RawPath));
        };
        var detailsExpander = UiFactory.Identify(new Expander { Header = l["ShowDetails"], Content = details, HorizontalAlignment = HorizontalAlignment.Stretch }, "VehicleDetailsExpander", l["ShowDetails"]);
        panel.Children.Add(search); Grid.SetRow(list, 1); panel.Children.Add(list); Grid.SetRow(detailsExpander, 2); panel.Children.Add(detailsExpander);
        var dialog = UiFactory.Dialog(root, l["Vehicles"], l["Close"], panel);
        if (card != null) dialog.PrimaryButtonText = l["DriveThisVehicle"];
        var result = await dialog.ShowQueuedAsync();
        if (card != null && result == ContentDialogResult.Primary && list.SelectedItem is VehicleOption selectedOption)
        {
            var chosen = selectedOption.Entry;
            await vm.DriveWithVehicleAsync(card, chosen.Id == Guid.Empty ? null : new(chosen.Path, chosen.DisplayName));
        }
    }
    public async Task DiagnosticsAsync(ScenarioCardViewModel card)
    {
        var panel = UiFactory.Form();
        foreach (var reference in card.Entry.References) panel.Children.Add(UiFactory.Text((reference.Exists ? "✓ " : "⚠ ") + reference.Key + ": " + reference.RawPath));
        foreach (var item in card.Entry.Compatibility.Items)
        {
            var text = l[item.Code]; var detail = UiFactory.Text(string.Join(Environment.NewLine, new[] { item.SourcePath, item.Reference, item.ResolvedPath, item.Detail }.Where(s => s.Length > 0)));
            panel.Children.Add(new Expander { Header = text, Content = detail, HorizontalAlignment = HorizontalAlignment.Stretch });
        }
        if (card.Entry.Compatibility.Items.Count == 0) panel.Children.Add(UiFactory.Text(l["NoDiagnostics"]));
        await UiFactory.Dialog(root, l["Diagnostics"], l["Close"], new ScrollViewer { Content = panel, MaxHeight = 560 }).ShowQueuedAsync();
    }
    public async Task ImportAsync(string sourcePath, string? destinationOverride = null)
    {
        if (session.Settings.Roots.Count == 0) { await ErrorAsync(l["NoRoot"]); return; }
        var destination = destinationOverride ?? session.Settings.Roots.FirstOrDefault(r => r.Id == session.Settings.DefaultRootId)?.Path ?? session.Settings.Roots[0].Path;
        ImportPlan? plan = null;
        await vm.RunOperationAsync(async token => plan = await session.Imports.AnalyzeAsync(new(sourcePath), destination, new Progress<ScanProgress>(p => vm.Status = l.Format("Scanning", p.Files, p.Scenarios)), token));
        if (plan == null) return;
        try
        {
            var form = UiFactory.Form(); form.Children.Add(UiFactory.Text(l.Format("ImportSummary", plan.Scenarios, plan.Vehicles, plan.Operations.Count)));
            form.Children.Add(UiFactory.Text(l[plan.Confidence]));
            var target = UiFactory.Identify(new TextBox { Text = destination, Header = l["Destination"] }, "ImportDestination", l["Destination"]); form.Children.Add(target);
            var behavior = UiFactory.Identify(new RadioButtons { ItemsSource = new[] { l["Ask"], l["Skip"], l["Overwrite"] }, SelectedIndex = (int)session.Settings.ImportConflicts }, "ImportConflict", l["Conflict"]); form.Children.Add(UiFactory.Row(l["Conflict"], behavior));
            form.Children.Add(UiFactory.Text(l["Added"] + ": " + plan.Operations.Count(o => !o.Exists) + " / " + l["Overwritten"] + ": " + plan.Operations.Count(o => o.Exists)));
            var files = UiFactory.Identify(new ListView { ItemsSource = plan.Operations.Select(o => (o.Exists ? "⚠ " : "+ ") + o.RelativePath).ToArray(), MaxHeight = 200 }, "ImportFiles", l["Files"]);
            form.Children.Add(new Expander { Header = l["Files"], Content = files, HorizontalAlignment = HorizontalAlignment.Stretch });
            if (plan.Diagnostics.Count > 0) form.Children.Add(new Expander { Header = l["Warnings"] + " (" + plan.Diagnostics.Count + ")", Content = new ListView { ItemsSource = plan.Diagnostics.Select(d => d.Reference.Length > 0 ? d.Reference : d.Code).ToArray(), MaxHeight = 160 } });
            var dialog = UiFactory.Dialog(root, l["ImportPreview"], l["Cancel"], new ScrollViewer { Content = form, MaxHeight = 560 }); dialog.PrimaryButtonText = l["Import"];
            if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary) return;
            if (!target.Text.Equals(destination, StringComparison.OrdinalIgnoreCase))
            {
                // 配置先変更で競合の数が変わるため、新しいプレビューへ戻す。
                await session.Imports.DiscardAsync(plan); plan = null;
                await ImportAsync(sourcePath, target.Text); return;
            }
            var selected = (ConflictBehavior)behavior.SelectedIndex;
            if (plan.Operations.Any(o => o.Exists) && selected is ConflictBehavior.Ask or ConflictBehavior.Overwrite)
            {
                var confirm = UiFactory.Dialog(root, l["Confirm"], l["Cancel"], UiFactory.Text(l["ConflictConfirm"])); confirm.PrimaryButtonText = l["Overwrite"]; confirm.SecondaryButtonText = l["Skip"];
                var choice = await confirm.ShowQueuedAsync(); if (choice == ContentDialogResult.None) return; selected = choice == ContentDialogResult.Primary ? ConflictBehavior.Overwrite : ConflictBehavior.Skip;
            }
            var currentPlan = plan;
            await vm.RunOperationAsync(async token =>
            {
                var result = await session.Imports.CommitAsync(currentPlan, selected, new Progress<ScanProgress>(p => vm.Status = l.Format("Scanning", p.Files, p.Scenarios)), token);
                plan = null; await session.ScanAsync(null, token); vm.Status = l.Format("Imported", result.Files.Count);
            });
        }
        catch (Exception ex) { await vm.ShowErrorAsync(ex); }
        finally { if (plan != null && Directory.Exists(plan.StagingDirectory)) await session.Imports.DiscardAsync(plan); }
    }
}
