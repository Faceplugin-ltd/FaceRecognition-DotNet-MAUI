namespace FaceRecognition.Net.Maui;

public class AboutPage : ContentPage
{
    readonly Label _license = new()
    {
        Text = "License: …",
        TextColor = DemoTheme.Text,
        HorizontalTextAlignment = TextAlignment.Center,
    };

    public AboutPage()
    {
        Title = "About";
        BackgroundColor = DemoTheme.Bg;
        Shell.SetBackgroundColor(this, DemoTheme.Bg);
        Shell.SetTitleColor(this, DemoTheme.Text);
        var site = new Button
        {
            Text = "faceplugin.com",
            TextColor = DemoTheme.Accent,
            BackgroundColor = Colors.Transparent,
        };
        site.Clicked += async (_, _) => await Launcher.Default.OpenAsync("https://faceplugin.com");
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 32),
                Spacing = 12,
                Children =
                {
                    DemoUi.Logo(),
                    new Label
                    {
                        Text = "FacePlugin",
                        TextColor = DemoTheme.Text,
                        FontSize = 22,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                    },
                    new Label
                    {
                        Text = "Face Recognition SDK",
                        TextColor = DemoTheme.Accent,
                        FontSize = 15,
                        HorizontalTextAlignment = TextAlignment.Center,
                    },
                    Card(_license),
                    Card(new Label
                    {
                        Text = "FacePlugin builds on-device identity technology — face recognition, liveness, and document reading — so biometric data never has to leave the phone.",
                        TextColor = DemoTheme.Text,
                    }),
                    Card(new Label
                    {
                        Text = "This app demos the Face Recognition SDK for .NET MAUI: enroll, identify, capture, and attribute analysis. Everything runs fully on-premise.",
                        TextColor = DemoTheme.Text,
                    }),
                    site,
                    new Label
                    {
                        Text = "© 2026 FacePlugin. All rights reserved.",
                        TextColor = DemoTheme.Muted,
                        FontSize = 12,
                        HorizontalTextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 16, 0, 0),
                    },
                },
            },
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var label = FaceJson.LicenseLabel(await FaceSdk.GetLicenseStatusAsync());
            _license.Text = "License: " + label;
        }
        catch
        {
            _license.Text = "License: Not licensed";
        }
    }

    static Border Card(View content) => new()
    {
        BackgroundColor = DemoTheme.Surface,
        StrokeThickness = 0,
        Padding = new Thickness(16),
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
        Content = content,
    };
}
