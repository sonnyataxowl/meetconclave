namespace Conclave.Client.Services;

public enum RoomEntry { NotFound, Failed, Sealed, Ended, NeedLogin, CanJoin }

/// <summary>Decides what an invite link (/m/{id}) should do for the visitor, before any LiveKit work starts.</summary>
public static class RoomAccess
{
    public static RoomEntry Decide(Meeting? meeting, string? error, bool loggedIn)
    {
        if (meeting is null) return error is null or "not_found" ? RoomEntry.NotFound : RoomEntry.Failed;
        if (meeting.SealId is not null) return RoomEntry.Sealed;
        if (meeting.EndedAt is not null) return RoomEntry.Ended;
        return loggedIn ? RoomEntry.CanJoin : RoomEntry.NeedLogin;
    }

    public static string InvitePath(string id) => $"/m/{id}";
}
