namespace Conclave.Client.Services;

public enum RoomAccess { Open, NotFound, Ended, Sealed, Failed }

/// <summary>Invite-link building and the decision of what a visitor of /m/{id} gets to see.</summary>
public static class InviteLink
{
    /// <summary>The shareable link of a meeting: {origin}/m/{id}.</summary>
    public static string Build(string baseUri, string id) => baseUri.TrimEnd('/') + ReturnPath(id);

    /// <summary>App-relative path a visitor is sent back to after logging in.</summary>
    public static string ReturnPath(string id) => "/m/" + Uri.EscapeDataString(id);

    /// <summary>True when a post-login return path points at a meeting room (those auto-join).</summary>
    public static bool IsRoomPath(string? path) => path is not null && path.StartsWith("/m/", StringComparison.Ordinal) && path.Length > 3;

    public static RoomAccess Classify(Meeting? meeting, string? error)
    {
        if (meeting is null) return error is "not_found" ? RoomAccess.NotFound : RoomAccess.Failed;
        if (meeting.SealId is not null) return RoomAccess.Sealed;
        if (meeting.EndedAt is not null) return RoomAccess.Ended;
        return RoomAccess.Open;
    }
}
