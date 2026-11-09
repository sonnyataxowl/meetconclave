using Xunit;

namespace Conclave.Tests;

public class RoomInviteTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string Room() => File.ReadAllText(Path.Combine(Root(), "src", "Conclave.Client", "Pages", "Room.razor"));

    [Fact]
    public void Room_IsReachableByInviteLink()
    {
        Assert.Contains("@page \"/m/{Id}\"", Room());
    }

    [Fact]
    public void Room_CopiesCleanInviteUrl()
    {
        var razor = Room();
        Assert.Contains("InviteUrl", razor);
        Assert.Contains("\"/m/\" + Id", razor);
    }

    [Fact]
    public void Room_LoginReturnsToSameRoomAndAutoJoins()
    {
        var razor = Room();
        Assert.Contains("LoginAsync(\"/m/\" + Id)", razor);
        Assert.Contains("_user is not null) await Join()", razor);
    }

    [Fact]
    public void Room_ShowsNotFoundAndEndedNotices()
    {
        var razor = Room();
        Assert.Contains("회의를 찾을 수 없습니다", razor);
        Assert.Contains("_meeting.EndedAt is not null", razor);
        Assert.Contains("err == \"not_found\"", razor);
    }
}
