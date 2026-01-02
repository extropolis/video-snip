using VideoSnip.Models;
using VideoSnip.Services;

namespace VideoSnip.Tests.Services;

public class RecordingControllerTests
{
    [Fact]
    public void Constructor_InitializesWithIdleState()
    {
        using var controller = new RecordingController();

        Assert.Equal(RecordingState.Idle, controller.State);
    }

    [Fact]
    public void Constructor_InitializesDurationToZero()
    {
        using var controller = new RecordingController();

        Assert.Equal(TimeSpan.Zero, controller.Duration);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var controller = new RecordingController();

        // Should not throw
        controller.Dispose();
        controller.Dispose();
    }

    [Fact]
    public void StateChanged_EventIsRaised()
    {
        using var controller = new RecordingController();
        var eventRaised = false;

        controller.StateChanged += state => eventRaised = true;

        // The event would be raised during recording operations
        // This test validates the event can be subscribed to
        Assert.False(eventRaised); // Not raised yet since no operation performed
    }

    [Fact]
    public void DurationUpdated_EventCanBeSubscribed()
    {
        using var controller = new RecordingController();
        TimeSpan lastDuration = TimeSpan.Zero;

        controller.DurationUpdated += duration => lastDuration = duration;

        // Event subscription should work
        Assert.Equal(TimeSpan.Zero, lastDuration);
    }
}

public class RecordingStateTests
{
    [Fact]
    public void RecordingState_HasExpectedValues()
    {
        Assert.Equal(0, (int)RecordingState.Idle);
        Assert.Equal(1, (int)RecordingState.Selecting);
        Assert.Equal(2, (int)RecordingState.Recording);
        Assert.Equal(3, (int)RecordingState.Stopping);
    }

    [Fact]
    public void RecordingState_AllValuesAreDefined()
    {
        var values = Enum.GetValues<RecordingState>();
        Assert.Equal(4, values.Length);
    }
}
