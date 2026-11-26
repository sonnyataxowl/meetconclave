namespace Conclave.Client.Services;

/// <summary>Outcome of comparing a locally computed fingerprint with the sealed one. SizeDiffers is only a hint for the message.</summary>
public sealed record FileCheckOutcome(bool Match, bool SizeDiffers);

public static class FileCheck
{
    /// <summary>Fingerprints are compared case-insensitively and ignoring surrounding whitespace; a blank fingerprint never matches.</summary>
    public static FileCheckOutcome Compare(string? sealedSha256, long sealedBytes, string? fileSha256, long fileBytes)
    {
        var a = sealedSha256?.Trim();
        var b = fileSha256?.Trim();
        var match = !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        return new FileCheckOutcome(match, sealedBytes != fileBytes);
    }

    /// <summary>Progress as a whole percent, clamped to 0..100; an empty file counts as done.</summary>
    public static int Percent(long done, long total) => total <= 0 ? 100 : (int)Math.Clamp(done * 100 / total, 0, 100);
}
