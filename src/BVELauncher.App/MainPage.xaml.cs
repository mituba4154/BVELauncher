using BVELauncher.App.Services;
using BVELauncher.App.ViewModels;
using BVELauncher.Application;
using BVELauncher.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
namespace BVELauncher.App;
public sealed partial class MainPage:Page
{
    public MainPageViewModel ViewModel{get;}
    private readonly LauncherSession session;
    private readonly LocalizationService l;
    private LibraryDialogs dialogs=null!;
    private SettingsWindow? settingsWindow;
    private bool localized;
    public MainPage()
    {
        ViewModel=App.Services.GetRequiredService<MainPageViewModel>();session=App.Services.GetRequiredService<LauncherSession>();l=App.Services.GetRequiredService<LocalizationService>();
        InitializeComponent();Localize();
        l.Changed+=()=>DispatcherQueue.TryEnqueue(()=>{Localize();ViewModel.RefreshVisible();App.Window.Title=App.Services.GetRequiredService<Branding>().DisplayName;});
        Loaded+=Loaded_Page;
        foreach(var (key,action) in new (VirtualKey,Action)[]{(VirtualKey.F,()=>SearchBox.Focus(FocusState.Keyboard)),(VirtualKey.R,()=>ViewModel.RescanCommand.Execute(null)),((VirtualKey)188,()=>OpenSettings())})
        {
            var accelerator=new KeyboardAccelerator{Key=key,Modifiers=VirtualKeyModifiers.Control};accelerator.Invoked+=(_,e)=>{action();e.Handled=true;};KeyboardAccelerators.Add(accelerator);
        }
    }
    private async void Loaded_Page(object sender,RoutedEventArgs e)
    {
        Loaded-=Loaded_Page;dialogs=new(session,l,ViewModel,XamlRoot);
        ViewModel.LaunchStarted+=App.Window.OnLaunch;ViewModel.LaunchEnded+=App.Window.RestoreAfterLaunch;
        ViewModel.ErrorRequested=dialogs.ErrorAsync;ViewModel.DetailsRequested=dialogs.DetailsAsync;
        await ViewModel.InitializeAsync();App.Window.ApplySettings();
    }
    private void Localize()
    {
        localized=false;
        foreach(var (button,key) in new (AppBarButton,string)[]{(ImportButton,"Import"),(FolderButton,"Folder"),(RefreshButton,"Refresh"),(VehiclesButton,"Vehicles"),(GridButton,"Grid"),(ListButton,"List"),(SettingsButton,"Settings")})
        {button.Label=l[key];ToolTipService.SetToolTip(button,l[key]);AutomationProperties.SetName(button,l[key]);}
        SearchBox.PlaceholderText=l["Search"];AutomationProperties.SetName(SearchBox,l["Search"]);
        EmptyText.Text=l["Empty"];EmptyFolderButton.Content=l["AddFolder"];EmptyImportButton.Content=l["Import"];DriveButton.Content=l["Launch"];CancelButton.Content=l["Cancel"];DropText.Text=l["DropHint"];
        FilterBox.Items.Clear();foreach(var key in new[]{"All","Bve6","Bve5","Favorites","Warnings"})FilterBox.Items.Add(l[key]);FilterBox.SelectedIndex=(int)ViewModel.Filter;
        SortBox.Items.Clear();foreach(var key in new[]{"Name","Route","Vehicle","Recent","Modified"})SortBox.Items.Add(l[key]);SortBox.SelectedIndex=(int)ViewModel.Sort;
        AutomationProperties.SetName(FilterBox,l["All"]);AutomationProperties.SetName(SortBox,l["Sort"]);localized=true;
    }
    public static Visibility Visible(bool value)=>value?Visibility.Visible:Visibility.Collapsed;
    public static Visibility Hidden(bool value)=>value?Visibility.Collapsed:Visibility.Visible;
    public static ImageSource? CachedImage(string? path)=>string.IsNullOrWhiteSpace(path)?null:new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(path));
    private async void Scenario_Click(object sender,ItemClickEventArgs e){if(e.ClickedItem is ScenarioCardViewModel card)await GuardAsync(()=>ViewModel.ActivateAsync(card));}
    private void Selection_Changed(object sender,SelectionChangedEventArgs e){if(e.AddedItems.FirstOrDefault() is ScenarioCardViewModel card)ViewModel.Selected=card;}
    private async void Group_Click(object sender,RoutedEventArgs e)=>await GuardAsync(()=>ViewModel.ToggleGroupAsync((string)((Button)sender).Tag));
    private void Filter_Changed(object sender,SelectionChangedEventArgs e){if(localized&&FilterBox.SelectedIndex>=0)ViewModel.Filter=(LibraryFilter)FilterBox.SelectedIndex;}
    private async void Sort_Changed(object sender,SelectionChangedEventArgs e){if(localized&&SortBox.SelectedIndex>=0){ViewModel.Sort=(SortOrder)SortBox.SelectedIndex;await GuardAsync(()=>session.SaveSettingsAsync(session.Settings with{Sort=ViewModel.Sort}));}}
    private async void Grid_Click(object sender,RoutedEventArgs e){ViewModel.GridView=true;await GuardAsync(()=>session.SaveSettingsAsync(session.Settings with{GridView=true}));}
    private async void List_Click(object sender,RoutedEventArgs e){ViewModel.GridView=false;await GuardAsync(()=>session.SaveSettingsAsync(session.Settings with{GridView=false}));}
    private async void Import_Click(object sender,RoutedEventArgs e)
    {
        await GuardAsync(async()=>{var picker=new FileOpenPicker(App.Window.AppWindow.Id);picker.FileTypeFilter.Add(".zip");picker.FileTypeFilter.Add(".7z");picker.FileTypeFilter.Add(".txt");picker.FileTypeFilter.Add(".dat");var file=await picker.PickSingleFileAsync();if(file!=null)await dialogs.ImportAsync(file.Path);});
    }
    private async void Folder_Click(object sender,RoutedEventArgs e)
    {
        await GuardAsync(async()=>{var folder=await new FolderPicker(App.Window.AppWindow.Id).PickSingleFolderAsync();if(folder==null)return;var roots=session.Settings.Roots.ToList();if(!roots.Any(r=>r.Path.Equals(folder.Path,StringComparison.OrdinalIgnoreCase)))roots.Add(new(Guid.NewGuid(),folder.Path));await session.SaveSettingsAsync(session.Settings with{Roots=roots});session.Watch(()=>DispatcherQueue.TryEnqueue(()=>ViewModel.RequestRescan()));ViewModel.RequestRescan();});
    }
    private async void Vehicles_Click(object sender,RoutedEventArgs e)=>await GuardAsync(()=>dialogs.VehicleAsync(null));
    private void Settings_Click(object sender,RoutedEventArgs e)=>OpenSettings();
    public void CloseSecondaryWindows(){dialogs?.CloseScenarioSettings();settingsWindow?.Close();}
    private void OpenSettings(){if(settingsWindow==null){settingsWindow=new(session,l,ViewModel);settingsWindow.Closed+=(_,_)=>{settingsWindow=null;App.Window.ApplyTheme();Localize();ViewModel.RefreshVisible();};}settingsWindow.Activate();}
    private void Scenario_RightTapped(object sender,RightTappedRoutedEventArgs e)
    {
        var element=e.OriginalSource as FrameworkElement;
        while(element!=null&&element.DataContext is not ScenarioCardViewModel)element=VisualTreeHelper.GetParent(element) as FrameworkElement;
        var card=element?.DataContext as ScenarioCardViewModel??ViewModel.Selected;if(card==null)return;
        ViewModel.Selected=card;var menu=ContextMenu(card);((FrameworkElement)sender).ContextFlyout=menu;menu.ShowAt((FrameworkElement)sender,new FlyoutShowOptions{Position=e.GetPosition((FrameworkElement)sender)});e.Handled=true;
    }
    private MenuFlyout ContextMenu(ScenarioCardViewModel card)
    {
        var menu=new MenuFlyout();
        Add("Launch",()=>ViewModel.DriveWithVehicleAsync(card,null,true));Add("OverrideLaunch",()=>dialogs.VehicleAsync(card));menu.Items.Add(new MenuFlyoutSeparator());
        Add("Favorite",()=>ViewModel.FavoriteCommand.ExecuteAsync(card));Add("MoveGroup",()=>dialogs.GroupAsync(card));
        Add("ScenarioSettings",()=>dialogs.DetailsAsync(card));Add("Diagnostics",()=>dialogs.DiagnosticsAsync(card));menu.Items.Add(new MenuFlyoutSeparator());
        Add("OpenFolder",()=>{ExplorerInterop.OpenFolder(System.IO.Path.GetDirectoryName(card.Entry.Path)!);return Task.CompletedTask;});Add("OpenFile",()=>{ExplorerInterop.OpenTextFile(card.Entry.Path);return Task.CompletedTask;});Add("Hide",()=>ViewModel.HideCommand.ExecuteAsync(card));return menu;
        void Add(string key,Func<Task> action){var item=UiFactory.Identify(new MenuFlyoutItem{Text=l[key]},"Menu"+key,l[key]);item.Click+=async(_,_)=>await GuardAsync(action);menu.Items.Add(item);}
    }
    private void ScenarioActions_Loaded(object sender,RoutedEventArgs e)
    {
        if(sender is Button button && button.DataContext is ScenarioCardViewModel card)
        {UiFactory.Identify(button,"ScenarioActions"+card.Entry.Id.ToString("N"),l["ScenarioActions"]);ToolTipService.SetToolTip(button,l["ScenarioActions"]);}
    }
    private void ScenarioActions_Click(object sender,RoutedEventArgs e)
    {
        if(sender is FrameworkElement element && element.DataContext is ScenarioCardViewModel card)
        {ViewModel.Selected=card;ContextMenu(card).ShowAt(element);}
    }
    private void Scenarios_KeyDown(object sender,KeyRoutedEventArgs e)
    {
        if(e.Key==VirtualKey.Enter){ViewModel.DriveCommand.Execute(ViewModel.Selected);e.Handled=true;}
        if(e.Key==VirtualKey.Application||e.Key==VirtualKey.F10 && Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {if(ViewModel.Selected!=null)ContextMenu(ViewModel.Selected).ShowAt((FrameworkElement)sender);e.Handled=true;}
    }
    private void Scenarios_DoubleTapped(object sender,DoubleTappedRoutedEventArgs e){if(session.Settings.ClickBehavior==ClickBehavior.Select)ViewModel.DriveCommand.Execute(ViewModel.Selected);}
    private void DragOver_Import(object sender,DragEventArgs e){if(e.DataView.Contains(StandardDataFormats.StorageItems)){e.AcceptedOperation=DataPackageOperation.Copy;e.DragUIOverride.Caption=l["DropHint"];DropOverlay.Visibility=Visibility.Visible;}}
    private void DragLeave_Import(object sender,DragEventArgs e)=>DropOverlay.Visibility=Visibility.Collapsed;
    private async void Drop_Import(object sender,DragEventArgs e){DropOverlay.Visibility=Visibility.Collapsed;await GuardAsync(async()=>{if(e.DataView.Contains(StandardDataFormats.StorageItems))foreach(var item in await e.DataView.GetStorageItemsAsync())await dialogs.ImportAsync(item.Path);});}
    private async Task GuardAsync(Func<Task> action){try{await action();}catch(Exception ex){await ViewModel.ShowErrorAsync(ex);}}
}
