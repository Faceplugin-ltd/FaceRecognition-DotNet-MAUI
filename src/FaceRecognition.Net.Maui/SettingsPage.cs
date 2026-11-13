namespace FaceRecognition.Net.Maui;

public class SettingsPage : ContentPage
{
    readonly RadioButton _front = new() { Content = "Front", TextColor = DemoTheme.Text };
    readonly RadioButton _back = new() { Content = "Back", TextColor = DemoTheme.Text };
    readonly Entry _liveness = NumEntry();
    readonly RadioButton _accurate = new() { Content = "High Accuracy", TextColor = DemoTheme.Text };
    readonly RadioButton _light = new() { Content = "Light Weight", TextColor = DemoTheme.Text };
    readonly Entry _identify = NumEntry();
    readonly Entry _yaw = NumEntry();
    readonly Entry _roll = NumEntry();
    readonly Entry _pitch = NumEntry();
    readonly Entry _eyes = NumEntry();

    public SettingsPage()
    {
        Title = "Settings";
        BackgroundColor = DemoTheme.Bg;
        Shell.SetBackgroundColor(this, DemoTheme.Bg);
        Shell.SetTitleColor(this, DemoTheme.Text);

        var clear = new Button
        {
            Text = "Clear all person",
            TextColor = DemoTheme.Danger,
            BackgroundColor = Colors.Transparent,
        };
        clear.Clicked += async (_, _) =>
        {
            if (!await DisplayAlert("Settings", "Remove every enrolled face?", "Clear", "Cancel"))
            {
                return;
            }

            await PersonStore.ClearAsync();
            await DisplayAlert("Settings", "Enrolled faces cleared.", "OK");
        };

        var restore = new Button
        {
            Text = "Restore default settings",
            TextColor = DemoTheme.Accent,
            BackgroundColor = Colors.Transparent,
        };
        restore.Clicked += (_, _) =>
        {
            SettingsStore.RestoreDefaults();
            Load();
            DisplayAlert("Settings", "Defaults restored.", "OK");
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    Section("Camera"),
                    Card(new VerticalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Label { Text = "Camera lens", TextColor = DemoTheme.Text },
                            new HorizontalStackLayout { Spacing = 24, Children = { _front, _back } },
                        },
                    }),
                    Section("Thresholds"),
                    Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            Row("Liveness", _liveness),
                            new Label { Text = "Liveness level", TextColor = DemoTheme.Text },
                            new HorizontalStackLayout { Spacing = 16, Children = { _accurate, _light } },
                            Row("Identify", _identify),
                            Row("Yaw", _yaw),
                            Row("Roll", _roll),
                            Row("Pitch", _pitch),
                            Row("Eye-close", _eyes),
                        },
                    }),
                    Section("Reset"),
                    Card(new VerticalStackLayout { Children = { clear, restore } }),
                },
            },
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Load();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Persist();
    }

    void Load()
    {
        var s = SettingsStore.Current;
        _front.IsChecked = s.FrontCamera;
        _back.IsChecked = !s.FrontCamera;
        _accurate.IsChecked = s.LivenessLevel == 0;
        _light.IsChecked = s.LivenessLevel != 0;
        _liveness.Text = s.LivenessThreshold.ToString();
        _identify.Text = s.IdentifyThreshold.ToString();
        _yaw.Text = s.YawThreshold.ToString();
        _roll.Text = s.RollThreshold.ToString();
        _pitch.Text = s.PitchThreshold.ToString();
        _eyes.Text = s.EyeCloseThreshold.ToString();
    }

    void Persist()
    {
        SettingsStore.Save(new AppSettings
        {
            FrontCamera = _front.IsChecked,
            LivenessLevel = _accurate.IsChecked ? 0 : 1,
            LivenessThreshold = Read(_liveness, 0.8, 0, 1),
            IdentifyThreshold = Read(_identify, 0.67, 0, 1),
            YawThreshold = Read(_yaw, 40, 0, 90),
            RollThreshold = Read(_roll, 40, 0, 90),
            PitchThreshold = Read(_pitch, 40, 0, 90),
            EyeCloseThreshold = Read(_eyes, 0.5, 0, 1),
        });
    }

    static double Read(Entry entry, double fallback, double min, double max)
    {
        if (!double.TryParse(entry.Text, out var value) || value < min || value > max)
        {
            return fallback;
        }

        return value;
    }

    static Entry NumEntry() => new()
    {
        Keyboard = Keyboard.Numeric,
        TextColor = DemoTheme.Text,
        BackgroundColor = DemoTheme.Surface,
    };

    static Label Section(string title) => new()
    {
        Text = title.ToUpperInvariant(),
        TextColor = DemoTheme.Accent,
        FontSize = 13,
        CharacterSpacing = 1,
        Margin = new Thickness(4, 8, 4, 0),
    };

    static Border Card(View content) => new()
    {
        BackgroundColor = DemoTheme.Surface,
        StrokeThickness = 0,
        Padding = new Thickness(14),
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
        Content = content,
    };

    static Grid Row(string title, View editor)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(140),
            },
        };
        grid.Add(new Label
        {
            Text = title,
            TextColor = DemoTheme.Text,
            VerticalTextAlignment = TextAlignment.Center,
        });
        grid.Add(editor, 1, 0);
        return grid;
    }
}
