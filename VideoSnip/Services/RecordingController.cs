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

    // Maximum width supported by MediaFoundation H.264 encoder
    private const int MaxEncoderWidth = 4096;

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
            System.Diagnostics.Debug.WriteLine($"Starting recording: Region=({region.X},{region.Y}) Size={region.Width}x{region.Height} Mode={region.Mode}");

            // Check if resolution exceeds encoder limits and calculate scaled dimensions
            int captureWidth = region.Width;
            int captureHeight = region.Height;
            int outputWidth = region.Width;
            int outputHeight = region.Height;

            if (region.Width > MaxEncoderWidth)
            {
                // Scale down to max supported width while maintaining aspect ratio
                double scale = (double)MaxEncoderWidth / region.Width;
                outputWidth = MaxEncoderWidth;
                outputHeight = (int)(region.Height * scale);
                // Ensure even dimensions for H.264
                outputWidth = (outputWidth / 2) * 2;
                outputHeight = (outputHeight / 2) * 2;
                System.Diagnostics.Debug.WriteLine($"Resolution {region.Width}x{region.Height} exceeds encoder limit. Scaling output to {outputWidth}x{outputHeight}");
            }

            // Calculate appropriate bitrate and framerate based on OUTPUT resolution
            int pixels = outputWidth * outputHeight;
            int bitrate;
            int framerate;

            if (pixels > 4000000) // 4K+
            {
                bitrate = 20000 * 1000; // 20 Mbps
                framerate = 24; // Lower framerate for 4K
            }
            else if (pixels > 2000000) // 1440p
            {
                bitrate = 15000 * 1000; // 15 Mbps
                framerate = 30;
            }
            else // 1080p and below
            {
                bitrate = 8000 * 1000; // 8 Mbps
                framerate = 30;
            }

            System.Diagnostics.Debug.WriteLine($"Output Resolution: {outputWidth}x{outputHeight} ({pixels} pixels), Bitrate: {bitrate / 1000000} Mbps, Framerate: {framerate}");

            RecorderOptions options;

            // For large resolutions, try software encoding as hardware may not support it
            bool useSoftwareEncoding = pixels > 4000000;
            System.Diagnostics.Debug.WriteLine($"Using {(useSoftwareEncoding ? "SOFTWARE" : "HARDWARE")} encoding");

            if (region.Mode == VideoCaptureMode.FullScreen)
            {
                // For full screen, use SourceRect (DisplayRecordingSource has issues)
                System.Diagnostics.Debug.WriteLine($"Full screen mode - using SourceRect: (0,0) {region.Width}x{region.Height}");
                var sourceRect = new ScreenRect(0, 0, region.Width, region.Height);

                var outputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video,
                    SourceRect = sourceRect
                };

                // If resolution exceeds encoder limits, scale output down
                if (outputWidth != region.Width || outputHeight != region.Height)
                {
                    outputOptions.OutputFrameSize = new ScreenSize(outputWidth, outputHeight);
                    System.Diagnostics.Debug.WriteLine($"Scaling output from {region.Width}x{region.Height} to {outputWidth}x{outputHeight}");
                }

                options = new RecorderOptions
                {
                    OutputOptions = outputOptions,
                    VideoEncoderOptions = new VideoEncoderOptions
                    {
                        Bitrate = bitrate,
                        Framerate = framerate,
                        IsFixedFramerate = false
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
            }
            else
            {
                // For region/window capture, use SourceRect with aligned dimensions
                int alignedWidth = (region.Width / 2) * 2;   // Align to even number
                int alignedHeight = (region.Height / 2) * 2;
                var sourceRect = new ScreenRect(region.X, region.Y, alignedWidth, alignedHeight);
                System.Diagnostics.Debug.WriteLine($"Region/Window mode - SourceRect: {region.X},{region.Y} {alignedWidth}x{alignedHeight}");

                var outputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video,
                    SourceRect = sourceRect
                };

                // If resolution exceeds encoder limits, scale output down
                if (alignedWidth > MaxEncoderWidth)
                {
                    double scale = (double)MaxEncoderWidth / alignedWidth;
                    int scaledWidth = (MaxEncoderWidth / 2) * 2;
                    int scaledHeight = ((int)(alignedHeight * scale) / 2) * 2;
                    outputOptions.OutputFrameSize = new ScreenSize(scaledWidth, scaledHeight);
                    System.Diagnostics.Debug.WriteLine($"Scaling region output from {alignedWidth}x{alignedHeight} to {scaledWidth}x{scaledHeight}");
                }

                options = new RecorderOptions
                {
                    OutputOptions = outputOptions,
                    VideoEncoderOptions = new VideoEncoderOptions
                    {
                        Bitrate = bitrate,
                        Framerate = framerate,
                        IsFixedFramerate = false,
                        IsHardwareEncodingEnabled = !useSoftwareEncoding,
                        Encoder = new H264VideoEncoder
                        {
                            BitrateMode = H264BitrateControlMode.UnconstrainedVBR,
                            EncoderProfile = H264Profile.High
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
            }

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

        // Check file size periodically when recording
        if (e.Status == RecorderStatus.Recording && !string.IsNullOrEmpty(_tempPath))
        {
            try
            {
                var fileInfo = new FileInfo(_tempPath);
                if (fileInfo.Exists)
                {
                    System.Diagnostics.Debug.WriteLine($"Temp file size: {fileInfo.Length} bytes");
                }
            }
            catch { }
        }
    }

    private void OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        _lastError = e.Error;
        System.Diagnostics.Debug.WriteLine($"RECORDING FAILED: {e.Error}");
        System.Windows.MessageBox.Show($"Recording failed: {e.Error}", "Video Snip Error",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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

    /// <summary>
    /// Cancels the current recording and discards the temp file without saving.
    /// Used for restart functionality.
    /// </summary>
    public async Task CancelRecordingAsync()
    {
        if (State != RecordingState.Recording) return;

        SetState(RecordingState.Stopping);
        _durationTimer.Stop();

        try
        {
            _recorder?.Stop();

            // Brief wait for recording to stop
            if (_recordingComplete != null)
            {
                await Task.WhenAny(_recordingComplete.Task, Task.Delay(5000));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cancelling recording: {ex.Message}");
        }

        // Dispose recorder
        if (_recorder != null)
        {
            _recorder.OnRecordingComplete -= OnRecordingComplete;
            _recorder.OnRecordingFailed -= OnRecordingFailed;
            _recorder.OnStatusChanged -= OnStatusChanged;
            _recorder.Dispose();
            _recorder = null;
        }

        // Delete temp file
        CleanupTempFile();

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();

        Cleanup();
        SetState(RecordingState.Idle);
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

            // Wait for recording to complete - use longer timeout for large files (4K, etc.)
            if (_recordingComplete != null)
            {
                System.Diagnostics.Debug.WriteLine("Waiting for recording to finalize...");
                var completedTask = await Task.WhenAny(_recordingComplete.Task, Task.Delay(120000)); // 2 minutes for large 4K files
                if (completedTask != _recordingComplete.Task)
                {
                    System.Diagnostics.Debug.WriteLine("Recording finalization timed out after 2 minutes");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Recording finalized successfully");
                }
            }

            // Give the recorder extra time to release the file
            await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping recording: {ex.Message}");
        }

        // Dispose recorder before returning to ensure file is released
        if (_recorder != null)
        {
            _recorder.OnRecordingComplete -= OnRecordingComplete;
            _recorder.OnRecordingFailed -= OnRecordingFailed;
            _recorder.OnStatusChanged -= OnStatusChanged;
            _recorder.Dispose();
            _recorder = null;
        }

        // Force garbage collection to release any lingering handles
        // This is especially important after pause/resume cycles
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Extra delay after disposal
        await Task.Delay(300);

        // Check if recording actually produced data
        if (!string.IsNullOrEmpty(_tempPath) && File.Exists(_tempPath))
        {
            var fileInfo = new FileInfo(_tempPath);
            System.Diagnostics.Debug.WriteLine($"Final temp file size: {fileInfo.Length} bytes");
            if (fileInfo.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("WARNING: Recording produced 0 byte file!");
                var error = _lastError ?? "Unknown error - recording produced empty file";
                System.Windows.MessageBox.Show(
                    $"Recording failed - file is empty.\n\nError: {error}",
                    "Video Snip Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                Cleanup();
                SetState(RecordingState.Idle);
                return null;
            }
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
