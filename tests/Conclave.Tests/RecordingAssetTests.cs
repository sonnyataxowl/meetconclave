using Xunit;

namespace Conclave.Tests;

public class RecordingAssetTests
{
    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray()));
    }

    [Fact]
    public void Js_ExportsStartAndStopRecording()
    {
        var js = RepoFile("src", "Conclave.Client", "wwwroot", "js", "conclave.js");
        Assert.Contains("export function startRecording", js);
        Assert.Contains("export async function stopRecording", js);
    }

    [Fact]
    public void Js_GuardsAgainstDoubleStartAndEmptyRecording()
    {
        var js = RepoFile("src", "Conclave.Client", "wwwroot", "js", "conclave.js");
        Assert.Contains("이미 녹화 중입니다", js);
        Assert.Contains("녹화된 데이터가 없습니다", js);
        Assert.Contains("recorder.onerror", js);
    }

    [Fact]
    public void Room_ShowsRecordButtonsOnlyToHost()
    {
        var razor = RepoFile("src", "Conclave.Client", "Pages", "Room.razor");
        Assert.Contains("@if (_isHost && !_recording", razor);
        Assert.Contains("@if (_isHost && _recording)", razor);
        Assert.Contains("● 녹화 중", razor);
    }

    [Fact]
    public void Room_ResetsRecordingFlagWhenStopFails()
    {
        var razor = RepoFile("src", "Conclave.Client", "Pages", "Room.razor");
        Assert.Contains("catch (Exception ex) { _recording = false;", razor);
    }
}
