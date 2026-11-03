namespace ClickStitch.Core.Tests;

public sealed class RecordingSessionTests
{
    [Fact]
    public void A_new_session_is_idle()
    {
        var session = new RecordingSession();

        Assert.Equal(RecordingState.Idle, session.State);
        Assert.Equal(0, session.Count);
        Assert.Equal(RecordingStop.None, session.StopReason);
        Assert.False(session.Add());
    }

    [Fact]
    public void Start_accepts_events_and_stop_keeps_them()
    {
        var session = new RecordingSession();

        session.Start();
        Assert.True(session.Add());
        Assert.True(session.Add());
        session.Stop();

        Assert.Equal(RecordingState.Stopped, session.State);
        Assert.Equal(RecordingStop.Requested, session.StopReason);
        Assert.Equal(2, session.Count);
        Assert.False(session.Add());
        Assert.Null(session.Notice);
    }

    [Fact]
    public void Cancel_drops_the_events()
    {
        var session = new RecordingSession();
        session.Start();
        session.Add();

        session.Cancel();

        Assert.Equal(RecordingState.Cancelled, session.State);
        Assert.Equal(0, session.Count);
        Assert.False(session.Add());
    }

    [Fact]
    public void Start_after_stop_or_cancel_begins_again()
    {
        var session = new RecordingSession();
        session.Start();
        session.Add();
        session.Stop();
        session.Start();

        Assert.Equal(RecordingState.Recording, session.State);
        Assert.Equal(0, session.Count);

        session.Cancel();
        session.Start();
        Assert.True(session.Add());
        Assert.Equal(1, session.Count);
    }

    [Fact]
    public void A_second_start_is_refused()
    {
        var session = new RecordingSession();
        session.Start();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => session.Start());

        Assert.Equal("A recording is already in progress.", error.Message);
        Assert.Equal(RecordingState.Recording, session.State);
    }

    [Fact]
    public void The_event_that_fills_the_limit_is_kept_and_the_session_stops()
    {
        var session = new RecordingSession();
        session.Start();
        for (int index = 0; index < RecordingSession.MaximumEvents - 1; index++)
        {
            Assert.True(session.Add());
        }

        Assert.Equal(RecordingState.Recording, session.State);
        Assert.True(session.Add());

        Assert.Equal(RecordingState.Stopped, session.State);
        Assert.Equal(RecordingStop.Limit, session.StopReason);
        Assert.Equal(RecordingSession.MaximumEvents, session.Count);
        Assert.Equal("Recording stopped after 100,000 events.", session.Notice);
        Assert.False(session.Add());
        Assert.Equal(RecordingSession.MaximumEvents, session.Count);
    }
}
