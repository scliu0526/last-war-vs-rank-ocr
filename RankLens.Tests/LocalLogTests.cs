using RankLens.App;

namespace RankLens.Tests;

public class LocalLogTests
{
    [Fact]
    public void RetentionDaysRemovesOlderLogFiles()
    {
        var folder = CreateFolder();
        try
        {
            var old = Path.Combine(folder, "old.log");
            File.WriteAllText(old, "old");
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-3));
            new LocalLog(new AppSettings { LogRetentionDays = 1, LogRetentionBytes = 1024 * 1024 }, folder).Write("Info", "current");
            Assert.False(File.Exists(old));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void RetentionBytesRemovesOldestFilesUntilWithinLimit()
    {
        var folder = CreateFolder();
        try
        {
            var oldest = Path.Combine(folder, "oldest.log");
            File.WriteAllText(oldest, new string('x', 80));
            File.SetLastWriteTimeUtc(oldest, DateTime.UtcNow.AddMinutes(-2));
            var newer = Path.Combine(folder, "newer.log");
            File.WriteAllText(newer, new string('y', 80));
            File.SetLastWriteTimeUtc(newer, DateTime.UtcNow.AddMinutes(-1));
            new LocalLog(new AppSettings { LogRetentionDays = 30, LogRetentionBytes = 100 }, folder).Write("Info", "current");
            Assert.False(File.Exists(oldest));
            Assert.True(Directory.GetFiles(folder, "*.log").Sum(path => new FileInfo(path).Length) <= 100);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void StructuredStageLogIncludesVersionStageAndDuration()
    {
        var folder = CreateFolder();
        try
        {
            new LocalLog(new AppSettings(), folder).WriteStage(
                "Info", "Recognition", "Images=1 Candidates=2.", TimeSpan.FromMilliseconds(12));
            var text = File.ReadAllText(Directory.GetFiles(folder, "*.log").Single());
            Assert.Contains("Version=", text);
            Assert.Contains("Stage=Recognition", text);
            Assert.Contains("ElapsedMs=12", text);
            Assert.DoesNotContain("Commander", text);
        }
        finally { Directory.Delete(folder, true); }
    }

    private static string CreateFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ranklens-log-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
