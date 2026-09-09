namespace RankLens.App;

public enum RankingCategory
{
    Weekly,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday
}

public readonly record struct RankingWeek(DateOnly Monday)
{
    public static RankingWeek Current(DateOnly today)
    {
        var offset = ((int)today.DayOfWeek + 6) % 7;
        return new RankingWeek(today.AddDays(-offset));
    }

    public static bool TryCreate(DateOnly monday, out RankingWeek week)
    {
        if (monday.DayOfWeek != DayOfWeek.Monday)
        {
            week = default;
            return false;
        }

        week = new RankingWeek(monday);
        return true;
    }

    public string FileName => $"{Monday:yyyy-MM-dd}.xlsx";
}

public sealed class RankingCandidate
{
    public required RankingCategory Category { get; init; }
    public required int Rank { get; init; }
    public required string CommanderName { get; set; }
    public required string AllianceName { get; set; }
    public required long Score { get; set; }
    public bool NoAllianceConfirmed { get; set; }
    public bool IsSelected { get; set; }
    public string SourceImage { get; init; } = string.Empty;

    public bool IsValid => Rank is >= 1 and <= 200
        && !string.IsNullOrWhiteSpace(CommanderName)
        && Score >= 0
        && (!string.IsNullOrWhiteSpace(AllianceName) || NoAllianceConfirmed);
}

public interface IRecognitionSource
{
    Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(
        IReadOnlyList<string> imagePaths,
        CancellationToken cancellationToken = default);
}

public sealed class FixedRecognitionSource(IReadOnlyList<RankingCandidate> candidates) : IRecognitionSource
{
    public Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(
        IReadOnlyList<string> imagePaths,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(candidates);
    }
}

public sealed class ReviewSession(RankingWeek week, IReadOnlyList<RankingCandidate> candidates)
{
    public RankingWeek Week { get; } = week;
    public IReadOnlyList<RankingCandidate> Candidates { get; } = candidates;
}
