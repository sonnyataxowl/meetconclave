using Conclave.Client.Services;
using Xunit;

namespace Conclave.Tests;

public class SealVerdictTests
{
    private static readonly string Sha = new('a', 64);

    private static VerifyInfo Info(string? sha = null) => new("abc123", "m1", "주간 회의", "호스트", sha ?? Sha, 1024, 60000,
        null, null, null, "evt-1", "axowl", "hackertones");

    private static LedgerRecord Rec(string? sha = null, string? integrity = "verified", bool sealedOk = true, string? subject = "m1", string? type = "custom.recording_sealed")
        => new("evt-1", type, subject, sha ?? Sha, sealedOk, integrity, "hot_cache", DateTimeOffset.UtcNow);

    [Fact]
    public void Match_WhenLedgerVerifiedAndFingerprintEqual()
        => Assert.Equal(SealVerdict.Match, SealVerdictRules.Decide(Info(), Rec(), null).Verdict);

    [Fact]
    public void Match_ForArchivedEvent_AndIgnoresHexCase()
        => Assert.Equal(SealVerdict.Match, SealVerdictRules.Decide(Info(), Rec(sha: Sha.ToUpperInvariant(), integrity: "archived"), null).Verdict);

    [Fact]
    public void Tampered_WhenDatabaseFingerprintWasChanged()
    {
        var edited = Info(new string('b', 64));   // someone rewrote the row's recording_sha256
        Assert.Equal(SealVerdict.Tampered, SealVerdictRules.Decide(edited, Rec(), null).Verdict);
    }

    [Fact]
    public void Tampered_WhenLedgerReportsTamperedRow()
        => Assert.Equal(SealVerdict.Tampered, SealVerdictRules.Decide(Info(), Rec(integrity: "tampered"), null).Verdict);

    [Theory]
    [InlineData("other-meeting", "custom.recording_sealed")]
    [InlineData("m1", "custom.contract_signed")]
    [InlineData(null, "custom.recording_sealed")]
    public void Tampered_WhenEventIdPointsAtSomeoneElsesSeal(string? subject, string type)
        => Assert.Equal(SealVerdict.Tampered, SealVerdictRules.Decide(Info(), Rec(subject: subject, type: type), null).Verdict);

    [Fact]
    public void Unavailable_WhenLedgerCallFailed_NeverTampered()
    {
        var r = SealVerdictRules.Decide(Info(), null, "ledger_unavailable");
        Assert.Equal(SealVerdict.Unavailable, r.Verdict);
        Assert.Contains("ledger_unavailable", r.Reason);
    }

    [Fact]
    public void Unavailable_WhenNoRecordAndNoError()
        => Assert.Equal(SealVerdict.Unavailable, SealVerdictRules.Decide(Info(), null, null).Verdict);

    [Fact]
    public void Unavailable_WhenSealRecordMissing()
        => Assert.Equal(SealVerdict.Unavailable, SealVerdictRules.Decide(null, Rec(), null).Verdict);

    [Fact]
    public void Unavailable_WhenLedgerDoesNotConfirmSealOrFingerprint()
    {
        Assert.Equal(SealVerdict.Unavailable, SealVerdictRules.Decide(Info(), Rec(sealedOk: false), null).Verdict);
        Assert.Equal(SealVerdict.Unavailable, SealVerdictRules.Decide(Info(), Rec() with { Sha256 = null }, null).Verdict);
    }
}
