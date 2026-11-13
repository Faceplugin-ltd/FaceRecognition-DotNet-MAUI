using Microsoft.Maui.Layouts;

namespace FaceRecognition.Net.Maui;

public class HomePage : ContentPage
{
    readonly Label _status = new()
    {
        TextColor = DemoTheme.Text,
        FontSize = 13,
        HorizontalTextAlignment = TextAlignment.Center,
    };
    readonly Border _errorBanner = new()
    {
        IsVisible = false,
        BackgroundColor = DemoTheme.StatusError,
        StrokeThickness = 0,
        Padding = new Thickness(12),
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
    };
    readonly Label _errorText = new() { TextColor = Colors.White, FontSize = 13 };
    readonly VerticalStackLayout _people = new() { Spacing = 8 };
    readonly Label _empty = new()
    {
        Text = "",
        TextColor = DemoTheme.Muted,
        HorizontalTextAlignment = TextAlignment.Center,
    };
    readonly AbsoluteLayout _root = new();
    readonly ActivityIndicator _busyOverlay = new()
    {
        IsVisible = false,
        IsRunning = false,
        Color = DemoTheme.Accent,
        BackgroundColor = Color.FromRgba(0, 0, 0, 0.45),
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
    };
    Border _enroll = null!;
    Border _identify = null!;
    Border _capture = null!;
    Border _attribute = null!;
    bool _started;
    bool _busy;

