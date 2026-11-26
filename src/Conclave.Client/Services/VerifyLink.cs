namespace Conclave.Client.Services;

/// <summary>Builds and normalises public /verify links and the user-facing messages of the verify page.</summary>
public static class VerifyLink
{
    public const string EmptyHint = "봉인 번호를 입력하거나 공유받은 검증 링크를 여세요.";
    public const string NotFound = "이 번호의 봉인 기록이 없습니다. 번호를 다시 확인해 주세요.";

    public static string Build(string baseUri, string sealId)
        => baseUri.TrimEnd('/') + "/verify/" + Uri.EscapeDataString(sealId.Trim());

    /// <summary>Accepts a bare seal id or a pasted /verify/{id} link and returns the id (null when empty).</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = input.Trim();
        var i = s.IndexOf("/verify/", StringComparison.OrdinalIgnoreCase);
        if (i >= 0) s = s[(i + 8)..];
        var end = s.IndexOfAny(new[] { '/', '?', '#' });
        if (end >= 0) s = s[..end];
        s = Uri.UnescapeDataString(s).Trim();
        return s.Length == 0 ? null : s;
    }

    public static string ErrorMessage(string? error)
        => string.IsNullOrWhiteSpace(error) || error.Contains("404") || error.Contains("not_found", StringComparison.OrdinalIgnoreCase)
            ? NotFound
            : "봉인 기록을 불러오지 못했습니다. 잠시 뒤 다시 시도해 주세요. (" + error + ")";

    public static string Title(string? meetingTitle)
        => string.IsNullOrWhiteSpace(meetingTitle) ? "봉인 검증 · " + AppInfo.Name : meetingTitle + " · 봉인 검증 · " + AppInfo.Name;
}
