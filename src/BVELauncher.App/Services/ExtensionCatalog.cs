// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using BVELauncher.Infrastructure;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;

namespace BVELauncher.App.Services;

public static class ExtensionCatalog
{
    public static FrameworkElement Build(LauncherSession session, LocalizationService l, Window owner)
    {
        var panel = UiFactory.Form();
        panel.Children.Add(UiFactory.Text(l["ExtensionCatalogHint"]));
        panel.Children.Add(UiFactory.Text("TsHud"));
        panel.Children.Add(UiFactory.Text(l["TsHudDescription"]));
        panel.Children.Add(UiFactory.Text(l["TsHudDependency"]));
        var link = UiFactory.Identify(new HyperlinkButton { Content = l["OfficialDownload"], NavigateUri = new("https://booth.pm/ja/items/8919155") }, "TsHudOfficialPage", l["OfficialDownload"]);
        panel.Children.Add(link);
        var status = UiFactory.Identify(UiFactory.Text(""), "ExtensionInstallStatus", l["Extensions"]);
        AutomationProperties.SetName(status, "");
        panel.Children.Add(UiFactory.Button(l["InstallDownloadedExtension"], "InstallTsHud", () => InstallAsync("TsHud.dll")));
        panel.Children.Add(UiFactory.Button(l["InstallOtherExtension"], "InstallOtherExtension", () => InstallAsync(null)));
        panel.Children.Add(status); return panel;

        async Task InstallAsync(string? selectedDll)
        {
            try
            {
                var picker = new FileOpenPicker(owner.AppWindow.Id); foreach (var type in new[] { ".zip", ".7z", ".dll" }) picker.FileTypeFilter.Add(type);
                var file = await picker.PickSingleFileAsync(); if (file == null) return;
                string destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "BveEx", "2.0", "Extensions");
                var targetPicker = new FolderPicker(owner.AppWindow.Id);
                var form = UiFactory.Form(); var target = UiFactory.Identify(new TextBox { Text = destination, Header = l["Destination"] }, "ExtensionDestination", l["Destination"]);
                var dllName = UiFactory.Identify(new TextBox { Text = selectedDll ?? (Path.GetExtension(file.Path).Equals(".dll", StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(file.Path) : ""), Header = l["ExtensionDllName"], IsReadOnly = selectedDll != null }, "ExtensionDllName", l["ExtensionDllName"]);
                if (selectedDll == null) form.Children.Add(dllName);
                form.Children.Add(UiFactory.Text(l["ExtensionDefaultDestination"]));
                var details = UiFactory.Form(); details.Children.Add(target); details.Children.Add(UiFactory.Button(l["Folder"], "ExtensionFolder", async () => { var folder = await targetPicker.PickSingleFolderAsync(); if (folder != null) target.Text = folder.Path; }));
                form.Children.Add(UiFactory.Identify(new Expander { Header = l["ShowDetails"], Content = details, HorizontalAlignment = HorizontalAlignment.Stretch }, "ExtensionDetails", l["ShowDetails"]));
                form.Children.Add(UiFactory.Text(l["ExtensionTrustHint"]));
                form.Children.Add(UiFactory.Text(l["ExtensionSingleDllHint"]));
                var dialog = UiFactory.Dialog(((FrameworkElement)owner.Content).XamlRoot, selectedDll == null ? l["Extensions"] : "TsHud", l["Cancel"], form); dialog.PrimaryButtonText = l["OK"];
                if (await dialog.ShowQueuedAsync() != ContentDialogResult.Primary) return;
                var planner = new ExtensionPackageImporter(session.Imports);
                var plan = await planner.AnalyzeAsync(file.Path, target.Text, dllName.Text.Trim());
                try
                {
                    var operation = plan.Operations.Single();
                    var preview = UiFactory.Dialog(((FrameworkElement)owner.Content).XamlRoot, l["ImportPreview"], l["Cancel"], UiFactory.Text(operation.DestinationPath + Environment.NewLine + (operation.Exists ? l["ConflictConfirm"] : l["ExtensionTrustHint"])));
                    preview.PrimaryButtonText = l["Import"];
                    if (await preview.ShowQueuedAsync() != ContentDialogResult.Primary) return;
                    await session.Imports.CommitAsync(plan, operation.Exists ? BVELauncher.Core.ConflictBehavior.Overwrite : BVELauncher.Core.ConflictBehavior.Skip);
                    status.Text = l["ExtensionInstalled"];
                }
                finally { if (Directory.Exists(plan.StagingDirectory)) await session.Imports.DiscardAsync(plan); }
            }
            catch (Exception ex) { await session.ReportErrorAsync("Extension import", ex); status.Text = l.Format("OperationFailed", ex.Message is "ExtensionPackageMissing" or "ExtensionPackageInvalid" ? l[ex.Message] : ex.Message); }
        }
    }
}
