using System.IO;

namespace RankLens.App;

public sealed class LocalLog
{
    private readonly string folder;
    private readonly AppSettings settings;

    public LocalLog(AppSettings settings, string? folderOverride = null)
    {
        this.settings = settings;
        folder = folderOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RankLens", "Logs");
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (UnauthorizedAccessException)
        {
            folder = Path.Combine(Path.GetTempPath(), "RankLens", "Logs");
            Directory.CreateDirectory(folder);
        }
    }

    public void Write(string level, string message)
    {
        var safeMessage = message.Replace("\r", " ").Replace("\n", " ");
        var path = Path.Combine(folder, $"{DateTime.Now:yyyy-MM-dd}.log");
        File.AppendAllText(path, $"{DateTimeOffset.Now:O} [{level}] {safeMessage}{Environment.NewLine}");
        Prune();
    }

    private void Prune()
    {
        var files = new DirectoryInfo(folder).GetFiles("*.log").OrderBy(file => file.LastWriteTimeUtc).ToList();
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, settings.LogRetentionDays));
        foreach (var file in files.Where(file => file.LastWriteTimeUtc < cutoff).ToList())
        {
            file.Delete();
            files.Remove(file);
        }

        var total = files.Sum(file => file.Length);
        foreach (var file in files)
        {
            if (total <= Math.Max(1, settings.LogRetentionBytes))
            {
                break;
            }

            total -= file.Length;
            file.Delete();
        }
    }
}
