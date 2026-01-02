using System.IO;
using System.Windows.Threading;
using ScreenRecorderLib;
using VideoSnip.Models;

namespace VideoSnip.Services;

public enum RecordingState
{
    Idle,
    Selecting,
    Recording,
    Stopping
}

public class RecordingResult
{
    public string TempFilePath { get; set; } = "";
    public string SuggestedFileName { get; set; } = "";
    public string DefaultFolder { get; set; } = "";
    public TimeSpan Duration { get; set; }
}

public class RecordingController : IDisposable
{
    private Recorder? _recorder;
    private RecordingRegion? _region;
    private string? _tempPath;
    private string? _suggestedFileName;
    private DateTime _startTime;
    private readonly DispatcherTimer _durationTimer;
    private bool _disposed;
    private TaskCompletionSource<bool>? _recordingComplete;
    private string? _lastError;
    private RecordingBorderService? _borderService;
    private bool _isPaused;
    private TimeSpan _pausedDuration;
    private DateTime _pauseStartTime;

    public RecordingState State { get; private set; } = RecordingState.Idle;
    public TimeSpan Duration { get; private set; }
    public bool IsPaused => _isPaused;

    public event Action<RecordingState>? StateChanged;
    public event Action<TimeSpan>? DurationUpdated;

    public RecordingController()
    {
        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _durationTimer.Tick += (s, e) =>
        {
            if (!_isPaused)
            {
                Duration = DateTime.Now - _startTime - _pausedDuration;
                DurationUpdated?.Invoke(Duration);
            }
        };
    }

    public Task<(bool Success, string? Error)> StartRecordingAsync(RecordingRegion region)
    {
        if (State != RecordingState.Idle)
        {
            return Task.FromResult<(bool, string?)>((false, "Recording already in progress"));
        }

        _region = region;
        _lastError = null;
        _isPaused = false;
        _pausedDuration = TimeSpan.Zero;

        // Generate temp path and suggested filename
        var tempFolder = Path.GetTempPath();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _suggestedFileName = $"{timestamp}.mp4";
        _tempPath = Path.Combine(tempFolder, $"VideoSnip_{Guid.NewGuid():N}.mp4");

        try
        {
            // Configure recorder options
            var sourceRect = new ScreenRect(region.X, region.Y, region.Width, region.Height);

            var options = new RecorderOptions
            {
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video,
                    SourceRect = sourceRect
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Bitrate = 8000 * 1000, // 8 Mbps
                    Framerate = 30,
                    IsFixedFramerate = false,
                    Encoder = new H264VideoEncoder
                    {
                        BitrateMode = H264BitrateControlMode.Quality,
                        EncoderProfile = H264Profile.Main
                    }
                },
                MouseOptions = new MouseOptions
                {
                    IsMousePointerEnabled = true
                },
                AudioOptions = new AudioOptions
                {
                    IsAudioEnabled = false
                }
            };

            _recorder = Recorder.CreateRecorder(options);
            _recorder.OnRecordingComplete += OnRecordingComplete;
            _recorder.OnRecordingFailed += OnRecordingFailed;
            _recorder.OnStatusChanged += OnStatusChanged;

            _recordingComplete = new TaskCompletionSource<bool>();
            _recorder.Record(_tempPath);

            // Show recording border
            _borderService = new RecordingBorderService();
            _borderService.ShowBorder(region);

            _startTime = DateTime.Now;
            _durationTimer.Start();

            SetState(RecordingState.Recording);
            return Task.FromResult<(bool, string?)>((true, null));
        }
        catch (Exception ex)
        {
            Cleanup();
            return Task.FromResult<(bool, string?)>((false, $"Failed to start recorder: {ex.Message}"));
        }
    }

    private void OnStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Recording status: {e.Status}");
    }

    private void OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        _lastError = e.Error;
        System.Diagnostics.Debug.WriteLine($"Recording failed: {e.Error}");
        _recordingComplete?.TrySetResult(false);
    }

    private void OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Recording complete: {e.FilePath}");
        _recordingComplete?.TrySetResult(true);
    }

    public void Pause()
    {
        if (State != RecordingState.Recording || _isPaused) return;

        _recorder?.Pause();
        _isPaused = true;
        _pauseStartTime = DateTime.Now;
    }

    public void Resume()
    {
        if (State != RecordingState.Recording || !_isPaused) return;

        _recorder?.Resume();
        _pausedDuration += DateTime.Now - _pauseStartTime;
        _isPaused = false;
    }

    public async Task<RecordingResult?> StopRecordingAsync()
    {
        if (State != RecordingState.Recording) return null;

        SetState(RecordingState.Stopping);
        _durationTimer.Stop();
        var duration = Duration;

        try
        {
            _recorder?.Stop();

            // Wait for recording to complete (with timeout)
            if (_recordingComplete != null)
            {
                var completedTask = await Task.WhenAny(_recordingComplete.Task, Task.Delay(5000));
                if (completedTask != _recordingComplete.Task)
                {
                    System.Diagnostics.Debug.WriteLine("Recording stop timed out");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping recording: {ex.Message}");
        }

        var result = new RecordingResult
        {
            TempFilePath = _tempPath ?? "",
            SuggestedFileName = _suggestedFileName ?? "recording.mp4",
            DefaultFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Duration = duration
        };

        Cleanup();
        SetState(RecordingState.Idle);

        return result;
    }

    private void SetState(RecordingState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    private void Cleanup()
    {
        // Hide recording border
        _borderService?.Dispose();
        _borderService = null;

        if (_recorder != null)
        {
            _recorder.OnRecordingComplete -= OnRecordingComplete;
            _recorder.OnRecordingFailed -= OnRecordingFailed;
            _recorder.OnStatusChanged -= OnStatusChanged;
            _recorder.Dispose();
            _recorder = null;
        }

        _recordingComplete = null;
        _region = null;
        _tempPath = null;
        _suggestedFileName = null;
    }

    private void CleanupTempFile()
    {
        if (!string.IsNullOrEmpty(_tempPath))
        {
            try
            {
                if (File.Exists(_tempPath))
                {
                    File.Delete(_tempPath);
                }
            }
            catch
            {
                // Ignore temp file cleanup errors
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _durationTimer.Stop();

        if (State == RecordingState.Recording)
        {
            _recorder?.Stop();
        }

        CleanupTempFile();
        Cleanup();

        GC.SuppressFinalize(this);
    }
}
