using Conclave.Client.Pages;
using Xunit;

namespace Conclave.Client.Tests;

public class HomeTests
{
    [Fact]
    public void Home_title_is_conclave()
    {
        Assert.Equal("Conclave", Home.Title);
    }
}
