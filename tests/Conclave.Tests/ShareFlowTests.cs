using Xunit;

namespace Conclave.Tests;

/// <summary>Static checks that the share flow stays wired: card -> /verify link, copy button, preview meta.</summary>
public class ShareFlowTests
{
    private static string Src(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName, "src", "Conclave.Client" }.Concat(parts).ToArray()));
    }

    [Fact]
    public void VerifyPage_HasCopyButtonAndDynamicTitle()
    {
        var page = Src("Pages", "Verify.razor");
        Assert.Contains("<ShareLink", page);
        Assert.Contains("VerifyLink.Title(", page);
        Assert.Contains("VerifyLink.ErrorMessage(", page);
    }

    [Fact]
    public void SealedCard_LinksToVerifyAndCanCopy()
    {
        var room = Src("Pages", "Room.razor");
        Assert.Contains("/verify/@_sealed.SealId", room);
        Assert.Contains("<ShareLink", room);
    }

    [Fact]
    public void ShareLink_UsesClipboardHelper()
    {
        Assert.Contains("copyText", Src("Pages", "ShareLink.razor"));
        Assert.Contains("export async function copyText", Src("wwwroot", "js", "conclave.js"));
    }

    [Fact]
    public void IndexHtml_HasPreviewMeta()
    {
        var html = Src("wwwroot", "index.html");
        Assert.Contains("property=\"og:title\"", html);
        Assert.Contains("name=\"description\"", html);
    }
}
