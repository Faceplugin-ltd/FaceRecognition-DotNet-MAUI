namespace FaceRecognition.Net.Maui;

public class IdentifyResultPage : ContentPage
{
    public IdentifyResultPage()
    {
        Title = "Identify Result";
        BackgroundColor = DemoTheme.Bg;
        Shell.SetBackgroundColor(this, DemoTheme.Bg);
        Shell.SetTitleColor(this, DemoTheme.Text);

        var identified = PendingIdentifyResult.IdentifiedCropJpeg;
        var enrolled = PendingIdentifyResult.EnrolledThumbBase64;
        var box = PendingIdentifyResult.Box;
        var marks = PendingIdentifyResult.CropLandmarks;

        var identifiedCard = PhotoCol(
            "Identified",
            identified == null
                ? null
                : ImageSource.FromStream(() => new MemoryStream(identified)),
            marks,
            null);
        var enrolledCard = PhotoCol(
            "Enrolled",
            string.IsNullOrEmpty(enrolled)
                ? null
                : ImageSource.FromStream(() => new MemoryStream(Convert.FromBase64String(enrolled))),
            Array.Empty<(double, double)>(),
            "ID: " + PendingIdentifyResult.PersonName);

        var photos = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 16,
            Padding = new Thickness(16, 8),
        };
        photos.Add(identifiedCard);
        photos.Add(enrolledCard, 1);

        var body = new VerticalStackLayout
        {
            Children =
            {
                new Label
                {
                    Text = "Identify Result",
                    TextColor = DemoTheme.Text,
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 16, 0, 0),
                    HeightRequest = 48,
                },
                photos,
                new Label
                {
                    Text = "Similarity: " + PendingIdentifyResult.Similarity,
                    TextColor = DemoTheme.Text,
                    FontSize = 18,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0),
                },
            },
        };

        if (box != null)
        {
            body.Add(ResultDetails.BuildList(
                ResultDetails.Rows(box, SettingsStore.Current, includeMatch: false)));
        }

        Content = new ScrollView { Content = body };
    }

    static View PhotoCol(string caption, ImageSource? source, IReadOnlyList<(double X, double Y)> marks, string? idLine)
    {
        var image = new Image
        {
            Source = source,
            WidthRequest = 140,
            HeightRequest = 140,
            Aspect = Aspect.AspectFill,
            BackgroundColor = DemoTheme.Bg,
        };
        var stage = new Grid { WidthRequest = 140, HeightRequest = 140, HorizontalOptions = LayoutOptions.Center };
        stage.Add(image);
        if (marks.Count > 0)
        {
            stage.Add(new GraphicsView
            {
                Drawable = new LandmarkDrawable(marks),
                WidthRequest = 140,
                HeightRequest = 140,
                InputTransparent = true,
            });
        }

        var col = new VerticalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(8),
            BackgroundColor = DemoTheme.Surface,
            Children =
            {
                stage,
                new Label
                {
                    Text = caption,
                    TextColor = DemoTheme.Text,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 5),
                },
            },
        };
        if (!string.IsNullOrEmpty(idLine))
        {
            col.Add(new Label
            {
                Text = idLine,
                TextColor = DemoTheme.Muted,
                FontSize = 13,
                HorizontalTextAlignment = TextAlignment.Center,
            });
        }

        return new Border
        {
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            BackgroundColor = DemoTheme.Surface,
            Content = col,
        };
    }
}

public class LandmarkDrawable : IDrawable
{
    readonly IReadOnlyList<(double X, double Y)> _marks;

    public LandmarkDrawable(IReadOnlyList<(double X, double Y)> marks) => _marks = marks;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // Marks are in 200×200 crop space; scale to view.
        var sx = dirtyRect.Width / 200f;
        var sy = dirtyRect.Height / 200f;
        canvas.FillColor = Color.FromArgb("#00E5FF");
        foreach (var (x, y) in _marks)
        {
            canvas.FillCircle((float)(x * sx), (float)(y * sy), 3);
        }
    }
}
