namespace Conclave.Client.Services;

public enum SealPhase { Idle, Recording, Hashing, Sealing, Failed, Sealed }

/// <summary>What the "봉인됨" card shows. Built from a sealed meeting so it survives a page reload.</summary>
public sealed record SealCard(string Sha256, string SealId, DateTimeOffset? SealedAt, long? Bytes, long? DurationMs)
{
    /// <summary>Path of the public verify page (epic ⑤); a placeholder link until that page exists.</summary>
    public string VerifyPath => $"/verify/{SealId}";

    public static SealCard? From(Meeting? m)
        => m is { SealId: { Length: > 0 } id, Sha256: { Length: > 0 } sha }
            ? new SealCard(sha, id, m.SealedAt, m.Bytes, m.DurationMs)
            : null;
}

public static class SealFlow
{
    public static string Status(SealPhase phase) => phase switch
    {
        SealPhase.Recording => "녹화 중",
        SealPhase.Hashing => "지문 계산 중…",
        SealPhase.Sealing => "Axowl 에 봉인 중…",
        SealPhase.Failed => "봉인 실패",
        SealPhase.Sealed => "봉인됨",
        _ => "",
    };

    public static bool IsWorking(SealPhase phase) => phase is SealPhase.Hashing or SealPhase.Sealing;

    /// <summary>Retry only re-sends the fingerprint we already computed; it never re-records.</summary>
    public static bool CanRetry(SealPhase phase, string? sha256) => phase == SealPhase.Failed && !string.IsNullOrEmpty(sha256);
}
