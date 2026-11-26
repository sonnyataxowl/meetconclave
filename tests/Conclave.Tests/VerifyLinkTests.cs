using Conclave.Client.Services;
using Xunit;

namespace Conclave.Tests;

public class VerifyLinkTests
{
    [Fact]
    public void Build_JoinsBaseAndId()
    {
        Assert.Equal("https://conclave2.pages.dev/verify/abc123", VerifyLink.Build("https://conclave2.pages.dev/", " abc123 "));
    }

    [Theory]
    [InlineData("abc123", "abc123")]
    [InlineData("  abc123  ", "abc123")]
    [InlineData("https://conclave2.pages.dev/verify/abc123", "abc123")]
    [InlineData("https://conclave2.pages.dev/verify/abc123?x=1#top", "abc123")]
    [InlineData("https://conclave2.pages.dev/verify/abc123/", "abc123")]
    public void Normalize_ExtractsSealId(string input, string expected)
    {
        Assert.Equal(expected, VerifyLink.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://x.test/verify/")]
    public void Normalize_EmptyIsNull(string? input)
    {
        Assert.Null(VerifyLink.Normalize(input));
    }

    [Fact]
    public void Build_ThenNormalize_RoundTrips()
    {
        Assert.Equal("a b", VerifyLink.Normalize(VerifyLink.Build("https://x.test/", "a b")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("HTTP 404")]
    [InlineData("not_found")]
    public void ErrorMessage_NotFoundIsFriendly(string? err)
    {
        Assert.Equal(VerifyLink.NotFound, VerifyLink.ErrorMessage(err));
    }

    [Fact]
    public void ErrorMessage_OtherErrorsKeepDetail()
    {
        var m = VerifyLink.ErrorMessage("HTTP 500");
        Assert.Contains("HTTP 500", m);
        Assert.NotEqual(VerifyLink.NotFound, m);
    }

    [Fact]
    public void Title_UsesMeetingTitleWhenKnown()
    {
        Assert.Equal("주간 회의 · 봉인 검증 · Conclave", VerifyLink.Title("주간 회의"));
        Assert.Equal("봉인 검증 · Conclave", VerifyLink.Title(null));
    }
}
