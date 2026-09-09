using System.Globalization;
using System.Text.RegularExpressions;

namespace RankLens.App;

public sealed record OcrTextLine(string Text, float Confidence, int Top, int Bottom);

public static partial class OcrCandidateParser
{
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
            : RankingCategory.Weekly;
    }
    [GeneratedRegex(@"^\s*(?<rank>\d{1,3})\s+(?<name>.+?)\s+(?<score>[\d,\s]+)\s*$")]
    private static partial Regex RankLineRegex();

    [GeneratedRegex(@"^\s*(?<rank>\d{1,3})\s+(?<name>[^\t|]+?)[\t|]+(?<alliance>[^\t|]+?)[\t|]+(?<score>[\d,\s]+)\s*$")]
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
                || !long.TryParse(match.Groups["score"].Value.Replace(",", string.Empty).Replace(" ", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
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
            var match = Regex.Match(lines[index].Text, @"^\s*(?<rank>\d{1,3})\s+(?<score>[\d,\s]+)\s*$");
            if (!match.Success || lines[index].Confidence < confidenceThreshold) continue;
            if (!int.TryParse(match.Groups["rank"].Value, out var rank)
                || !long.TryParse(match.Groups["score"].Value.Replace(",", string.Empty).Replace(" ", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var score)
                || index + 2 >= lines.Count) continue;

            var commander = lines[index + 1];
            var alliance = lines[index + 2];
            if (commander.Confidence < confidenceThreshold || alliance.Confidence < confidenceThreshold) continue;
            result.Add(new RankingCandidate
            {
                Category = category, Rank = rank,
                CommanderName = commander.Text.Trim(), AllianceName = alliance.Text.Trim(), Score = score,
                IsSelected = true, SourceImage = sourceImage
            });
            index += 2;
        }
        return result;
    }
}
