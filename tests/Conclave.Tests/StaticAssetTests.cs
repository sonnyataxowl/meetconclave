using Xunit;

namespace Conclave.Tests;

public class StaticAssetTests
{
    private static string ClientWwwroot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "Conclave.Client", "wwwroot");
    }

    [Fact]
    public void IndexHtml_LinksDarkStylesheet()
    {
        var html = File.ReadAllText(Path.Combine(ClientWwwroot(), "index.html"));
        Assert.Contains("css/app.css", html);
    }

    [Fact]
    public void AppCss_DeclaresDarkTheme()
    {
        var css = File.ReadAllText(Path.Combine(ClientWwwroot(), "css", "app.css"));
        Assert.Contains("color-scheme: dark", css);
        Assert.Contains(".cc-header", css);
        Assert.Contains(".cc-footer", css);
    }
}
