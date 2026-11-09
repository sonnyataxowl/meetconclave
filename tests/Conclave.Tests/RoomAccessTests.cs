using Conclave.Client.Services;
using Xunit;

namespace Conclave.Tests;

public class RoomAccessTests
{
    private static Meeting M(DateTimeOffset? ended = null, string? seal = null)
        => new("abc12345", "t", "host", null, null, ended, seal, null, null, null, null, null, null);

    [Fact]
    public void UnknownMeeting_IsNotFound() => Assert.Equal(RoomEntry.NotFound, RoomAccess.Decide(null, "not_found", true));

    [Fact]
    public void NetworkError_IsFailed_NotNotFound() => Assert.Equal(RoomEntry.Failed, RoomAccess.Decide(null, "Failed to fetch", true));

    [Fact]
    public void EndedMeeting_IsEnded() => Assert.Equal(RoomEntry.Ended, RoomAccess.Decide(M(ended: DateTimeOffset.UtcNow), null, true));

    [Fact]
    public void SealedMeeting_IsSealed_EvenWhenEnded() => Assert.Equal(RoomEntry.Sealed, RoomAccess.Decide(M(ended: DateTimeOffset.UtcNow, seal: "s1"), null, false));

    [Fact]
    public void LoggedOutVisitor_NeedsLogin() => Assert.Equal(RoomEntry.NeedLogin, RoomAccess.Decide(M(), null, false));

    [Fact]
    public void LoggedInVisitor_CanJoin() => Assert.Equal(RoomEntry.CanJoin, RoomAccess.Decide(M(), null, true));

    [Fact]
    public void InvitePath_UsesMeetingId() => Assert.Equal("/m/abc12345", RoomAccess.InvitePath("abc12345"));
}
