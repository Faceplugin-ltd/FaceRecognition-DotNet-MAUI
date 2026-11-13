namespace FaceRecognition.Net.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(IdentifyPage), typeof(IdentifyPage));
        Routing.RegisterRoute(nameof(IdentifyResultPage), typeof(IdentifyResultPage));
        Routing.RegisterRoute(nameof(CapturePage), typeof(CapturePage));
        Routing.RegisterRoute(nameof(AttributePage), typeof(AttributePage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
    }
}
