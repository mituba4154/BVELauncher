namespace BVELauncher.App.Services;

public sealed class Branding(LocalizationService localization)
{
    public string DisplayName => localization["ProductDisplayName"];
    public string Description => localization["ProductDescription"];
    public string Version => typeof(Branding).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}

