using Conclave.Client.Services;
using Xunit;

namespace Conclave.Tests;

public class InviteLinkTests
{
    private static Meeting M(DateTimeOffset? ended = null, string? seal = null) =>
        new("abc23456", "t", "host", null, null, ended, seal, null, null, null, null, null, null);

    [Fact]
    public void Build_UsesMeetingPath_WithoutDoubleSlash()
    {
        Assert.Equal("https://conclave2.pages.dev/m/abc23456", InviteLink.Build("https://conclave2.pages.dev/", "abc23456"));
    }

    [Fact]
    public void Classify_Missing_IsNotFound() => Assert.Equal(RoomAccess.NotFound, InviteLink.Classify(null));

    [Fact]
    public void Classify_Running_IsOpen() => Assert.Equal(RoomAccess.Open, InviteLink.Classify(M()));

    [Fact]
    public void Classify_Ended_IsEnded() => Assert.Equal(RoomAccess.Ended, InviteLink.Classify(M(ended: DateTimeOffset.UtcNow)));

    [Fact]
    public void Classify_Sealed_WinsOverEnded() => Assert.Equal(RoomAccess.Sealed, InviteLink.Classify(M(DateTimeOffset.UtcNow, "seal1")));
}
