using System.Diagnostics;
using System.Security.Cryptography;
using Conclave.Client.Services;
using Microsoft.JSInterop;
using Xunit;

namespace Conclave.Tests;

public class FingerprintTests
{
    [Theory]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", true)]
    [InlineData("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("g3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", false)]
    public void IsSha256Hex_AcceptsOnly64LowercaseHex(string? s, bool expected)
        => Assert.Equal(expected, FingerprintService.IsSha256Hex(s));

    [Fact]
    public async Task HashFileInput_ReturnsFingerprintFromBrowserModule()
    {
        var hash = Convert.ToHexString(SHA256.HashData("abc"u8)).ToLowerInvariant();
        var svc = new FingerprintService(new FakeJs(new FingerprintResult(hash, 3, "a.webm")));
        var r = await svc.HashFileInputAsync("f");
        Assert.Equal(hash, r!.Sha256);
        Assert.Equal(3, r.Bytes);
    }

    [Fact]
    public async Task HashFileInput_NothingChosen_ReturnsNull()
        => Assert.Null(await new FingerprintService(new FakeJs(null)).HashFileInputAsync("f"));

    [Fact]
    public async Task HashFileInput_RejectsMalformedHash()
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => new FingerprintService(new FakeJs(new FingerprintResult("xyz", 1, "a"))).HashFileInputAsync("f"));

    // The browser module itself runs under Node (if present) against System.Security.Cryptography as the reference.
    [Fact]
    public void Sha256Js_MatchesReferenceForKnownAndChunkedInputs()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var js = Path.Combine(dir!.FullName, "src", "Conclave.Client", "wwwroot", "js", "sha256.js");
        var script = @"
const { Sha256, hashBlob } = await import(process.argv[1]);
const enc = new TextEncoder();
const out = [];
const h = (u8) => new Sha256().update(u8).hex();
out.push(h(new Uint8Array(0)));
out.push(h(enc.encode('abc')));
out.push(h(enc.encode('abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq')));
const big = new Uint8Array(1000003); for (let i = 0; i < big.length; i++) big[i] = (i * 31 + 7) & 255;
out.push(h(big));
const s = new Sha256(); for (let o = 0; o < big.length; o += 777) s.update(big.subarray(o, Math.min(o + 777, big.length)));
out.push(s.hex());
out.push(await hashBlob(new Blob([big]), null, 65537));
console.log(out.join('\n'));
";
        string stdout;
        try
        {
            var psi = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add("--input-type=module"); psi.ArgumentList.Add("-e"); psi.ArgumentList.Add(script);
            psi.ArgumentList.Add(new Uri(js).AbsoluteUri);
            using var p = Process.Start(psi)!;
            stdout = p.StandardOutput.ReadToEnd();
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            Assert.True(p.ExitCode == 0, err);
        }
        catch (System.ComponentModel.Win32Exception) { return; } // node not installed: nothing to run

        var big = new byte[1000003]; for (var i = 0; i < big.Length; i++) big[i] = (byte)((i * 31 + 7) & 255);
        string H(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
        var expected = new[]
        {
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1",
            H(big), H(big), H(big),
        };
        Assert.Equal(expected, stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private sealed class FakeJs(FingerprintResult? result) : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args)
            => InvokeAsync<T>(identifier, CancellationToken.None, args);

        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args)
            => identifier == "import"
                ? new ValueTask<T>((T)(object)new FakeModule(result))
                : throw new InvalidOperationException(identifier);
    }

    private sealed class FakeModule(FingerprintResult? result) : IJSObjectReference
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args)
            => InvokeAsync<T>(identifier, CancellationToken.None, args);

        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args)
            => identifier == "hashFileInput" ? new ValueTask<T>((T)(object?)result!) : throw new InvalidOperationException(identifier);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
