namespace Conclave.Client.Services;

public enum VerifyStatus { Loading, Found, NotFound, Error }

/// <summary>Maps the result of GET /api/verify/{id} to what the page shows.</summary>
public static class VerifyView
{
    public static VerifyStatus Classify(VerifyInfo? info, string? error)
    {
        if (info is not null) return VerifyStatus.Found;
        if (string.IsNullOrEmpty(error)) return VerifyStatus.NotFound;
        // The edge function answers 404 with {error:'not_found'}; a bare 404 (no JSON body) surfaces as 'HTTP 404'.
        if (error == "not_found" || error == "HTTP 404") return VerifyStatus.NotFound;
        return VerifyStatus.Error;
    }
}
