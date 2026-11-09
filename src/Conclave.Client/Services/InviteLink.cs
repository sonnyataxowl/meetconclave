namespace Conclave.Client.Services;

public enum RoomAccess { NotFound, Open, Ended, Sealed }

/// <summary>Invite URL building and what an invite link should do for a given meeting.</summary>
public static class InviteLink
{
    public static string Build(string baseUri, string meetingId) => $"{baseUri.TrimEnd('/')}/m/{meetingId}";

    public static RoomAccess Classify(Meeting? meeting) =>
        meeting is null ? RoomAccess.NotFound
        : meeting.SealId is not null ? RoomAccess.Sealed
        : meeting.EndedAt is not null ? RoomAccess.Ended
        : RoomAccess.Open;
}
