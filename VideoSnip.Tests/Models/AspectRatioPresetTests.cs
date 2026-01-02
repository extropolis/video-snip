using VideoSnip.Models;

namespace VideoSnip.Tests.Models;

public class AspectRatioPresetTests
{
    [Fact]
    public void Presets_ContainsExpectedPresets()
    {
        var presets = AspectRatioPreset.Presets;

        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Name == "Free");
        Assert.Contains(presets, p => p.Name == "16:9");
        Assert.Contains(presets, p => p.Name == "4:3");
        Assert.Contains(presets, p => p.Name == "1:1");
    }

    [Fact]
    public void Presets_FreePreset_HasNullRatio()
    {
        var freePreset = AspectRatioPreset.Presets.First(p => p.Name == "Free");

        Assert.Null(freePreset.Ratio);
        Assert.Null(freePreset.FixedWidth);
        Assert.Null(freePreset.FixedHeight);
    }

    [Fact]
    public void Presets_16x9Preset_HasCorrectRatio()
    {
        var preset = AspectRatioPreset.Presets.First(p => p.Name == "16:9");

        Assert.NotNull(preset.Ratio);
        Assert.Equal(16.0 / 9.0, preset.Ratio.Value, precision: 5);
    }

    [Fact]
    public void Presets_4x3Preset_HasCorrectRatio()
    {
        var preset = AspectRatioPreset.Presets.First(p => p.Name == "4:3");

        Assert.NotNull(preset.Ratio);
        Assert.Equal(4.0 / 3.0, preset.Ratio.Value, precision: 5);
    }

    [Fact]
    public void Presets_1x1Preset_HasCorrectRatio()
    {
        var preset = AspectRatioPreset.Presets.First(p => p.Name == "1:1");

        Assert.NotNull(preset.Ratio);
        Assert.Equal(1.0, preset.Ratio.Value);
    }

    [Fact]
    public void Presets_FixedSizePresets_HaveCorrectDimensions()
    {
        var preset1080p = AspectRatioPreset.Presets.First(p => p.Name.StartsWith("1920x1080"));
        var preset720p = AspectRatioPreset.Presets.First(p => p.Name.StartsWith("1280x720"));

        Assert.Equal(1920, preset1080p.FixedWidth);
        Assert.Equal(1080, preset1080p.FixedHeight);
        Assert.Equal(1280, preset720p.FixedWidth);
        Assert.Equal(720, preset720p.FixedHeight);
    }

    [Fact]
    public void Presets_Count_IsExpected()
    {
        // Free + 5 aspect ratios + 6 resolutions (16:9) + 3 resolutions (4:3) = 15
        Assert.Equal(15, AspectRatioPreset.Presets.Length);
    }
}
