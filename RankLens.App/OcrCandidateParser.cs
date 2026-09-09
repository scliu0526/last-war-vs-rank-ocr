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
        if (ContainsAny(text, "星期一", "Monday", "月曜日", "월요일", "วันจันทร์")) return RankingCategory.Monday;
        if (ContainsAny(text, "星期二", "Tuesday", "火曜日", "화요일", "วันอังคาร")) return RankingCategory.Tuesday;
        if (ContainsAny(text, "星期三", "Wednesday", "水曜日", "수요일", "วันพุธ")) return RankingCategory.Wednesday;
        if (ContainsAny(text, "星期四", "Thursday", "木曜日", "목요일", "วันพฤหัสบดี")) return RankingCategory.Thursday;
        if (ContainsAny(text, "星期五", "Friday", "金曜日", "금요일", "วันศุกร์")) return RankingCategory.Friday;
        if (ContainsAny(text, "星期六", "Saturday", "土曜日", "토요일", "วันเสาร์")) return RankingCategory.Saturday;
        return ContainsAny(text, "本週排名", "Weekly Ranking", "週ランキング", "주간 순위", "อันดับประจำสัปดาห์")
            ? RankingCategory.Weekly
            : RankingCategory.PendingClassification;
    }

    public static bool IsCategoryResolved(IEnumerable<string> lines)
    {
        var text = string.Join(" ", lines);
        return ContainsAny(text, "本週排名", "Weekly Ranking", "週ランキング", "주간 순위", "อันดับประจำสัปดาห์",
            "星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday",
            "月曜日", "火曜日", "水曜日", "木曜日", "金曜日", "土曜日", "월요일", "화요일", "수요일", "목요일", "금요일", "토요일",
            "วันจันทร์", "วันอังคาร", "วันพุธ", "วันพฤหัสบดี", "วันศุกร์", "วันเสาร์");
    }

    private static bool ContainsAny(string text, params string[] values) => values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
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
                SourceImage = sourceImage,
                SourceTop = line.Top,
                SourceBottom = line.Bottom
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
                IsSelected = true, SourceImage = sourceImage,
                SourceTop = lines[index].Top,
                SourceBottom = lines[scoreIndex].Bottom
            });
            index = scoreIndex;
        }
        return result;
    }
}
