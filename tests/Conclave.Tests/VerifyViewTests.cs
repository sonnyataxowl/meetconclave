using Conclave.Client.Services;
using Xunit;

namespace Conclave.Tests;

public class VerifyViewTests
{
    private static VerifyInfo Info() => new("abc123", "m1", "주간 회의", "호스트", new string('a', 64), 1024, 60000,
        null, null, null, "evt-1", "axowl", "hackertones");

    [Fact]
    public void Found_WhenInfoPresent() => Assert.Equal(VerifyStatus.Found, VerifyView.Classify(Info(), null));

    [Theory]
    [InlineData("not_found")]
    [InlineData("HTTP 404")]
    [InlineData(null)]
    public void NotFound_ForMissingRecord(string? error) => Assert.Equal(VerifyStatus.NotFound, VerifyView.Classify(null, error));

    [Theory]
    [InlineData("HTTP 500")]
    [InlineData("Failed to fetch")]
    [InlineData("db 500: boom")]
    public void Error_ForServerOrNetworkFailure(string error) => Assert.Equal(VerifyStatus.Error, VerifyView.Classify(null, error));
}
