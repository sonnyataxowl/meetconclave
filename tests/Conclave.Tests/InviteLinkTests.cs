using Xunit;

namespace Conclave.Tests;

public class InviteLinkTests
{
    private static string Room()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "Conclave.Client", "Pages", "Room.razor"));
    }

    [Fact]
    public void Room_CopiesCleanInviteLink()
    {
        var room = Room();
        Assert.Contains("초대 링크 복사", room);
        Assert.Contains("/m/{Id}", room);
        Assert.DoesNotContain("\"copyText\", Nav.Uri", room);
    }

    [Fact]
    public void Room_ExplainsMissingAndEndedMeetings()
    {
        var room = Room();
        Assert.Contains("회의를 찾을 수 없습니다", room);
        Assert.Contains("이미 종료된 회의", room);
    }

    [Fact]
    public void Room_LoginReturnsToMeetingAndAutoJoins()
    {
        var room = Room();
        Assert.Contains("?autojoin=1", room);
        Assert.Contains("Nav.Uri.Contains(\"autojoin=1\")", room);
    }
}
