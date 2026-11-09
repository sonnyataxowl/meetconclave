using Conclave.Client.Services;
using Xunit;

namespace Conclave.Tests;

public class InviteLinkTests
{
    private static Meeting M(DateTimeOffset? ended = null, string? seal = null)
        => new("abc12345", "t", "host", null, null, ended, seal, null, null, null, null, null, null);

    [Theory]
    [InlineData("https://conclave2.pages.dev/", "abc12345", "https://conclave2.pages.dev/m/abc12345")]
    [InlineData("https://conclave2.pages.dev", "abc12345", "https://conclave2.pages.dev/m/abc12345")]
    public void Build_ReturnsCanonicalLink(string baseUri, string id, string expected)
        => Assert.Equal(expected, InviteLink.Build(baseUri, id));

    [Fact]
    public void ReturnPath_IsRoomPath()
    {
        Assert.Equal("/m/abc12345", InviteLink.ReturnPath("abc12345"));
        Assert.True(InviteLink.IsRoomPath(InviteLink.ReturnPath("abc12345")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/")]
    [InlineData("/meetings")]
    [InlineData("/m/")]
    public void IsRoomPath_RejectsOtherPaths(string? path) => Assert.False(InviteLink.IsRoomPath(path));

    [Fact]
    public void Classify_UnknownMeeting_IsNotFound() => Assert.Equal(RoomAccess.NotFound, InviteLink.Classify(null, "not_found"));

    [Fact]
    public void Classify_OtherError_IsFailed() => Assert.Equal(RoomAccess.Failed, InviteLink.Classify(null, "HTTP 500"));

    [Fact]
    public void Classify_EndedMeeting_IsEnded() => Assert.Equal(RoomAccess.Ended, InviteLink.Classify(M(ended: DateTimeOffset.UtcNow), null));

    [Fact]
    public void Classify_SealedMeeting_IsSealed() => Assert.Equal(RoomAccess.Sealed, InviteLink.Classify(M(ended: DateTimeOffset.UtcNow, seal: "s1"), null));

    [Fact]
    public void Classify_LiveMeeting_IsOpen() => Assert.Equal(RoomAccess.Open, InviteLink.Classify(M(), null));
}
