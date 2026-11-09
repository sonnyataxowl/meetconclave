using Conclave.Client;
using Xunit;

namespace Conclave.Tests;

public class AppInfoTests
{
    [Fact]
    public void Name_IsConclave()
    {
        Assert.Equal("Conclave", AppInfo.Name);
    }

    [Fact]
    public void HomePath_IsRoot()
    {
        Assert.Equal("/", AppInfo.HomePath);
    }
}
