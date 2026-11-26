using Xunit;

namespace Conclave.Tests;

public class VerifyEndpointTests
{
    private static string VerifyBlock()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var js = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Conclave.Client", "wwwroot", "_worker.js"));
        var start = js.IndexOf("/api\\/verify", StringComparison.Ordinal);
        Assert.True(start >= 0, "verify route missing");
        var end = js.IndexOf("return json({ error: 'not_found' }, 404);\n}", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return js.Substring(start, end - start);
    }

    [Fact]
    public void Verify_IsPublic_NoSignInRequired()
    {
        var block = VerifyBlock();
        Assert.DoesNotContain("requireUser", block);
        Assert.DoesNotContain("optionalUser", block);
    }

    [Fact]
    public void Verify_ReadsOnlyPublicColumns()
    {
        var block = VerifyBlock();
        Assert.DoesNotContain("select *", block);
        Assert.DoesNotContain("host_sub", block);
        Assert.Contains("recording_sha256", block);
    }

    [Fact]
    public void Verify_OnlyReturnsSealedRecords_And404sOtherwise()
    {
        var block = VerifyBlock();
        Assert.Contains("seal_id is not null", block);
        Assert.Contains("'not_found' }, 404", block);
    }
}
