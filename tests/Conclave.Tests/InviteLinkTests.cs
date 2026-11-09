using Conclave.Client.Services;
using Xunit;

namespace Conclave.Tests;

public class InviteLinkTests
{
    private static Meeting M(DateTimeOffset? ended = null, string? seal = null)
        => new("m1", "t", "host", null, null, ended, seal, null, null, null, null, null, null);

    [Fact] public void Missing_meeting_is_not_found() => Assert.Equal(InviteEntry.NotFound, InviteLink.Resolve(null, true));
    [Fact] public void Ended_meeting_is_reported() => Assert.Equal(InviteEntry.Ended, InviteLink.Resolve(M(DateTimeOffset.UtcNow), true));
    [Fact] public void Sealed_meeting_is_reported() => Assert.Equal(InviteEntry.Ended, InviteLink.Resolve(M(seal: "s1"), false));
    [Fact] public void Signed_in_visitor_joins_automatically() => Assert.Equal(InviteEntry.AutoJoin, InviteLink.Resolve(M(), true));
    [Fact] public void Anonymous_visitor_is_asked_to_sign_in() => Assert.Equal(InviteEntry.NeedsLogin, InviteLink.Resolve(M(), false));
}
