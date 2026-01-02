using System.Windows;

namespace VideoSnip.Models;

public enum VideoCaptureMode
{
    Region,
    Window
}

public class RecordingRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public IntPtr WindowHandle { get; set; }
    public VideoCaptureMode Mode { get; set; }
    public string? WindowTitle { get; set; }

    public Rect ToRect() => new(X, Y, Width, Height);

    public bool IsValid => Width > 10 && Height > 10;
}

public class AspectRatioPreset
{
    public string Name { get; set; } = "";
    public double? Ratio { get; set; } // null = custom/free
    public int? FixedWidth { get; set; }
    public int? FixedHeight { get; set; }

    public static AspectRatioPreset[] Presets =
    [
        new() { Name = "Free", Ratio = null },
        new() { Name = "16:9", Ratio = 16.0 / 9.0 },
        new() { Name = "4:3", Ratio = 4.0 / 3.0 },
        new() { Name = "1:1", Ratio = 1.0 },
        new() { Name = "9:16", Ratio = 9.0 / 16.0 },
        new() { Name = "3840x2160 (4K)", FixedWidth = 3840, FixedHeight = 2160 },
        new() { Name = "1920x1080", FixedWidth = 1920, FixedHeight = 1080 },
        new() { Name = "1280x720", FixedWidth = 1280, FixedHeight = 720 },
        new() { Name = "800x600", FixedWidth = 800, FixedHeight = 600 }
    ];
}
