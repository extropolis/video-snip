using System.IO;
using ScreenRecorderLib;

namespace VideoSnip.Tests.Services;

/// <summary>
/// Tests to systematically determine what ScreenRecorderLib configurations work.
/// These tests help identify encoder limitations and find working settings.
/// NOTE: These tests require display/GPU hardware and cannot run on CI runners.
/// </summary>
[Trait("Category", "RequiresDisplay")]
public class ScreenRecorderLibTests
{
    private readonly string _testOutputFolder;

    public ScreenRecorderLibTests()
    {
        _testOutputFolder = Path.Combine(Path.GetTempPath(), "VideoSnipTests");
        Directory.CreateDirectory(_testOutputFolder);
    }

    [Theory]
    [InlineData(640, 480, "640x480")]
    [InlineData(1280, 720, "1280x720")]
    [InlineData(1920, 1080, "1920x1080")]
    [InlineData(2560, 1440, "2560x1440")]
    [InlineData(3840, 2160, "3840x2160")]
    public async Task TestRecording_DifferentResolutions(int width, int height, string name)
    {
        var outputPath = Path.Combine(_testOutputFolder, $"test_{name}.mp4");
        var result = await TryRecordWithSourceRect(width, height, outputPath);

        // Output result for analysis
        if (result.Success)
        {
            Assert.True(result.Success, $"Recording {name} succeeded");
        }
        else
        {
            // Mark as inconclusive with the error for analysis
            Assert.Fail($"Recording {name} FAILED: {result.Error}");
        }
    }

    [Fact]
    public async Task TestRecording_DisplaySource_NoSourceRect()
    {
        var outputPath = Path.Combine(_testOutputFolder, "test_display_source.mp4");
        var result = await TryRecordWithDisplaySource(outputPath);

        if (result.Success)
        {
            Assert.True(result.Success, "DisplaySource recording succeeded");
        }
        else
        {
            Assert.Fail($"DisplaySource recording FAILED: {result.Error}");
        }
    }

    [Fact]
    public async Task TestRecording_ActualPrimaryScreen()
    {
        // Get actual primary screen dimensions
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        int width = screen?.Bounds.Width ?? 1920;
        int height = screen?.Bounds.Height ?? 1080;

        var outputPath = Path.Combine(_testOutputFolder, $"test_actual_screen_{width}x{height}.mp4");
        var result = await TryRecordWithSourceRect(width, height, outputPath);

        if (result.Success)
        {
            Assert.True(result.Success, $"Actual screen {width}x{height} succeeded");
        }
        else
        {
            Assert.Fail($"Actual screen {width}x{height} FAILED: {result.Error}");
        }
    }

    [Fact]
    public async Task TestRecording_WorkAreaDimensions()
    {
        // Simulate WorkArea (screen minus taskbar) - common odd dimensions
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        int width = screen?.WorkingArea.Width ?? 1920;
        int height = screen?.WorkingArea.Height ?? 1040;

        var outputPath = Path.Combine(_testOutputFolder, $"test_workarea_{width}x{height}.mp4");
        var result = await TryRecordWithSourceRect(width, height, outputPath);

        if (result.Success)
        {
            Assert.True(result.Success, $"WorkArea {width}x{height} succeeded");
        }
        else
        {
            Assert.Fail($"WorkArea {width}x{height} FAILED: {result.Error}");
        }
    }

    [Theory]
    [InlineData(true, "Hardware")]
    [InlineData(false, "Software")]
    public async Task TestRecording_EncoderType(bool useHardware, string name)
    {
        var outputPath = Path.Combine(_testOutputFolder, $"test_encoder_{name}.mp4");
        var result = await TryRecordWithEncoderSetting(1920, 1080, useHardware, outputPath);

        if (result.Success)
        {
            Assert.True(result.Success, $"{name} encoding succeeded");
        }
        else
        {
            Assert.Fail($"{name} encoding FAILED: {result.Error}");
        }
    }

    [Theory]
    [InlineData(8000000, "8Mbps")]
    [InlineData(4000000, "4Mbps")]
    [InlineData(2000000, "2Mbps")]
    public async Task TestRecording_DifferentBitrates(int bitrate, string name)
    {
        var outputPath = Path.Combine(_testOutputFolder, $"test_bitrate_{name}.mp4");
        var result = await TryRecordWithBitrate(1920, 1080, bitrate, outputPath);

        if (result.Success)
        {
            Assert.True(result.Success, $"Bitrate {name} succeeded");
        }
        else
        {
            Assert.Fail($"Bitrate {name} FAILED: {result.Error}");
        }
    }

