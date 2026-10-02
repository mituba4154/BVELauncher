// SPDX-FileCopyrightText: 2026 BVELauncher contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.ObjectModel;
using System.Globalization;
using BVELauncher.Application;
using BVELauncher.Core;
using BVELauncher.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BVELauncher.App.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly LauncherSession session;
    private readonly LocalizationService localization;
    private readonly LaunchScenario launch;
    private readonly ThumbnailService thumbnails;
    private readonly SynchronizationContext context;
    private CancellationTokenSource? operation;
    private CancellationTokenSource? searchCancellation;
    private bool refreshPending;
    private bool rescanPending;
    private readonly Dictionary<Guid, ScenarioCardViewModel> cards = [];
    private readonly Dictionary<Guid, (string? Path, DateTime Stamp, long Size)> thumbnailVersions = [];
    public ObservableCollection<ScenarioGroup> Groups { get; } = [];
    public ObservableCollection<ScenarioCardViewModel> VisibleScenarios { get; } = [];
    public Func<ScenarioCardViewModel, Task>? DetailsRequested { get; set; }
    public event Action<LaunchBehavior>? LaunchStarted;
    public event Action? LaunchEnded;
    public Func<string, Task>? ErrorRequested { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial LibraryFilter Filter { get; set; }
    [ObservableProperty] public partial SortOrder Sort { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; } = true;
    [ObservableProperty] public partial bool NeedsBve { get; set; }
    [ObservableProperty] public partial bool GridView { get; set; } = true;
    [ObservableProperty] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial ScenarioCardViewModel? Selected { get; set; }
    public MainPageViewModel(LauncherSession session, LocalizationService localization, LaunchScenario launch, ThumbnailService thumbnails)
    {
        (this.session, this.localization, this.launch, this.thumbnails) = (session, localization, launch, thumbnails);
        context = SynchronizationContext.Current ?? throw new InvalidOperationException("UI synchronization context required.");
        session.LibraryChanged += () => context.Post(_ => QueueRefresh(), null);
    }
    public async Task InitializeAsync()
    {
        await RunOperationAsync(async token =>
        {
            await session.InitializeAsync(token);
            UpdateBveAvailability();
            if (session.Settings.Language.Length > 0) localization.SetLanguage(session.Settings.Language);
            GridView = session.Settings.GridView; Sort = session.Settings.Sort;
            session.Watch(() => context.Post(_ => RequestRescan(), null));
            RefreshVisible();
            await session.ScanAsync(new Progress<ScanProgress>(OnScanProgress), token);
        });
    }
    [RelayCommand] private async Task RescanAsync() => await RunOperationAsync(token => session.ScanAsync(new Progress<ScanProgress>(OnScanProgress), token));
    [RelayCommand] private async Task DetectBveAsync() => await RunOperationAsync(async token =>
    {
        await session.RefreshRuntimesAsync(token); UpdateBveAvailability();
        await session.ScanAsync(new Progress<ScanProgress>(OnScanProgress), token);
    });
    public void UpdateBveAvailability() => NeedsBve = !session.Settings.Runtimes.Any(r => r.IsValid && File.Exists(r.ExecutablePath));
    public void RequestRescan()
    {
        if (IsBusy) { rescanPending = true; return; }
        RescanCommand.Execute(null);
    }
    private void QueueRefresh()
    {
        if (refreshPending) return;
        refreshPending = true;
        _ = RefreshLaterAsync();
    }
    private async Task RefreshLaterAsync()
    {
        await Task.Delay(120);
        refreshPending = false;
        RefreshVisible();
    }
    private void OnScanProgress(ScanProgress progress)
    {
        if (progress.Discovered != null) session.AcceptDiscovered(progress.Discovered);
        Status = localization.Format("Scanning", progress.Files, progress.Scenarios);
    }
    [RelayCommand] private void Cancel() => operation?.Cancel();
    [RelayCommand] private async Task DriveAsync(ScenarioCardViewModel? card)
    {
        card ??= Selected; if (card == null) return;
        await DriveWithVehicleAsync(card, null, true);
    }
    public async Task DriveWithVehicleAsync(ScenarioCardViewModel card, VehicleOverride? vehicle, bool useDefaultVehicle = false)
    {
        try
        {
            var selection = await session.ResolveLaunchVehicleAsync(card.Entry, vehicle, useDefaultVehicle);
            var plan = await launch.PrepareAsync(selection.Scenario, session.Settings.Runtimes, session.Settings, session.Paths.Generated, selection.Vehicle, useDefaultVehicle: false);
            var running = await launch.ExecuteAsync(plan);
            card.IsRunning = true; card.Status = localization["Running"]; LaunchStarted?.Invoke(plan.Behavior);
            try { await session.UpdateScenarioAsync(card.Entry with { LastLaunchUtc = running.StartedUtc }); }
            catch (Exception ex) { await session.ReportErrorAsync("Launch metadata update", ex); }
            await launch.WaitAsync(running);
            card.IsRunning = false; card.Status = ""; LaunchEnded?.Invoke();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { await ShowErrorAsync(ex); }
    }
    public async Task ActivateAsync(ScenarioCardViewModel card)
    {
        Selected = card;
        switch (session.Settings.ClickBehavior)
        {
            case ClickBehavior.Launch: DriveCommand.Execute(card); break;
            case ClickBehavior.Details: if (DetailsRequested != null) await DetailsRequested(card); break;
        }
    }
    [RelayCommand] private async Task FavoriteAsync(ScenarioCardViewModel card) => await session.UpdateScenarioAsync(card.Entry with { Favorite = !card.Entry.Favorite });
    [RelayCommand] private async Task HideAsync(ScenarioCardViewModel card) => await session.UpdateScenarioAsync(card.Entry with { Hidden = true });
    partial void OnSearchTextChanged(string value)
    {
        searchCancellation?.Cancel(); searchCancellation?.Dispose(); searchCancellation = new();
        _ = SearchAfterDelayAsync(searchCancellation.Token);
    }
    private async Task SearchAfterDelayAsync(CancellationToken token)
    {
        try { await Task.Delay(180, token); RefreshVisible(); }
        catch (OperationCanceledException) { /* 新しい検索入力に置き換わった。 */ }
    }
    partial void OnFilterChanged(LibraryFilter value) => RefreshVisible();
    partial void OnSortChanged(SortOrder value) => RefreshVisible();
    public void RefreshVisible()
    {
        UpdateBveAvailability();
        var selectedId = Selected?.Id;
        var visible = LibraryQuery.Apply(session.Library.Scenarios, SearchText, Filter, Sort, CultureInfo.GetCultureInfo(localization.Language));
        var imageFiles = session.Library.Assets.Where(a => a.Kind == AssetKind.Image).ToDictionary(a => a.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in session.Library.Scenarios)
        {
            if (cards.TryGetValue(entry.Id, out var card)) card.Update(entry);
            else { card = new(entry); cards[entry.Id] = card; }
            var image = entry.ImagePath == null ? null : imageFiles.GetValueOrDefault(entry.ImagePath);
            var version = (entry.ImagePath, image?.LastWriteUtc ?? DateTime.MinValue, image?.Size ?? 0);
            if (!thumbnailVersions.TryGetValue(entry.Id, out var previous) || previous != version)
            { thumbnailVersions[entry.Id] = version; card.ThumbnailPath = null; _ = LoadThumbnailAsync(card, version); }
        }
        foreach (var removed in cards.Keys.Except(session.Library.Scenarios.Select(s => s.Id)).ToArray()) { cards.Remove(removed); thumbnailVersions.Remove(removed); }
        var visibleCards = visible.Select(s => cards[s.Id]).ToArray();
        CollectionSynchronization.Update(VisibleScenarios, visibleCards);
        var existingGroups = Groups.ToDictionary(g => g.Title);
        var desiredGroups = new List<ScenarioGroup>();
        foreach (var group in visibleCards.GroupBy(c => c.Entry.Group.Length == 0 ? localization["Other"] : c.Entry.Group))
        {
            if (!existingGroups.TryGetValue(group.Key, out var target)) target = new(group.Key, []);
            CollectionSynchronization.Update(target.Items, session.Settings.CollapsedGroups.Contains(group.Key) ? [] : group.ToArray());
            desiredGroups.Add(target);
        }
        CollectionSynchronization.Update(Groups, desiredGroups);
        Selected = selectedId.HasValue ? visibleCards.FirstOrDefault(c => c.Id == selectedId) : null;
        IsEmpty = visibleCards.Length == 0; Status = localization.Format("Count", visibleCards.Length);
    }
    private async Task LoadThumbnailAsync(ScenarioCardViewModel card, (string? Path, DateTime Stamp, long Size) version)
    {
        var thumbnail = await thumbnails.GetAsync(version.Path, CancellationToken.None);
        // 遅れて完了した旧画像の処理で、新しい画像を上書きしない。
        if (thumbnailVersions.TryGetValue(card.Id, out var current) && current == version) card.ThumbnailPath = thumbnail;
    }
    public async Task ToggleGroupAsync(string title)
    {
        var collapsed = session.Settings.CollapsedGroups.ToList();
        if (!collapsed.Remove(title)) collapsed.Add(title);
        await session.SaveSettingsAsync(session.Settings with { CollapsedGroups = collapsed }); RefreshVisible();
    }
    public async Task RunOperationAsync(Func<CancellationToken, Task> work)
    {
        if (IsBusy) return;
        operation = new(); IsBusy = true;
        try { await work(operation.Token); }
        catch (OperationCanceledException) { Status = localization["Cancel"]; }
        catch (Exception ex) { await ShowErrorAsync(ex); }
        finally
        {
            IsBusy = false; operation.Dispose(); operation = null;
            if (rescanPending) { rescanPending = false; context.Post(_ => RequestRescan(), null); }
        }
    }
    public async Task ShowErrorAsync(Exception error)
    {
        await session.ReportErrorAsync("UI operation", error);
        string key = error.Message.Split(':')[0];
        string message;
        try { message = localization[key]; }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ArgumentException) { message = localization.Format("OperationFailed", error.Message); }
        if (ErrorRequested != null) await ErrorRequested(message);
    }
}

