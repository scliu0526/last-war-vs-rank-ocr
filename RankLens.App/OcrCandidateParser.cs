using System.Globalization;
using System.Text.RegularExpressions;

namespace RankLens.App;

public sealed record OcrTextLine(string Text, float Confidence, int Top, int Bottom);

public static partial class OcrCandidateParser
{
    private static string NormalizeScore(string value) => value
        .Replace(",", string.Empty, StringComparison.Ordinal)
        .Replace("，", string.Empty, StringComparison.Ordinal)
        .Replace(" ", string.Empty, StringComparison.Ordinal)
        .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
        .Replace("\u202F", string.Empty, StringComparison.Ordinal);
    public static RankingCategory DetectCategory(IEnumerable<string> lines)
    {
        var text = string.Join(" ", lines);
        if (text.Contains("星期一", StringComparison.OrdinalIgnoreCase)) return RankingCategory.Monday;
        if (text.Contains("星期二", StringComparison.OrdinalIgnoreCase)) return RankingCategory.Tuesday;
        if (text.Contains("星期三", StringComparison.OrdinalIgnoreCase)) return RankingCategory.Wednesday;
        if (text.Contains("星期四", StringComparison.OrdinalIgnoreCase)) return RankingCategory.Thursday;
        if (text.Contains("星期五", StringComparison.OrdinalIgnoreCase)) return RankingCategory.Friday;
        if (text.Contains("星期六", StringComparison.OrdinalIgnoreCase)) return RankingCategory.Saturday;
        return text.Contains("本週排名", StringComparison.OrdinalIgnoreCase)
            ? RankingCategory.Weekly
            : RankingCategory.PendingClassification;
    }

    public static bool IsCategoryResolved(IEnumerable<string> lines)
    {
        var text = string.Join(" ", lines);
        return text.Contains("本週排名", StringComparison.OrdinalIgnoreCase)
            || text.Contains("星期一", StringComparison.OrdinalIgnoreCase)
            || text.Contains("星期二", StringComparison.OrdinalIgnoreCase)
            || text.Contains("星期三", StringComparison.OrdinalIgnoreCase)
            || text.Contains("星期四", StringComparison.OrdinalIgnoreCase)
            || text.Contains("星期五", StringComparison.OrdinalIgnoreCase)
            || text.Contains("星期六", StringComparison.OrdinalIgnoreCase);
    }
    [GeneratedRegex(@"^\s*(?<rank>\d{1,3})\s+(?<name>.+?)\s+(?<score>[\d,，\s\u00A0\u202F]+)\s*$")]
    private static partial Regex RankLineRegex();

    [GeneratedRegex(@"^\s*(?<rank>\d{1,3})\s+(?<name>[^\t|]+?)[\t|]+(?<alliance>[^\t|]+?)[\t|]+(?<score>[\d,，\s\u00A0\u202F]+)\s*$")]
    private static partial Regex StructuredRankLineRegex();

    public static IReadOnlyList<RankingCandidate> ParsePlainText(
        RankingCategory category,
        string sourceImage,
        IEnumerable<string> lines,
        double confidence = 1)
    {
        var ocrLines = lines.Select((text, index) => new OcrTextLine(text, (float)confidence, index, index));
        return Parse(category, sourceImage, ocrLines.ToArray(), confidence);
    }

    public static IReadOnlyList<RankingCandidate> Parse(
        RankingCategory category,
        string sourceImage,
        IReadOnlyList<OcrTextLine> lines,
        double confidenceThreshold = 0.95)
    {
        var result = new List<RankingCandidate>();
        foreach (var line in lines)
        {
            var match = StructuredRankLineRegex().Match(line.Text);
            var structured = match.Success;
            if (!structured) match = RankLineRegex().Match(line.Text);
            if (!match.Success || line.Confidence < confidenceThreshold)
            {
                continue;
            }

            if (!int.TryParse(match.Groups["rank"].Value, out var rank)
                || !long.TryParse(NormalizeScore(match.Groups["score"].Value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
            {
                continue;
            }

            result.Add(new RankingCandidate
            {
                Category = category,
                Rank = rank,
                CommanderName = match.Groups["name"].Value.Trim(),
                AllianceName = structured ? match.Groups["alliance"].Value.Trim() : string.Empty,
                Score = score,
                NoAllianceConfirmed = structured && string.Equals(match.Groups["alliance"].Value.Trim(), "無同盟", StringComparison.Ordinal),
                IsSelected = true,
                SourceImage = sourceImage
            });
        }

        return result;
    }

    public static IReadOnlyList<RankingCandidate> ParseRows(
        RankingCategory category,
        string sourceImage,
        IReadOnlyList<OcrTextLine> lines,
        double confidenceThreshold = 0.95)
    {
        var result = new List<RankingCandidate>();
        for (var index = 0; index < lines.Count; index++)
        {
            var match = Regex.Match(lines[index].Text, @"^\s*(?<rank>\d{1,3})(?:\s+(?<score>[\d,\s]+))?\s*$");
            if (!match.Success || lines[index].Confidence < confidenceThreshold) continue;
            if (!int.TryParse(match.Groups["rank"].Value, out var rank)) continue;
            var hasInlineScore = match.Groups["score"].Success;
            var commanderIndex = index + 1;
            var allianceIndex = commanderIndex + 1;
            var scoreIndex = hasInlineScore ? index : allianceIndex + 1;
            if (scoreIndex >= lines.Count) continue;
            var commander = lines[commanderIndex];
            var alliance = lines[allianceIndex];
            var scoreText = hasInlineScore ? match.Groups["score"].Value : lines[scoreIndex].Text;
            if (!long.TryParse(NormalizeScore(scoreText), NumberStyles.Integer, CultureInfo.InvariantCulture, out var score)) continue;
            if (commander.Confidence < confidenceThreshold || alliance.Confidence < confidenceThreshold) continue;
            result.Add(new RankingCandidate
            {
                Category = category, Rank = rank,
                CommanderName = commander.Text.Trim(), AllianceName = alliance.Text.Trim(), Score = score,
                IsSelected = true, SourceImage = sourceImage
            });
            index = scoreIndex;
        }
        return result;
    }
}
