namespace Conclave.Tests;

public class FunctionsContractTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Conclave.sln not found");
    }

    [Theory]
    [InlineData("functions/_lib/http.js")]
    [InlineData("functions/api/_middleware.js")]
    [InlineData("functions/api/health.js")]
    [InlineData("functions/api/[[path]].js")]
    public void Function_file_exists(string path)
        => Assert.True(File.Exists(Path.Combine(RepoRoot(), path)), path);

    [Fact]
    public void Helper_defines_error_shape()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "functions/_lib/http.js"));
        Assert.Contains("ok: false, error: { code, message }", src);
        Assert.Contains("ok: true, data", src);
    }
}
