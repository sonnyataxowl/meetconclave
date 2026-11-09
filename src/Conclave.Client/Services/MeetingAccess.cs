namespace Conclave.Client.Services;

public enum RoomAccess { Joinable, Ended, Sealed }

/// <summary>Decides what an invite-link visitor sees for a meeting.</summary>
public static class MeetingAccess
{
    public static RoomAccess Classify(Meeting m)
        => m.SealId is not null ? RoomAccess.Sealed
         : m.EndedAt is not null ? RoomAccess.Ended
         : RoomAccess.Joinable;

    /// <summary>True when the API error means the meeting id does not exist.</summary>
    public static bool IsNotFound(string? error)
        => error is not null && (error == "not_found" || error.StartsWith("HTTP 404", StringComparison.Ordinal));

    /// <summary>A signed-in user opening a live meeting link goes straight in.</summary>
    public static bool ShouldAutoJoin(bool signedIn, Meeting m) => signedIn && Classify(m) == RoomAccess.Joinable;
}
