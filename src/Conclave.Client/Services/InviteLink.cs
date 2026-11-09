namespace Conclave.Client.Services;

public enum InviteEntry { NotFound, Ended, AutoJoin, NeedsLogin }

/// <summary>Decides what an invite-link visitor sees: a notice, automatic entry (signed in), or the sign-in prompt.</summary>
public static class InviteLink
{
    public static InviteEntry Resolve(Meeting? meeting, bool signedIn)
    {
        if (meeting is null) return InviteEntry.NotFound;
        if (meeting.EndedAt is not null || meeting.SealId is not null) return InviteEntry.Ended;
        return signedIn ? InviteEntry.AutoJoin : InviteEntry.NeedsLogin;
    }
}
