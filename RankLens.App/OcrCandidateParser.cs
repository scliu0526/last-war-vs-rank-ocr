using System.Globalization;
using System.Text.RegularExpressions;

namespace RankLens.App;

public sealed record OcrTextLine(string Text, float Confidence, int Top, int Bottom);

public static partial class OcrCandidateParser
{
    [GeneratedRegex(@"^\s*(?<rank>\d{1,3})\s+(?<name>.+?)\s+(?<score>[\d,\s]+)\s*$")]
    private static partial Regex RankLineRegex();

    public static IReadOnlyList<RankingCandidate> Parse(
        RankingCategory category,
        string sourceImage,
        IReadOnlyList<OcrTextLine> lines,
        double confidenceThreshold = 0.95)
    {
        var result = new List<RankingCandidate>();
        foreach (var line in lines)
        {
            var match = RankLineRegex().Match(line.Text);
            if (!match.Success || line.Confidence < confidenceThreshold)
            {
                continue;
            }

            if (!int.TryParse(match.Groups["rank"].Value, out var rank)
                || !long.TryParse(match.Groups["score"].Value.Replace(",", string.Empty).Replace(" ", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
            {
                continue;
            }

            result.Add(new RankingCandidate
            {
                Category = category,
                Rank = rank,
                CommanderName = match.Groups["name"].Value.Trim(),
                AllianceName = string.Empty,
                Score = score,
                IsSelected = true,
                SourceImage = sourceImage
            });
        }

        return result;
    }
}
