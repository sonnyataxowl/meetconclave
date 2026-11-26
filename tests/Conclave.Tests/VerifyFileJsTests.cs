using System.Diagnostics;
using System.Security.Cryptography;
using Xunit;

namespace Conclave.Tests;

public class VerifyFileJsTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string Js(string name) => Path.Combine(Root(), "src", "Conclave.Client", "wwwroot", "js", name);

    // The recording must never leave the browser: the hashing modules may not use any network API.
    [Theory]
    [InlineData("verify-file.js")]
    [InlineData("sha256.js")]
    public void HashingModules_HaveNoNetworkCalls(string file)
    {
        var src = File.ReadAllText(Js(file));
        foreach (var banned in new[] { "fetch(", "XMLHttpRequest", "sendBeacon", "WebSocket", "FormData" })
            Assert.DoesNotContain(banned, src);
    }

    [Fact]
    public void FileCheckCard_UsesStreamingModuleWithProgress()
    {
        var razor = File.ReadAllText(Path.Combine(Root(), "src", "Conclave.Client", "Pages", "FileCheckCard.razor"));
        Assert.Contains("./js/verify-file.js", razor);
        Assert.Contains("OnHashProgress", razor);
        var page = File.ReadAllText(Path.Combine(Root(), "src", "Conclave.Client", "Pages", "Verify.razor"));
        Assert.Contains("<FileCheckCard", page);
    }

    // Runs under Node (skipped when node is missing) with System.Security.Cryptography as the reference.
    [Fact]
    public void HashFile_MatchesReference_ReportsProgress_AndDetectsOneByteChange()
    {
        var script = @"
const { hashFile } = await import(process.argv[1]);
const big = new Uint8Array(9 * 1024 * 1024 + 5); for (let i = 0; i < big.length; i++) big[i] = (i * 13 + 1) & 255;
const calls = [];
const a = await hashFile(new Blob([big]), (d, t) => calls.push([d, t]));
const changed = big.slice(); changed[4000000] ^= 1;
const b = await hashFile(new Blob([changed]));
const e = await hashFile(new Blob([]));
console.log([a.sha256, a.bytes, b.sha256, e.sha256, e.bytes, calls.length > 0, calls[calls.length - 1].join('/')].join('\n'));
";
        string stdout;
        try
        {
            var psi = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add("--input-type=module"); psi.ArgumentList.Add("-e"); psi.ArgumentList.Add(script);
            psi.ArgumentList.Add(new Uri(Js("verify-file.js")).AbsoluteUri);
            using var p = Process.Start(psi)!;
            stdout = p.StandardOutput.ReadToEnd();
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            Assert.True(p.ExitCode == 0, err);
        }
        catch (System.ComponentModel.Win32Exception) { return; }

        var big = new byte[9 * 1024 * 1024 + 5]; for (var i = 0; i < big.Length; i++) big[i] = (byte)((i * 13 + 1) & 255);
        string Hex(byte[] x) => Convert.ToHexString(SHA256.HashData(x)).ToLowerInvariant();
        var changed = (byte[])big.Clone(); changed[4000000] ^= 1;
        var expected = new[] { Hex(big), big.Length.ToString(), Hex(changed), Hex(Array.Empty<byte>()), "0", "true", $"{big.Length}/{big.Length}" };
        Assert.Equal(expected, stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        Assert.NotEqual(Hex(big), Hex(changed));
    }
}
