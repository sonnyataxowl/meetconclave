using Conclave.Client.Services;
using Xunit;

namespace Conclave.Tests;

public class MeetingAccessTests
{
    private static Meeting M(DateTimeOffset? ended = null, string? seal = null)
        => new("abc123", "t", "host", null, null, ended, seal, null, null, null, null, null, null);

    [Fact]
    public void LiveMeeting_IsJoinable() => Assert.Equal(RoomAccess.Joinable, MeetingAccess.Classify(M()));

    [Fact]
    public void EndedMeeting_IsEnded() => Assert.Equal(RoomAccess.Ended, MeetingAccess.Classify(M(DateTimeOffset.UtcNow)));

    [Fact]
    public void SealedMeeting_IsSealed() => Assert.Equal(RoomAccess.Sealed, MeetingAccess.Classify(M(DateTimeOffset.UtcNow, "s1")));

    [Theory]
    [InlineData("not_found", true)]
    [InlineData("HTTP 404", true)]
    [InlineData("HTTP 500", false)]
    [InlineData(null, false)]
    public void IsNotFound_Detects404(string? err, bool expected) => Assert.Equal(expected, MeetingAccess.IsNotFound(err));

    [Fact]
    public void AutoJoin_OnlyWhenSignedInAndLive()
    {
        Assert.True(MeetingAccess.ShouldAutoJoin(true, M()));
        Assert.False(MeetingAccess.ShouldAutoJoin(false, M()));
        Assert.False(MeetingAccess.ShouldAutoJoin(true, M(DateTimeOffset.UtcNow)));
    }
}
