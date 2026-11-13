using System.Text.Json;

namespace FaceRecognition.Net.Maui;

public sealed class FaceBox
{
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
    public double? Yaw { get; init; }
    public double? Pitch { get; init; }
    public double? Roll { get; init; }
    public double? Liveness { get; init; }
    public string? LivenessLabel { get; init; }
    public string? GenderLabel { get; init; }
    public string? EmotionLabel { get; init; }
    public string? MaskLabel { get; init; }
    public string? EyesLeftLabel { get; init; }
    public string? EyesRightLabel { get; init; }
    public string? GlassesLabel { get; init; }
    public string? SunglassesLabel { get; init; }
    public string? OcclusionLabel { get; init; }
    public double? LeftEyeClosed { get; init; }
    public double? RightEyeClosed { get; init; }
    public double? Age { get; init; }
    public double? FaceQuality { get; init; }
    public double? FaceLuminance { get; init; }
    public string? QualityLabel { get; init; }
    public IReadOnlyList<double> Landmarks { get; init; } = Array.Empty<double>();
    public IReadOnlyDictionary<string, string> Attributes { get; init; } =
        new Dictionary<string, string>();

    public double Width => X2 - X1;
    public double Height => Y2 - Y1;

    public FaceBox WithLiveness(double? liveness, string? label) =>
        Clone(liveness: liveness, livenessLabel: label);

    public FaceBox WithEyes(
        string? leftLabel,
        string? rightLabel,
        double? leftClosed,
        double? rightClosed) =>
        Clone(
            eyesLeft: leftLabel,
            eyesRight: rightLabel,
            leftClosed: leftClosed,
            rightClosed: rightClosed);

    FaceBox Clone(
        double? liveness = null,
        string? livenessLabel = null,
        string? eyesLeft = null,
        string? eyesRight = null,
        double? leftClosed = null,
        double? rightClosed = null) =>
        new()
        {
            X1 = X1,
            Y1 = Y1,
            X2 = X2,
            Y2 = Y2,
            Yaw = Yaw,
            Pitch = Pitch,
            Roll = Roll,
            Liveness = liveness ?? Liveness,
            LivenessLabel = livenessLabel ?? LivenessLabel,
            GenderLabel = GenderLabel,
            EmotionLabel = EmotionLabel,
            MaskLabel = MaskLabel,
            EyesLeftLabel = eyesLeft ?? EyesLeftLabel,
            EyesRightLabel = eyesRight ?? EyesRightLabel,
            GlassesLabel = GlassesLabel,
            SunglassesLabel = SunglassesLabel,
            OcclusionLabel = OcclusionLabel,
            LeftEyeClosed = leftClosed ?? LeftEyeClosed,
            RightEyeClosed = rightClosed ?? RightEyeClosed,
            Age = Age,
            FaceQuality = FaceQuality,
            FaceLuminance = FaceLuminance,
            QualityLabel = QualityLabel,
            Landmarks = Landmarks,
            Attributes = Attributes,
        };
}

public sealed class AppSettings
{
    public bool FrontCamera { get; init; } = true;
    public double LivenessThreshold { get; init; } = 0.8;
    public int LivenessLevel { get; init; }
    public double IdentifyThreshold { get; init; } = 0.67;
    public double YawThreshold { get; init; } = 40;
    public double RollThreshold { get; init; } = 40;
    public double PitchThreshold { get; init; } = 40;
    public double EyeCloseThreshold { get; init; } = 0.5;

    public int CaptureFlags
    {
        get
        {
            var flags = FaceSdk.DetectPose | FaceSdk.DetectLandmarks | FaceSdk.DetectEyes
                | FaceSdk.DetectMask | FaceSdk.DetectLiveness;
            if (LivenessLevel == 0)
            {
                flags |= FaceSdk.DetectLivenessAccurate;
            }

            return flags;
        }
    }

    /// <summary>Android FaceDetectionParam.allAttributes() + check_liveness_level.</summary>
    public int AttributeFlags
    {
        get
        {
            var flags = FaceSdk.DetectPose | FaceSdk.DetectLandmarks
                | FaceSdk.DetectAge | FaceSdk.DetectGender
                | FaceSdk.DetectEmotion | FaceSdk.DetectMask
                | FaceSdk.DetectQuality | FaceSdk.DetectFaceQuality
                | FaceSdk.DetectEyes | FaceSdk.DetectLiveness | FaceSdk.DetectGlasses;
            if (LivenessLevel == 0)
            {
                flags |= FaceSdk.DetectLivenessAccurate;
            }

            return flags;
        }
    }

    public int LivenessFlags
    {
        get
        {
            var flags = FaceSdk.DetectLiveness;
            if (LivenessLevel == 0)
            {
                flags |= FaceSdk.DetectLivenessAccurate;
            }

            return flags;
        }
    }
}

public static class SettingsStore
{
    const string Key = "face_settings_v1";

    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        var json = Preferences.Get(Key, "");
        if (string.IsNullOrWhiteSpace(json))
        {
            Current = new AppSettings();
            return;
        }

        try
        {
            Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Current = settings;
        Preferences.Set(Key, JsonSerializer.Serialize(settings));
    }

    public static void RestoreDefaults() => Save(new AppSettings());
}

public sealed class EnrolledPerson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FeatureBase64 { get; set; } = "";
    public string? ThumbnailBase64 { get; set; }
}

public static class PersonStore
{
    static readonly SemaphoreSlim Gate = new(1, 1);
    static List<EnrolledPerson> _people = new();
    static bool _loaded;

    public static IReadOnlyList<EnrolledPerson> People => _people;

    public static async Task LoadAsync()
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_loaded)
            {
                return;
            }

            var path = Path.Combine(FileSystem.AppDataDirectory, "people.json");
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                _people = JsonSerializer.Deserialize<List<EnrolledPerson>>(json) ?? new();
            }

            _loaded = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task AddAsync(string featureBase64, string? thumbnailBase64)
    {
        await LoadAsync().ConfigureAwait(false);
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _people.Add(new EnrolledPerson
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = "Person" + Random.Shared.Next(10000, 20000),
                FeatureBase64 = featureBase64,
                ThumbnailBase64 = thumbnailBase64,
            });
            await WriteAsync().ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task RemoveAsync(string id)
    {
        await LoadAsync().ConfigureAwait(false);
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _people.RemoveAll(p => p.Id == id);
            await WriteAsync().ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task ClearAsync()
    {
        await LoadAsync().ConfigureAwait(false);
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _people.Clear();
            await WriteAsync().ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    static Task WriteAsync()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, "people.json");
        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(_people));
    }
}
