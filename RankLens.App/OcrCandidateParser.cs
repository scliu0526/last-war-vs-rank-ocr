using System.Globalization;
using System.Text.RegularExpressions;

namespace RankLens.App;

public sealed record OcrTextLine(string Text, float Confidence, int Top, int Bottom, int Left = 0);

public static partial class OcrCandidateParser
{
    private static string NormalizeScore(string value) => value
        .Replace(",", string.Empty, StringComparison.Ordinal)
        .Replace("，", string.Empty, StringComparison.Ordinal)
        .Replace(" ", string.Empty, StringComparison.Ordinal)
        .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
        .Replace("\u202F", string.Empty, StringComparison.Ordinal);

    private static string DigitsOnly(string value) => new(value.Where(char.IsDigit).ToArray());
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
            if (!match.Success)
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
                RankConfidence = line.Confidence,
                CommanderConfidence = line.Confidence,
                AllianceConfidence = structured ? line.Confidence : 0,
                ScoreConfidence = line.Confidence,
                NoAllianceConfirmed = structured && string.Equals(match.Groups["alliance"].Value.Trim(), "無同盟", StringComparison.Ordinal),
                IsSelected = false,
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
        var ordered = lines.OrderBy(line => line.Top).ThenBy(line => line.Left).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var match = Regex.Match(ordered[index].Text, @"^\s*(?<rank>\d{1,3})(?:\s+(?<score>[\d,\s]+))?\s*$");
            if (!match.Success) continue;
            if (!int.TryParse(match.Groups["rank"].Value, out var rank)) continue;
            var hasInlineScore = match.Groups["score"].Success;
            var nextRank = index + 1;
            while (nextRank < ordered.Length
                && (nextRank - index < 4
                    || (!Regex.IsMatch(ordered[nextRank].Text, @"^\s*\d{1,3}(?:\s+[\d,\s]+)?\s*$")
                        && ordered[nextRank].Top - ordered[index].Top <= 90))) nextRank++;
            var group = ordered[(index + 1)..nextRank];
            if (hasInlineScore)
            {
                group = [ordered[index]];
            }
            var scoreLine = group
                .Select(line => (Line: line, Match: Regex.Match(line.Text, @"[\d][\d,，\s]*")))
                .Where(item => item.Match.Success)
                .OrderByDescending(item => NormalizeScore(item.Match.Value).Length)
                .FirstOrDefault();
            var scoreText = hasInlineScore ? match.Groups["score"].Value : scoreLine.Match?.Value ?? string.Empty;
            var textLines = group.Where(line => !ReferenceEquals(line, scoreLine.Line)
                && !Regex.IsMatch(line.Text, @"^\s*[\d,，\s]+\s*$"))
                .Select(line => line.Text.Trim()).Where(text => text.Length > 0).ToArray();
            if (!long.TryParse(DigitsOnly(scoreText), NumberStyles.Integer, CultureInfo.InvariantCulture, out var score)
                || textLines.Length == 0) continue;
            var commander = textLines[0];
            var alliance = textLines.Length > 1 ? string.Join(" ", textLines.Skip(1)) : string.Empty;
            var scoreConfidence = hasInlineScore ? ordered[index].Confidence : scoreLine.Line?.Confidence ?? 0;
            result.Add(new RankingCandidate
            {
                Category = category, Rank = rank,
                CommanderName = commander, AllianceName = alliance, Score = score,
                RankConfidence = ordered[index].Confidence,
                CommanderConfidence = ordered[index].Confidence,
                AllianceConfidence = alliance.Length == 0 ? 0 : ordered[index].Confidence,
                ScoreConfidence = scoreConfidence,
                NoAllianceConfirmed = string.Equals(alliance, "無同盟", StringComparison.Ordinal),
                IsSelected = false, SourceImage = sourceImage,
                SourceTop = ordered[index].Top,
                SourceBottom = group.Length == 0 ? ordered[index].Bottom : group.Max(line => line.Bottom)
            });
            index = Math.Max(index, nextRank - 1);
        }
        return result;
    }
}
