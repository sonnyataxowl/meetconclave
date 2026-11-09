using Xunit;

namespace Conclave.Tests;

public class DbContractTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Conclave.sln not found");
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(RepoRoot(), path));

    [Theory]
    [InlineData("package.json")]
    [InlineData("functions/_lib/db.js")]
    [InlineData("functions/api/health/db.js")]
    [InlineData("docs/migrations.md")]
    public void Db_file_exists(string path)
        => Assert.True(File.Exists(Path.Combine(RepoRoot(), path)), path);

    [Fact]
    public void Db_helper_reads_the_DATABASE_URL_secret_and_closes_connection()
    {
        var src = Read("functions/_lib/db.js");
        Assert.Contains("env.DATABASE_URL", src);
        Assert.Contains("sql.end(", src);
    }

    [Fact]
    public void Health_db_uses_the_error_contract()
    {
        var src = Read("functions/api/health/db.js");
        Assert.Contains("db_not_configured", src);
        Assert.Contains("db_unreachable", src);
        Assert.Contains("../../_lib/http.js", src);
    }

    [Fact]
    public void Package_declares_postgres_driver()
        => Assert.Contains("\"postgres\"", Read("package.json"));

    [Fact]
    public void Migration_doc_documents_the_request()
        => Assert.Contains("axowl_request_migration", Read("docs/migrations.md"));
}
