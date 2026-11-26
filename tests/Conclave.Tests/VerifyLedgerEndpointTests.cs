using Xunit;

namespace Conclave.Tests;

/// <summary>Static checks on the edge function (no JS runtime in CI): the ledger relay exists and keeps the org API key server-side.</summary>
public class VerifyLedgerEndpointTests
{
    private static string Worker()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "Conclave.Client", "wwwroot", "_worker.js"));
    }

    [Fact]
    public void Worker_RelaysLedgerEventWithApiKeyHeader()
    {
        var js = Worker();
        Assert.Contains(@"\/ledger$", js);
        Assert.Contains("/ledger/events/", js);
        Assert.Contains("'X-Api-Key': env.AXOWL_API_KEY", js);
    }

    [Fact]
    public void Worker_NeverPutsTheApiKeyInAResponse()
    {
        var js = Worker();
        var keyLines = js.Split('\n').Where(l => l.Contains("env.AXOWL_API_KEY")).ToList();
        Assert.NotEmpty(keyLines);

        // A "not configured" guard (!env.AXOWL_API_KEY) may answer with json(...), but only with static text — never interpolating anything.
        foreach (var line in keyLines.Where(l => l.Contains("!env.AXOWL_API_KEY")))
            Assert.DoesNotContain("${", line);

        // Every other line that reads the key (e.g. the outgoing header) must not build a response.
        foreach (var line in keyLines.Where(l => !l.Contains("!env.AXOWL_API_KEY")))
            Assert.DoesNotContain("json(", line);
    }
}