    /// <summary>
    /// Test that captures at 5120x2160 but scales output to 4096 width.
    /// This is the workaround for ultrawide monitors that exceed H.264 encoder limits.
    /// </summary>
    [Fact]
    public async Task TestRecording_UltrawideWithScaledOutput()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        int captureWidth = screen?.Bounds.Width ?? 5120;
        int captureHeight = screen?.Bounds.Height ?? 2160;

        // Scale to max 4096 width while maintaining aspect ratio
        const int maxWidth = 4096;
        int outputWidth = Math.Min(captureWidth, maxWidth);
        int outputHeight = captureWidth > maxWidth
            ? (int)(captureHeight * ((double)maxWidth / captureWidth))
            : captureHeight;
        // Ensure even dimensions
        outputWidth = (outputWidth / 2) * 2;
        outputHeight = (outputHeight / 2) * 2;

        var outputPath = Path.Combine(_testOutputFolder, $"test_ultrawide_scaled_{captureWidth}x{captureHeight}_to_{outputWidth}x{outputHeight}.mp4");
        var result = await TryRecordWithScaledOutput(captureWidth, captureHeight, outputWidth, outputHeight, outputPath);

        if (result.Success)
        {
            Assert.True(result.Success, $"Ultrawide {captureWidth}x{captureHeight} scaled to {outputWidth}x{outputHeight} succeeded");
        }
        else
        {
            Assert.Fail($"Ultrawide scaled output FAILED: {result.Error}");
        }
    }

    /// <summary>
    /// Test specific 5120x2160 resolution scaled to 4096x1728
    /// </summary>
    [Fact]
    public async Task TestRecording_5120x2160_ScaledTo4096()
    {
        int captureWidth = 5120;
        int captureHeight = 2160;
        int outputWidth = 4096;
        int outputHeight = 1728; // Maintains aspect ratio (5120:2160 = 4096:1728)

        var outputPath = Path.Combine(_testOutputFolder, $"test_5k_scaled.mp4");
        var result = await TryRecordWithScaledOutput(captureWidth, captureHeight, outputWidth, outputHeight, outputPath);

        if (result.Success)
        {
            Assert.True(result.Success, $"5K {captureWidth}x{captureHeight} scaled to {outputWidth}x{outputHeight} succeeded");
        }
        else
        {
            Assert.Fail($"5K scaled output FAILED: {result.Error}");
        }
    }

    private async Task<(bool Success, string? Error)> TryRecordWithSourceRect(int width, int height, string outputPath)
    {
        var tcs = new TaskCompletionSource<(bool, string?)>();

        try
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);

            var options = new RecorderOptions
            {
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video,
                    SourceRect = new ScreenRect(0, 0, width, height)
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Bitrate = 8000 * 1000,
                    Framerate = 30,
                    IsFixedFramerate = false
                },
                MouseOptions = new MouseOptions { IsMousePointerEnabled = false },
                AudioOptions = new AudioOptions { IsAudioEnabled = false }
            };

            using var recorder = Recorder.CreateRecorder(options);

            recorder.OnRecordingFailed += (s, e) => tcs.TrySetResult((false, e.Error));
            recorder.OnRecordingComplete += (s, e) => tcs.TrySetResult((true, null));
            recorder.OnStatusChanged += (s, e) =>
            {
                if (e.Status == RecorderStatus.Recording)
                {
                    // Record for 1 second then stop
                    Task.Delay(1000).ContinueWith(_ => recorder.Stop());
                }
            };

            recorder.Record(outputPath);

            // Wait for completion with timeout
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));
            if (completedTask != tcs.Task)
            {
                recorder.Stop();
                return (false, "Timeout waiting for recording to complete");
            }

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? Error)> TryRecordWithDisplaySource(string outputPath)
    {
        var tcs = new TaskCompletionSource<(bool, string?)>();

        try
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);

            var displaySource = new DisplayRecordingSource(DisplayRecordingSource.MainMonitor);

            var options = new RecorderOptions
            {
                SourceOptions = new SourceOptions
                {
                    RecordingSources = { displaySource }
                },
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Bitrate = 8000 * 1000,
                    Framerate = 30,
                    IsFixedFramerate = false
                },
                MouseOptions = new MouseOptions { IsMousePointerEnabled = false },
                AudioOptions = new AudioOptions { IsAudioEnabled = false }
            };

            using var recorder = Recorder.CreateRecorder(options);

            recorder.OnRecordingFailed += (s, e) => tcs.TrySetResult((false, e.Error));
            recorder.OnRecordingComplete += (s, e) => tcs.TrySetResult((true, null));
            recorder.OnStatusChanged += (s, e) =>
            {
                if (e.Status == RecorderStatus.Recording)
                {
                    Task.Delay(1000).ContinueWith(_ => recorder.Stop());
                }
            };

            recorder.Record(outputPath);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));
            if (completedTask != tcs.Task)
            {
                recorder.Stop();
                return (false, "Timeout waiting for recording to complete");
            }

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? Error)> TryRecordWithEncoderSetting(int width, int height, bool useHardware, string outputPath)
    {
        var tcs = new TaskCompletionSource<(bool, string?)>();

        try
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);

            var options = new RecorderOptions
            {
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video,
                    SourceRect = new ScreenRect(0, 0, width, height)
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Bitrate = 8000 * 1000,
                    Framerate = 30,
                    IsFixedFramerate = false,
                    IsHardwareEncodingEnabled = useHardware
                },
                MouseOptions = new MouseOptions { IsMousePointerEnabled = false },
                AudioOptions = new AudioOptions { IsAudioEnabled = false }
            };

            using var recorder = Recorder.CreateRecorder(options);

            recorder.OnRecordingFailed += (s, e) => tcs.TrySetResult((false, e.Error));
            recorder.OnRecordingComplete += (s, e) => tcs.TrySetResult((true, null));
            recorder.OnStatusChanged += (s, e) =>
            {
                if (e.Status == RecorderStatus.Recording)
                {
                    Task.Delay(1000).ContinueWith(_ => recorder.Stop());
                }
            };

            recorder.Record(outputPath);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));
            if (completedTask != tcs.Task)
            {
                recorder.Stop();
                return (false, "Timeout waiting for recording to complete");
            }

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? Error)> TryRecordWithBitrate(int width, int height, int bitrate, string outputPath)
    {
        var tcs = new TaskCompletionSource<(bool, string?)>();

        try
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);

            var options = new RecorderOptions
            {
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video,
                    SourceRect = new ScreenRect(0, 0, width, height)
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Bitrate = bitrate,
                    Framerate = 30,
                    IsFixedFramerate = false
                },
                MouseOptions = new MouseOptions { IsMousePointerEnabled = false },
                AudioOptions = new AudioOptions { IsAudioEnabled = false }
            };

            using var recorder = Recorder.CreateRecorder(options);

            recorder.OnRecordingFailed += (s, e) => tcs.TrySetResult((false, e.Error));
            recorder.OnRecordingComplete += (s, e) => tcs.TrySetResult((true, null));
            recorder.OnStatusChanged += (s, e) =>
            {
                if (e.Status == RecorderStatus.Recording)
                {
                    Task.Delay(1000).ContinueWith(_ => recorder.Stop());
                }
            };

            recorder.Record(outputPath);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(10000));
            if (completedTask != tcs.Task)
            {
                recorder.Stop();
                return (false, "Timeout waiting for recording to complete");
            }

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
    }

    private async Task<(bool Success, string? Error)> TryRecordWithScaledOutput(int captureWidth, int captureHeight, int outputWidth, int outputHeight, string outputPath)
    {
        var tcs = new TaskCompletionSource<(bool, string?)>();

        try
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);

            var options = new RecorderOptions
            {
                OutputOptions = new OutputOptions
                {
                    RecorderMode = RecorderMode.Video,
                    SourceRect = new ScreenRect(0, 0, captureWidth, captureHeight),
                    OutputFrameSize = new ScreenSize(outputWidth, outputHeight)
                },
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Bitrate = 15000 * 1000, // 15 Mbps for high quality
                    Framerate = 30,
                    IsFixedFramerate = false
                },
                MouseOptions = new MouseOptions { IsMousePointerEnabled = false },
                AudioOptions = new AudioOptions { IsAudioEnabled = false }
            };

            using var recorder = Recorder.CreateRecorder(options);

            recorder.OnRecordingFailed += (s, e) => tcs.TrySetResult((false, e.Error));
            recorder.OnRecordingComplete += (s, e) => tcs.TrySetResult((true, null));
            recorder.OnStatusChanged += (s, e) =>
            {
                if (e.Status == RecorderStatus.Recording)
                {
                    Task.Delay(1000).ContinueWith(_ => recorder.Stop());
                }
            };

            recorder.Record(outputPath);

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(15000)); // Longer timeout for larger captures
            if (completedTask != tcs.Task)
            {
                recorder.Stop();
                return (false, "Timeout waiting for recording to complete");
            }

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
    }
}
