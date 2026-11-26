using Microsoft.JSInterop;

namespace Conclave.Client.Services;

public sealed record FingerprintResult(string Sha256, long Bytes, string Name);

/// <summary>Computes the SHA-256 fingerprint of a local file in the browser (wwwroot/js/sha256.js, chunked). The file is never sent to a server.</summary>
public sealed class FingerprintService(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _mod;
    private async ValueTask<IJSObjectReference> Js() => _mod ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/sha256.js");

    /// <summary>Hashes the file chosen in the &lt;input type=file&gt; with this element id; null when nothing is chosen.</summary>
    public async Task<FingerprintResult?> HashFileInputAsync(string inputId)
    {
        var r = await (await Js()).InvokeAsync<FingerprintResult?>("hashFileInput", inputId);
        if (r is not null && !IsSha256Hex(r.Sha256)) throw new InvalidOperationException("지문 계산 결과가 올바르지 않습니다.");
        return r;
    }

    /// <summary>True for exactly 64 lowercase hexadecimal characters.</summary>
    public static bool IsSha256Hex(string? s) => s is { Length: 64 } && s.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    public async ValueTask DisposeAsync() { if (_mod is not null) await _mod.DisposeAsync(); }
}
