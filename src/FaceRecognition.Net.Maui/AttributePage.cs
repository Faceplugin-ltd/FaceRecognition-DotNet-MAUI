namespace FaceRecognition.Net.Maui;

public class AttributePage : ContentPage
{
    public AttributePage()
    {
        Title = "Attribute Result";
        BackgroundColor = DemoTheme.Bg;
        Shell.SetBackgroundColor(this, DemoTheme.Bg);
        Shell.SetTitleColor(this, DemoTheme.Text);

        var box = PendingAttribute.Box;
        var jpeg = PendingAttribute.Jpeg;
        var body = new VerticalStackLayout
        {
            Children =
            {
                new Label
                {
                    Text = "Attribute Result",
                    TextColor = DemoTheme.Text,
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 16, 0, 0),
                    HeightRequest = 48,
                },
            },
        };

        var cropHost = new Grid
        {
            WidthRequest = 240,
            HeightRequest = 240,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 16, 0, 8),
            BackgroundColor = DemoTheme.Bg,
        };
        body.Add(new Border
        {
            WidthRequest = 240,
            HeightRequest = 240,
            HorizontalOptions = LayoutOptions.Center,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            BackgroundColor = DemoTheme.Bg,
            Content = cropHost,
            Margin = new Thickness(0, 16, 0, 8),
        });

        Content = new ScrollView { Content = body };
        _ = LoadAsync(cropHost, body, jpeg, box);
    }

    static async Task LoadAsync(Grid cropHost, VerticalStackLayout body, byte[]? jpeg, FaceBox? box)
    {
        if (box == null || jpeg == null)
        {
            body.Add(new Label
            {
                Text = "No face",
                TextColor = DemoTheme.Text,
                Margin = new Thickness(16),
            });
            return;
        }

        byte[]? crop = null;
        IReadOnlyList<(double X, double Y)> marks = Array.Empty<(double, double)>();
        try
        {
            crop = await FaceSdk.CropFaceAsync(jpeg, box);
            // JpegSize matches engine-prepared JPEG (≤1280, upright) used for detect.
            var (srcW, srcH) = CaptureLogic.JpegSize(jpeg);
            marks = CaptureLogic.MapLandmarksToCrop(box, srcW, srcH, 200, 200);
        }
        catch
        {
            crop = jpeg;
        }

        if (crop != null)
        {
            var bytes = crop;
            cropHost.Add(new Image
            {
                Source = ImageSource.FromStream(() => new MemoryStream(bytes)),
                Aspect = Aspect.AspectFit,
            });
        }

        if (marks.Count > 0)
        {
            cropHost.Add(new GraphicsView
            {
                Drawable = new LandmarkDrawable(marks),
                InputTransparent = true,
            });
        }

        body.Add(ResultDetails.BuildList(ResultDetails.Rows(box, SettingsStore.Current, includeMatch: false)));
    }
}
