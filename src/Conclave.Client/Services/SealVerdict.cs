namespace Conclave.Client.Services;

public enum SealVerdict { Match, Tampered, Unavailable }

/// <summary>What Axowl's audit ledger holds for the sealed event (GET /api/verify/{id}/ledger, which relays guides/seal-anything).</summary>
public sealed record LedgerRecord(string? EventId, string? Type, string? SubjectId, string? Sha256, bool Sealed, string? Integrity, string? Source, DateTimeOffset? CreatedAt);

public sealed record VerdictResult(SealVerdict Verdict, string Reason);

/// <summary>
/// Compares what our database says was sealed with what Axowl's ledger recorded. Our database is not trusted:
/// whoever can edit the row can edit the fingerprint, so the ledger's copy decides.
/// Match = ledger verified the event and its fingerprint equals ours; Tampered = the ledger contradicts us;
/// Unavailable = we could not ask (or got no usable answer), which is never reported as either of the other two.
/// </summary>
public static class SealVerdictRules
{
    public const string RecordingType = "custom.recording_sealed";

    public static VerdictResult Decide(VerifyInfo? info, LedgerRecord? rec, string? error)
    {
        if (info is null) return Unavailable("봉인 기록이 없어 확인할 수 없습니다.");
        if (rec is null) return Unavailable(string.IsNullOrEmpty(error) ? "Axowl 에서 응답을 받지 못했습니다." : $"Axowl 확인 호출이 실패했습니다 ({error}).");

        // Axowl re-hashed the stored ledger row and it no longer matches its own seal.
        if (string.Equals(rec.Integrity, "tampered", StringComparison.OrdinalIgnoreCase))
            return new(SealVerdict.Tampered, "Axowl 원장의 기록이 봉인과 맞지 않습니다.");

        if (!rec.Sealed) return Unavailable("Axowl 가 이 이벤트를 봉인된 것으로 확인해 주지 않았습니다.");
        if (string.IsNullOrEmpty(rec.Sha256)) return Unavailable("Axowl 기록에 지문이 없어 비교할 수 없습니다.");

        // The event id stored in our row must point at THIS meeting's recording seal, not some other sealed event.
        if (!string.Equals(rec.Type, RecordingType, StringComparison.Ordinal) || !string.Equals(rec.SubjectId, info.MeetingId, StringComparison.Ordinal))
            return new(SealVerdict.Tampered, "저장된 봉인 번호가 이 회의의 Axowl 봉인 기록을 가리키지 않습니다.");

        return string.Equals(rec.Sha256, info.Sha256, StringComparison.OrdinalIgnoreCase)
            ? new(SealVerdict.Match, "저장된 지문이 Axowl 원장에 봉인된 지문과 같습니다.")
            : new(SealVerdict.Tampered, "저장된 지문이 Axowl 원장에 봉인된 지문과 다릅니다.");
    }

    private static VerdictResult Unavailable(string reason) => new(SealVerdict.Unavailable, reason);
}
