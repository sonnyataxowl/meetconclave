using Xunit;

namespace Conclave.Tests;

public class RoomInviteTests
{
    private static string RoomPage()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "Conclave.Client", "Pages", "Room.razor"));
    }

    [Fact]
    public void Room_CopiesInviteLink()
    {
        var page = RoomPage();
        Assert.Contains("초대 링크 복사", page);
        Assert.Contains("copyText", page);
    }

    [Fact]
    public void Room_ShowsNotFoundAndEndedNotices()
    {
        var page = RoomPage();
        Assert.Contains("회의를 찾을 수 없습니다", page);
        Assert.Contains("이 회의는 이미 끝났습니다", page);
    }

    [Fact]
    public void Room_AutoJoinsSignedInUser_AndReturnsToInviteAfterLogin()
    {
        var page = RoomPage();
        Assert.Contains("_user is not null) await Join()", page);
        Assert.Contains("Api.LoginAsync(\"/\" + Nav.ToBaseRelativePath(Nav.Uri))", page);
    }
}
