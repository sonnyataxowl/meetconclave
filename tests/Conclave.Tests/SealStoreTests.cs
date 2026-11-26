using Xunit;

namespace Conclave.Tests;

public class SealStoreTests
{
    private static string Worker()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conclave.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "src", "Conclave.Client", "wwwroot", "_worker.js"));
    }

    [Fact]
    public void Worker_DefinesSealLookupAndSaveHelpers()
    {
        var js = Worker();
        Assert.Contains("async function getMeetingSeal(", js);
        Assert.Contains("async function saveSealResult(", js);
    }

    [Fact]
    public void SaveSealResult_SealsOnlyOnce_AndOnlyForHost()
    {
        var js = Worker();
        Assert.Contains("where id=$1 and host_sub=$2 and seal_id is null returning *", js);
    }

    [Fact]
    public void SealRoute_UsesSaveHelper()
    {
        var js = Worker();
        Assert.Contains("await saveSealResult(env,", js);
    }

    [Fact]
    public void SealState_LivesOnMeetingRow_NoRecordingsTable()
    {
        var js = Worker();
        Assert.DoesNotContain("create table if not exists recordings", js);
        foreach (var col in new[] { "recording_sha256", "recording_bytes", "recording_ms", "seal_id", "sealed_at", "axowl_event_id" })
            Assert.Contains(col, js);
    }
}
