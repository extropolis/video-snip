using VideoSnip.Services;

namespace VideoSnip.Tests.Services;

public class RecordingResultTests
{
    [Fact]
    public void RecordingResult_DefaultProperties_AreEmpty()
    {
        var result = new RecordingResult();

        Assert.Equal("", result.TempFilePath);
        Assert.Equal("", result.SuggestedFileName);
        Assert.Equal("", result.DefaultFolder);
        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    [Fact]
    public void RecordingResult_CanSetAllProperties()
    {
        var result = new RecordingResult
        {
            TempFilePath = @"C:\Temp\test.mp4",
            SuggestedFileName = "2024-01-15_12-30-45.mp4",
            DefaultFolder = @"C:\Users\Test\Videos",
            Duration = TimeSpan.FromMinutes(5)
        };

        Assert.Equal(@"C:\Temp\test.mp4", result.TempFilePath);
        Assert.Equal("2024-01-15_12-30-45.mp4", result.SuggestedFileName);
        Assert.Equal(@"C:\Users\Test\Videos", result.DefaultFolder);
        Assert.Equal(TimeSpan.FromMinutes(5), result.Duration);
    }

    [Fact]
    public void RecordingResult_Duration_CanBeSet()
    {
        var result = new RecordingResult
        {
            Duration = TimeSpan.FromSeconds(90)
        };

        Assert.Equal(90, result.Duration.TotalSeconds);
    }
}
