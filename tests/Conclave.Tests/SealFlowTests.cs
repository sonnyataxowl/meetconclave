using Conclave.Client.Services;
using Xunit;

namespace Conclave.Tests;

public class SealFlowTests
{
    private static Meeting M(string? sealId, string? sha) =>
        new("abc", "t", "host", null, null, null, sealId, DateTimeOffset.Parse("2026-11-26T10:00:00Z"), sha, 2048, 61000, null, null);

    [Fact]
    public void Card_RestoredFromSealedMeeting()
    {
        var c = SealCard.From(M("s1", "ab12"));
        Assert.NotNull(c);
        Assert.Equal("ab12", c!.Sha256);
        Assert.Equal("/verify/s1", c.VerifyPath);
        Assert.Equal(DateTimeOffset.Parse("2026-11-26T10:00:00Z"), c.SealedAt);
    }

    [Theory]
    [InlineData(null, "ab12")]
    [InlineData("", "ab12")]
    [InlineData("s1", null)]
    [InlineData("s1", "")]
    public void Card_NullWhenNotFullySealed(string? sealId, string? sha) => Assert.Null(SealCard.From(M(sealId, sha)));

    [Fact]
    public void Card_NullForMissingMeeting() => Assert.Null(SealCard.From(null));

    [Theory]
    [InlineData(SealPhase.Hashing, "지문 계산 중…")]
    [InlineData(SealPhase.Sealing, "Axowl 에 봉인 중…")]
    [InlineData(SealPhase.Failed, "봉인 실패")]
    [InlineData(SealPhase.Idle, "")]
    public void Status_TextPerPhase(SealPhase p, string text) => Assert.Equal(text, SealFlow.Status(p));

    [Fact]
    public void IsWorking_OnlyWhileHashingOrSealing()
    {
        Assert.True(SealFlow.IsWorking(SealPhase.Hashing));
        Assert.True(SealFlow.IsWorking(SealPhase.Sealing));
        Assert.False(SealFlow.IsWorking(SealPhase.Failed));
        Assert.False(SealFlow.IsWorking(SealPhase.Sealed));
    }

    [Fact]
    public void Retry_NeedsFailureAndFingerprint()
    {
        Assert.True(SealFlow.CanRetry(SealPhase.Failed, "ab12"));
        Assert.False(SealFlow.CanRetry(SealPhase.Failed, null));
        Assert.False(SealFlow.CanRetry(SealPhase.Sealing, "ab12"));
    }
}
