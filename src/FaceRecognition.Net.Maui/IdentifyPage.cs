using Camera.MAUI;
using Microsoft.Maui.Layouts;

namespace FaceRecognition.Net.Maui;

public class IdentifyPage : ContentPage
{
    readonly CameraView _camera = new()
    {
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        AutoStartPreview = false,
    };
    readonly IdentifyOverlayDrawable _drawable = new();
    readonly GraphicsView _overlay;
    readonly ActivityIndicator _spinner = new()
    {
        IsRunning = true,
        Color = DemoTheme.Accent,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
    };
    readonly AbsoluteLayout _stage = new();
    readonly Button _back = DemoUi.BackButton(() => _ = Shell.Current.GoToAsync(".."));
    readonly TaskCompletionSource _camerasReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly List<FaceBox> _lastLiveness = new();
    List<FaceBox> _boxes = new();
    byte[]? _lastJpeg;
    bool _visible;
    bool _attached;
    bool _looping;
    bool _snapBusy;
    bool _livBusy;
    bool _front;
    bool _workerReady;
    bool _recognized;
    bool _confirming;
    long _lastLivenessMs;
    int _life;
    double _frameW = 480;
    double _frameH = 640;

    public IdentifyPage()
    {
        Title = "Identify";
        BackgroundColor = Colors.Black;
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetBackgroundColor(this, DemoTheme.Bg);
        _overlay = new GraphicsView
        {
            Drawable = _drawable,
            InputTransparent = true,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        AbsoluteLayout.SetLayoutBounds(_overlay, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(_overlay, AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(_spinner, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(_spinner, AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(_back, new Rect(16, 52, 44, 44));
        AbsoluteLayout.SetLayoutFlags(_back, AbsoluteLayoutFlags.None);
        _stage.Children.Add(_overlay);
        _stage.Children.Add(_spinner);
        _stage.Children.Add(_back);
        Content = _stage;
        _camera.CamerasLoaded += (_, _) => _camerasReady.TrySetResult();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _visible = true;
        _recognized = false;
        _confirming = false;
        var life = ++_life;
        if (!_attached)
        {
            AbsoluteLayout.SetLayoutBounds(_camera, new Rect(0, 0, 1, 1));
            AbsoluteLayout.SetLayoutFlags(_camera, AbsoluteLayoutFlags.All);
            _stage.Children.Insert(0, _camera);
            _attached = true;
        }

        Dispatcher.Dispatch(async () =>
        {
            if (life == _life)
            {
                await StartAsync();
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _life++;
        _ = LeaveAsync(stopCamera: true);
    }

    async Task StartAsync()
    {
        _workerReady = false;
        _spinner.IsVisible = true;
        _spinner.IsRunning = true;
        _drawable.Boxes = Array.Empty<FaceBox>();
        _overlay.Invalidate();

        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
        {
            _spinner.IsVisible = false;
            await DisplayAlert("Identify", "Camera permission is required", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        if (_camera.NumCamerasDetected == 0)
        {
            await Task.WhenAny(_camerasReady.Task, Task.Delay(2500));
        }

        _front = SettingsStore.Current.FrontCamera;
        // Camera.MAUI iOS preview already matches engine X (like Flutter camera).
        // Only Android front JPEGs are unmirrored while preview is mirrored → flip overlay.
        _drawable.Mirror = _front && DeviceInfo.Current.Platform == DevicePlatform.Android;
        _drawable.LivenessThreshold = SettingsStore.Current.LivenessThreshold;
        var camera = _camera.Cameras.FirstOrDefault(c => _front
                ? c.Position == CameraPosition.Front
                : c.Position == CameraPosition.Back)
            ?? _camera.Cameras.FirstOrDefault();
        if (camera == null)
        {
            _spinner.IsVisible = false;
            await DisplayAlert("Identify", "No camera found", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        _camera.Camera = camera;
        await FaceSdk.StopVideoWorkerAsync();
        if (await _camera.StartCameraAsync(new Size(1280, 720)) != CameraResult.Success)
        {
            await Task.Delay(500);
            if (await _camera.StartCameraAsync(new Size(1280, 720)) != CameraResult.Success)
            {
                _spinner.IsVisible = false;
                await DisplayAlert("Identify", "Could not start the camera", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }
        }

        if (PersonStore.People.Count == 0)
        {
            _spinner.IsVisible = false;
            await DisplayAlert("Identify", "Enroll a face first", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        var threshold = (float)SettingsStore.Current.IdentifyThreshold;
        var started = await FaceSdk.StartVideoWorkerAsync(threshold);
        var synced = await FaceSdk.SyncDatabaseAsync(
            PersonStore.People.Select(p => p.FeatureBase64).ToList(),
            threshold);
        _workerReady = started == FaceSdk.Success && synced == FaceSdk.Success;
        _spinner.IsVisible = false;
        _spinner.IsRunning = false;
        if (!_workerReady)
        {
            await DisplayAlert("Identify", "VideoWorker failed (" + started + "/" + synced + ")", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        RaiseOverlays();
        for (var i = 0; i < 6; i++)
        {
            await Task.Delay(200);
            RaiseOverlays();
            _overlay.Invalidate();
        }

        _ = LoopAsync(_life);
    }

    void RaiseOverlays()
    {
        _camera.ZIndex = 0;
        _overlay.ZIndex = 20;
        _spinner.ZIndex = 25;
        _back.ZIndex = 40;
        OverlayZOrder.BringToFront(_overlay, _spinner, _back);
    }

    /// <summary>Flutter IdentifySession.leave / Android leaveIdentify — stop ingest, drain liveness, then stop worker.</summary>
    void Leave()
    {
        _workerReady = false;
        _visible = false;
    }

    async Task LeaveAsync(bool stopCamera)
    {
        Leave();
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while ((_livBusy || _snapBusy) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(40);
        }

        await FaceSdk.StopVideoWorkerAsync();
        if (stopCamera && _attached)
        {
            await _camera.StopCameraAsync();
        }
    }

    async Task LoopAsync(int life)
    {
        if (_looping)
        {
            return;
        }

        _looping = true;
        try
        {
            while (_visible && life == _life && !_recognized)
            {
                // Drain VideoWorker before heavy Detect — same as CapturePage.
                for (var i = 0; i < 8 && !_recognized; i++)
                {
                    var json = await FaceSdk.PollEventAsync();
                    if (string.IsNullOrEmpty(json))
                    {
                        break;
                    }

                    OnWorkerEvent(json);
                }

                if (_workerReady && !_snapBusy && !_recognized)
                {
                    _snapBusy = true;
                    try
                    {
                        var jpeg = await SnapAsync();
                        if (jpeg != null)
                        {
                            var live = await FaceSdk.IngestLiveCameraFrameAsync(jpeg, _front);
                            if (live.Ingested && live.Width > 0 && live.Height > 0)
                            {
                                _frameW = live.Width;
                                _frameH = live.Height;
                                _drawable.FrameWidth = _frameW;
                                _drawable.FrameHeight = _frameH;
                            }

                            if (live.Ingested)
                            {
                                try
                                {
                                    var exported = await FaceSdk.ExportLastLiveFrameAsync();
                                    if (exported.Ingested && exported.Jpeg is { Length: > 0 })
                                    {
                                        _lastJpeg = exported.Jpeg;
                                        var now = Environment.TickCount64;
                                        if (!_livBusy && now - _lastLivenessMs >= 500)
                                        {
                                            _lastLivenessMs = now;
                                            _livBusy = true;
                                            try
                                            {
                                                var livJson = await FaceSdk.DetectAsync(
                                                    exported.Jpeg,
                                                    SettingsStore.Current.LivenessFlags);
                                                var liv = FaceJson.ParseDetect(livJson);
                                                if (liv.Count > 0)
                                                {
                                                    _lastLiveness.Clear();
                                                    _lastLiveness.AddRange(liv);
                                                }
                                            }
                                            finally
                                            {
                                                _livBusy = false;
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    _livBusy = false;
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        _snapBusy = false;
                    }
                }

                await Task.Delay(50);
            }
        }
        finally
        {
            _looping = false;
        }
    }

    void OnWorkerEvent(string json)
    {
        if (_recognized)
        {
            return;
        }

        var matchEv = WorkerJson.ParseMatch(json);
        if (matchEv != null)
        {
            if (matchEv.Value.Matched && matchEv.Value.PersonIndex != null)
            {
                _ = ConfirmAsync(matchEv.Value.PersonIndex.Value, matchEv.Value.Score ?? 0);
            }

            return;
        }

        var tracking = WorkerJson.ParseTracking(json);
        if (tracking == null)
        {
            return;
        }

        if (tracking.Value.FrameWidth > 0 && tracking.Value.FrameHeight > 0)
        {
            _frameW = tracking.Value.FrameWidth;
            _frameH = tracking.Value.FrameHeight;
            _drawable.FrameWidth = _frameW;
            _drawable.FrameHeight = _frameH;
        }

        var next = tracking.Value.Faces.Select(WorkerJson.ToFaceBox).ToList();
        next = CaptureLogic.MergeLiveness(next, _lastLiveness);
        _boxes = next;
        _drawable.Boxes = next;
        _overlay.Invalidate();

        foreach (var face in tracking.Value.Faces)
        {
            if (face.Matched && face.PersonIndex != null)
            {
                _ = ConfirmAsync(face.PersonIndex.Value, face.Score ?? 0);
                break;
            }
        }
    }

    async Task ConfirmAsync(int personIndex, double score)
    {
        if (_recognized || _confirming)
        {
            return;
        }

        _confirming = true;
        // Snapshot state before leave — LeaveAsync stops camera/worker.
        var person = ResolvePerson(personIndex);
        var faceBox = _boxes.Count > 0 ? _boxes[0] : null;
        var jpeg = _lastJpeg;
        var frameW = _frameW;
        var frameH = _frameH;
        var livCopy = _lastLiveness.ToList();

        await LeaveAsync(stopCamera: true); // Android leaveIdentify drains then stops worker
        try
        {
            if (person == null || faceBox == null)
            {
                await ResumeIdentifyAsync();
                return;
            }

            _recognized = true;

            byte[]? crop = null;
            IReadOnlyList<(double X, double Y)> marks = Array.Empty<(double, double)>();
            if (jpeg != null)
            {
                try
                {
                    crop = await FaceSdk.CropFaceAsync(jpeg, faceBox);
                    var (srcW, srcH) = frameW > 0 && frameH > 0
                        ? ((int)frameW, (int)frameH)
                        : CaptureLogic.JpegSize(jpeg);
                    marks = CaptureLogic.MapLandmarksToCrop(faceBox, srcW, srcH, 200, 200);
                }
                catch
                {
                    crop = jpeg;
                }
            }

            if (livCopy.Count > 0)
            {
                faceBox = CaptureLogic.MergeLiveness([faceBox], livCopy)[0];
            }

            PendingIdentifyResult.IdentifiedCropJpeg = crop;
            PendingIdentifyResult.EnrolledThumbBase64 = person.ThumbnailBase64;
            PendingIdentifyResult.PersonName = person.Name;
            PendingIdentifyResult.Similarity = score;
            PendingIdentifyResult.Box = faceBox;
            PendingIdentifyResult.CropLandmarks = marks;
            await Shell.Current.GoToAsync(nameof(IdentifyResultPage));
        }
        catch
        {
            await ResumeIdentifyAsync();
        }
    }

    async Task ResumeIdentifyAsync()
    {
        _confirming = false;
        _recognized = false;
        if (!IsVisible)
        {
            return;
        }

        _visible = true;
        await StartAsync();
    }

    static EnrolledPerson? ResolvePerson(int personIndex)
    {
        var list = PersonStore.People;
        if (personIndex >= 0 && personIndex < list.Count)
        {
            return list[personIndex];
        }

        if (personIndex - 1 >= 0 && personIndex - 1 < list.Count)
        {
            return list[personIndex - 1];
        }

        return null;
    }

    Task<byte[]?> SnapAsync() => CameraSnap.CaptureJpegAsync(_camera);
}
