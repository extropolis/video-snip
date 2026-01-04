using VideoSnip.Models;

namespace VideoSnip.Tests.Models;

public class RecordingRegionTests
{
    [Fact]
    public void IsValid_WhenDimensionsAreLargerThan10_ReturnsTrue()
    {
        var region = new RecordingRegion
        {
            X = 0,
            Y = 0,
            Width = 100,
            Height = 100
        };

        Assert.True(region.IsValid);
    }

    [Fact]
    public void IsValid_WhenWidthIsTooSmall_ReturnsFalse()
    {
        var region = new RecordingRegion
        {
            X = 0,
            Y = 0,
            Width = 5,
            Height = 100
        };

        Assert.False(region.IsValid);
    }

    [Fact]
    public void IsValid_WhenHeightIsTooSmall_ReturnsFalse()
    {
        var region = new RecordingRegion
        {
            X = 0,
            Y = 0,
            Width = 100,
            Height = 5
        };

        Assert.False(region.IsValid);
    }

    [Fact]
    public void IsValid_WhenBothDimensionsAreTooSmall_ReturnsFalse()
    {
        var region = new RecordingRegion
        {
            X = 0,
            Y = 0,
            Width = 10,
            Height = 10
        };

        Assert.False(region.IsValid);
    }

    [Fact]
    public void ToRect_ReturnsCorrectRect()
    {
        var region = new RecordingRegion
        {
            X = 100,
            Y = 200,
            Width = 300,
            Height = 400
        };

        var rect = region.ToRect();

        Assert.Equal(100, rect.X);
        Assert.Equal(200, rect.Y);
        Assert.Equal(300, rect.Width);
        Assert.Equal(400, rect.Height);
    }

    [Fact]
    public void Mode_DefaultsToFullScreen()
    {
        var region = new RecordingRegion();
        // Default enum value is FullScreen (first in enum = 0)
        Assert.Equal(VideoCaptureMode.FullScreen, region.Mode);
    }

    [Fact]
    public void WindowHandle_DefaultsToZero()
    {
        var region = new RecordingRegion();
        Assert.Equal(IntPtr.Zero, region.WindowHandle);
    }
}
