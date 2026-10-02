using BVELauncher.Application;
using BVELauncher.Bve;
using BVELauncher.Infrastructure;
using BVELauncher.Integrations;
using BVELauncher.App.Services;
using BVELauncher.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
namespace BVELauncher.App;
public partial class App : Microsoft.UI.Xaml.Application
{
    public static IServiceProvider Services {get;private set;}=null!;
    public static MainWindow Window {get;private set;}=null!;
    private AppInstance? instance;
    public App()
    {
        InitializeComponent();
        var services=new ServiceCollection();
        string? testRoot=Environment.GetEnvironmentVariable("BVELAUNCHER_TEST_DATA");
        services.AddSingleton(new AppPaths(testRoot));
        services.AddSingleton<ILocalLog,LocalLog>();services.AddSingleton<ILibraryStore,SqliteLibraryStore>();
        services.AddSingleton<ISettingsStore,SettingsStore>();services.AddSingleton<IAssetReader,BveAssetReader>();
        services.AddSingleton<IRuntimeDetector,RuntimeDetector>();services.AddSingleton<LibraryScanner>();services.AddSingleton<CompatibilityEngine>();
        services.AddSingleton<IBackupService,BackupService>();services.AddSingleton(new ImportLimits());services.AddSingleton<ArchiveStager>();services.AddSingleton<IImportService,ImportService>();
        services.AddSingleton<ProcessRunner>();services.AddSingleton<IProcessRunner>(sp=>sp.GetRequiredService<ProcessRunner>());
        services.AddSingleton<IBveRuntimeAdapter,Bve5RuntimeAdapter>();services.AddSingleton<IBveRuntimeAdapter,Bve6RuntimeAdapter>();
        services.AddSingleton<IBveSettingsAdapter,Bve5SettingsAdapter>();services.AddSingleton<IBveSettingsAdapter,Bve6SettingsAdapter>();
        services.AddSingleton<LaunchScenario>();services.AddSingleton<LibraryWatcher>();services.AddSingleton<ExtensionDetector>();services.AddSingleton<LauncherSession>();
        services.AddSingleton<LocalizationService>();services.AddSingleton<Branding>();services.AddSingleton<ThumbnailService>();services.AddTransient<MainPageViewModel>();
        Services=services.BuildServiceProvider();
        UnhandledException+=(_,e)=>{Services.GetRequiredService<ILocalLog>().Write("Error","Unhandled UI exception.",e.Exception);};
    }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            instance=AppInstance.FindOrRegisterForKey("BVELauncher"+(Environment.GetEnvironmentVariable("BVELAUNCHER_TEST_DATA")??""));
            if(!instance.IsCurrent){await instance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());Exit();return;}
            Window=new MainWindow();
            instance.Activated+=(_,activation)=>Window.DispatcherQueue.TryEnqueue(()=>{Window.AppWindow.Show();Window.Activate();});
            Window.Activate();
        }
        catch(Exception ex){Services.GetRequiredService<ILocalLog>().Write("Error","Application startup failed.",ex);throw;}
    }
}