    public HomePage()
    {
        BackgroundColor = DemoTheme.Bg;
        Shell.SetNavBarIsVisible(this, false);
        _errorBanner.Content = _errorText;

        var tiles1 = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
            ColumnSpacing = 12,
        };
        _enroll = DemoUi.TileButton("ENROLL", "enroll", () => _ = EnrollAsync());
        _identify = DemoUi.TileButton("IDENTIFY", "identify", () => _ = Go(nameof(IdentifyPage)));
        _capture = DemoUi.TileButton("CAPTURE", "capture", () => _ = Go(nameof(CapturePage)));
        tiles1.Add(_enroll);
        tiles1.Add(_identify, 1);
        tiles1.Add(_capture, 2);
        var tiles2 = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
            },
            ColumnSpacing = 12,
        };
        _attribute = DemoUi.TileButton("ATTRIBUTE", "attribute", () => _ = AttributeAsync());
        tiles2.Add(_attribute);
        tiles2.Add(DemoUi.TileButton("SETTINGS", "settings", () => _ = Go(nameof(SettingsPage), false)), 1);
        tiles2.Add(DemoUi.TileButton("ABOUT", "about", () => _ = Go(nameof(AboutPage), false)), 2);

        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 28),
                Spacing = 12,
                Children =
                {
                    DemoUi.Logo(),
                    new Label
                    {
                        Text = "Face Recognition",
                        TextColor = DemoTheme.Text,
                        FontSize = 22,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                    },
                    _status,
                    _errorBanner,
                    tiles1,
                    tiles2,
                    new Label
                    {
                        Text = "Enrolled Face",
                        TextColor = DemoTheme.Text,
                        FontSize = 20,
                        Margin = new Thickness(0, 12, 0, 0),
                    },
                    _empty,
                    _people,
                },
            },
        };
        AbsoluteLayout.SetLayoutBounds(scroll, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(scroll, AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(_busyOverlay, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(_busyOverlay, AbsoluteLayoutFlags.All);
        _root.Children.Add(scroll);
        _root.Children.Add(_busyOverlay);
        Content = _root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_started)
        {
            _started = true;
            Paint();
            await SdkSession.BootstrapAsync();
        }

        Paint();
    }

    void Paint()
    {
        _status.Text = SdkSession.Message;
        _status.TextColor = SdkSession.Ready ? DemoTheme.Green : DemoTheme.Muted;
        var showError = !SdkSession.Ready && !SdkSession.Loading && !string.IsNullOrWhiteSpace(SdkSession.Message);
        _errorBanner.IsVisible = showError;
        _errorText.Text = SdkSession.Message;
        var enabled = SdkSession.Ready;
        SetTileEnabled(_enroll, enabled, () => _ = EnrollAsync());
        SetTileEnabled(_identify, enabled, () => _ = Go(nameof(IdentifyPage)));
        SetTileEnabled(_capture, enabled, () => _ = Go(nameof(CapturePage)));
        SetTileEnabled(_attribute, enabled, () => _ = AttributeAsync());

        _people.Children.Clear();
        foreach (var person in PersonStore.People)
        {
            _people.Add(PersonRow(person));
        }

        _empty.Text = PersonStore.People.Count == 0
            ? (SdkSession.Ready ? "No enrolled faces yet." : "")
            : "";
    }

    static void SetTileEnabled(Border tile, bool enabled, Action onClick)
    {
        tile.Opacity = enabled ? 1 : 0.45;
        tile.GestureRecognizers.Clear();
        if (!enabled)
        {
            return;
        }

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => onClick();
        tile.GestureRecognizers.Add(tap);
    }

    View PersonRow(EnrolledPerson person)
    {
        ImageSource? photo = null;
        if (!string.IsNullOrEmpty(person.ThumbnailBase64))
        {
            photo = ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(person.ThumbnailBase64)));
        }

        var thumb = new Border
        {
            WidthRequest = 60,
            HeightRequest = 60,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
            BackgroundColor = DemoTheme.Surface,
            Clip = new Microsoft.Maui.Controls.Shapes.EllipseGeometry
            {
                Center = new Point(30, 30),
                RadiusX = 30,
                RadiusY = 30,
            },
            Content = photo == null
                ? new Label
                {
                    Text = "👤",
                    FontSize = 22,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                }
                : new Image { Source = photo, Aspect = Aspect.AspectFill },
        };

        var row = new Grid
        {
            HeightRequest = 72,
            ColumnDefinitions =
            {
                new ColumnDefinition(72),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(48),
            },
            Padding = new Thickness(8, 0),
        };
        row.Add(thumb);
        row.Add(new Label
        {
            Text = person.Name,
            TextColor = DemoTheme.Text,
            FontSize = 16,
            VerticalTextAlignment = TextAlignment.Center,
        }, 1);
        var remove = new Button
        {
            Text = "✕",
            TextColor = DemoTheme.Muted,
            BackgroundColor = Colors.Transparent,
        };
        remove.Clicked += async (_, _) =>
        {
            await PersonStore.RemoveAsync(person.Id);
            Paint();
        };
        row.Add(remove, 2);
        return new Border
        {
            BackgroundColor = DemoTheme.Surface,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Content = row,
        };
    }

    async Task Go(string route, bool requiresSdk = true)
    {
        if (requiresSdk && !SdkSession.Ready)
        {
            await DisplayAlert("Face Recognition", SdkSession.Loading ? "Loading native SDK…" : "SDK not ready", "OK");
            return;
        }

        await Shell.Current.GoToAsync(route);
    }

    async Task EnrollAsync()
    {
        if (!SdkSession.Ready || _busy)
        {
            if (!SdkSession.Ready)
            {
                await DisplayAlert("Face Recognition", "SDK not ready", "OK");
            }

            return;
        }

        var photo = await MediaPicker.Default.PickPhotoAsync();
        if (photo == null)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await using var stream = await photo.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            // Native gallery: orient + enginePreparedImage (≤1280) before detect/crop/extract.
            var jpeg = GalleryImagePrep.PrepareEngineJpeg(memory.ToArray());
            var json = await FaceSdk.DetectAsync(jpeg, FaceSdk.DetectPose | FaceSdk.DetectLandmarks);
            var boxes = FaceJson.ParseDetect(json);
            if (boxes.Count != 1)
            {
                await DisplayAlert("Face Recognition", boxes.Count == 0 ? "No face detected!" : "Multiple face detected!", "OK");
                return;
            }

            var featureJson = await FaceSdk.ExtractCroppedFeatureAsync(jpeg, boxes[0]);
            var feature = FaceJson.FeatureBase64(featureJson);
            if (string.IsNullOrWhiteSpace(feature))
            {
                await DisplayAlert("Face Recognition", "Enrollment failed: no template", "OK");
                return;
            }

            string? thumb = null;
            try
            {
                var crop = await FaceSdk.CropFaceAsync(jpeg, boxes[0]);
                if (crop is { Length: > 0 and <= 1_500_000 })
                {
                    thumb = Convert.ToBase64String(crop);
                }
            }
            catch
            {
                if (jpeg.Length <= 1_500_000)
                {
                    thumb = Convert.ToBase64String(jpeg);
                }
            }

            await PersonStore.AddAsync(feature, thumb);
            Paint();
            await DisplayAlert("Face Recognition", "Person enrolled!", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Face Recognition", "Enrollment failed: " + ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    async Task AttributeAsync()
    {
        if (!SdkSession.Ready)
        {
            await DisplayAlert("Face Recognition", "SDK not ready", "OK");
            return;
        }

        var photo = await MediaPicker.Default.PickPhotoAsync();
        if (photo == null)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await using var stream = await photo.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            // Same prepared buffer for detect + AttributePage CropFace (box space = JPEG pixels).
            var jpeg = GalleryImagePrep.PrepareEngineJpeg(memory.ToArray());
            var json = await FaceSdk.DetectAsync(jpeg, SettingsStore.Current.AttributeFlags);
            var boxes = FaceJson.ParseDetect(json);
            if (boxes.Count != 1)
            {
                await DisplayAlert("Face Recognition", boxes.Count == 0 ? "No face detected!" : "Multiple face detected!", "OK");
                return;
            }

            PendingAttribute.Jpeg = jpeg;
            PendingAttribute.Box = boxes[0];
            await Shell.Current.GoToAsync(nameof(AttributePage));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Face Recognition", "Could not load image: " + ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    void SetBusy(bool busy)
    {
        _busy = busy;
        _busyOverlay.IsVisible = busy;
        _busyOverlay.IsRunning = busy;
    }
}
