using System.Diagnostics;
using Xunit;

namespace Conclave.Tests;

public class SealRouteTests
{
    // Runs tests/js/seal.test.mjs under Node (skipped silently when node is not installed).
    [Fact]
    public void SealRoute_HostCheck_Ledger_Retries_AndDuplicateGuard()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var test = Path.Combine(dir!.FullName, "tests", "js", "seal.test.mjs");
        var worker = Path.Combine(dir.FullName, "src", "Conclave.Client", "wwwroot", "_worker.js");
        try
        {
            var psi = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add(test);
            psi.ArgumentList.Add(new Uri(worker).AbsoluteUri);
            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();
            Assert.True(p.ExitCode == 0, stderr + stdout);
            Assert.Contains("OK", stdout);
        }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
