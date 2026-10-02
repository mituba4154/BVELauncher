using System.Runtime.InteropServices;
using BVELauncher.App.Services;
using BVELauncher.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
namespace BVELauncher.App;
public sealed partial class MainWindow : Window
{
    private readonly LauncherSession session;
    private bool closing;
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    public MainWindow()
    {
        InitializeComponent();session=App.Services.GetRequiredService<LauncherSession>();
        var branding=App.Services.GetRequiredService<Branding>();Title=branding.DisplayName;AppTitleBar.Title=branding.DisplayName;
        ExtendsContentIntoTitleBar=true;SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory,"Assets","AppIcon.ico"));
        double scale=GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))/96.0;
        AppWindow.Resize(new SizeInt32((int)(1200*scale),(int)(800*scale)));
        AppWindow.Closing+=OnClosing;
        RootFrame.Navigate(typeof(MainPage));
    }
    public void ApplySettings()
    {
        var settings=session.Settings;var area=DisplayArea.GetFromWindowId(AppWindow.Id,DisplayAreaFallback.Primary).WorkArea;
        int width=Math.Clamp(settings.WindowWidth,640,Math.Max(640,area.Width));int height=Math.Clamp(settings.WindowHeight,480,Math.Max(480,area.Height));
        AppWindow.Resize(new(width,height));
        if(settings.WindowX is int x && settings.WindowY is int y)AppWindow.Move(new(Math.Clamp(x,area.X,area.X+Math.Max(0,area.Width-width)),Math.Clamp(y,area.Y,area.Y+Math.Max(0,area.Height-height))));
        if(settings.Maximized && AppWindow.Presenter is OverlappedPresenter presenter)presenter.Maximize();
        ApplyTheme();
    }
    public void ApplyTheme()=>((FrameworkElement)Content).RequestedTheme=session.Settings.Theme switch{"Light"=>ElementTheme.Light,"Dark"=>ElementTheme.Dark,_=>ElementTheme.Default};
    public void OnLaunch(LaunchBehavior behavior)
    {
        if(behavior==LaunchBehavior.Minimize && AppWindow.Presenter is OverlappedPresenter presenter)presenter.Minimize();
        if(behavior==LaunchBehavior.Hide)AppWindow.Hide();
    }
    public void RestoreAfterLaunch(){AppWindow.Show();if(AppWindow.Presenter is OverlappedPresenter presenter && presenter.State==OverlappedPresenterState.Minimized)presenter.Restore();Activate();}
    private async void OnClosing(AppWindow sender,AppWindowClosingEventArgs args)
    {
        if(closing)return;args.Cancel=true;closing=true;
        if(RootFrame.Content is MainPage page)page.CloseSecondaryWindows();
        try
        {
            var settings=session.Settings;bool maximized=AppWindow.Presenter is OverlappedPresenter presenter && presenter.State==OverlappedPresenterState.Maximized;
            await session.SaveSettingsAsync(settings with{WindowWidth=maximized?settings.WindowWidth:AppWindow.Size.Width,WindowHeight=maximized?settings.WindowHeight:AppWindow.Size.Height,WindowX=maximized?settings.WindowX:AppWindow.Position.X,WindowY=maximized?settings.WindowY:AppWindow.Position.Y,Maximized=maximized});
            session.StopWatching();await session.Runner.CleanupStaleGeneratedAsync();
        }
        catch(Exception ex){await session.ReportErrorAsync("Closing",ex);}
        Close();
    }
}
