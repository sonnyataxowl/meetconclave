using Conclave.Web;
using Xunit;

namespace Conclave.Web.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_IsConclave()
    {
        Assert.Equal("Conclave", AppInfo.Name);
    }

    [Fact]
    public void Tagline_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Tagline));
    }
}
