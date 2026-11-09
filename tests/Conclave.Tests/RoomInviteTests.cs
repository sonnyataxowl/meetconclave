using Xunit;

namespace Conclave.Tests;

public class RoomInviteTests
{
    private static string RoomSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "Conclave.Client", "Pages", "Room.razor"));
    }

    [Fact]
    public void Room_CopiesInviteLink_AndAutoJoinsSignedInVisitors()
    {
        var src = RoomSource();
        Assert.Contains("초대 링크 복사", src);
        Assert.Contains("_user is not null && m.SealId is null && m.EndedAt is null) await Join()", src);
        Assert.Contains("LoginToJoin", src);
    }

    [Fact]
    public void Room_ExplainsMissingAndEndedMeetings()
    {
        var src = RoomSource();
        Assert.Contains("회의를 찾을 수 없습니다", src);
        Assert.Contains("이미 종료되어 입장할 수 없습니다", src);
    }
}
