using Conclave.Client.Services;
using Xunit;

namespace Conclave.Tests;

public class FileCheckTests
{
    private const string H = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string Other = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void SameFingerprint_Matches()
    {
        var r = FileCheck.Compare(H, 10, H, 10);
        Assert.True(r.Match);
        Assert.False(r.SizeDiffers);
    }

    [Fact]
    public void CaseAndWhitespaceDoNotMatter()
        => Assert.True(FileCheck.Compare(" " + H.ToUpperInvariant() + "\n", 1, H, 1).Match);

    [Fact]
    public void OneChangedCharacter_IsMismatch()
    {
        var changed = (H[0] == 'e' ? "f" : "e") + H[1..];
        Assert.False(FileCheck.Compare(H, 5, changed, 5).Match);
    }

    [Fact]
    public void DifferentFingerprintAndSize_FlagsSize()
    {
        var r = FileCheck.Compare(H, 10, Other, 11);
        Assert.False(r.Match);
        Assert.True(r.SizeDiffers);
    }

    [Theory]
    [InlineData(null, H)]
    [InlineData("", H)]
    [InlineData(H, null)]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void BlankFingerprint_NeverMatches(string? sealedHash, string? fileHash)
        => Assert.False(FileCheck.Compare(sealedHash, 0, fileHash, 0).Match);

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 50)]
    [InlineData(100, 100, 100)]
    [InlineData(150, 100, 100)]
    [InlineData(-5, 100, 0)]
    [InlineData(0, 0, 100)]
    public void Percent_IsClamped(long done, long total, int expected)
        => Assert.Equal(expected, FileCheck.Percent(done, total));
}
