using System.Text.Json;
using System.IO;

namespace RankLens.App;

public enum RecognitionExecutionMode
{
    Cpu,
    DirectML
}

public sealed class AppSettings
{
    public string OutputFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public double ConfidenceThreshold { get; set; } = 0.95;
    public RecognitionExecutionMode ExecutionMode { get; set; } = RecognitionExecutionMode.Cpu;
    public string? AdapterName { get; set; }
    public int LogRetentionDays { get; set; } = 30;
    public long LogRetentionBytes { get; set; } = 100 * 1024 * 1024;
}

public sealed class AppSettingsStore
{
    private readonly string settingsPath;

    public AppSettingsStore(string? settingsPath = null)
    {
        this.settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RankLens",
            "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath)) ?? new AppSettings();
            var defaults = new AppSettings();
            if (double.IsNaN(settings.ConfidenceThreshold) || double.IsInfinity(settings.ConfidenceThreshold)) settings.ConfidenceThreshold = defaults.ConfidenceThreshold;
            settings.ConfidenceThreshold = Math.Clamp(settings.ConfidenceThreshold, 0, 1);
            if (settings.LogRetentionDays <= 0) settings.LogRetentionDays = defaults.LogRetentionDays;
            if (settings.LogRetentionBytes <= 0) settings.LogRetentionBytes = defaults.LogRetentionBytes;
            if (string.IsNullOrWhiteSpace(settings.OutputFolder)) settings.OutputFolder = defaults.OutputFolder;
            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
