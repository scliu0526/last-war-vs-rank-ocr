using RankLens.App;

namespace RankLens.Tests;

public class OcrCandidateParserTests
{
    [Fact]
    public void ParseRowsAssociatesFourLineRankingLayout()
    {
        var lines = new[]
        {
            new OcrTextLine("1", 0.99f, 0, 10),
            new OcrTextLine("CommanderAlpha", 0.99f, 11, 20),
            new OcrTextLine("[DEMO] 測試聯盟", 0.99f, 21, 30),
            new OcrTextLine("12,345,678", 0.99f, 31, 40)
        };

        var result = OcrCandidateParser.ParseRows(RankingCategory.Monday, "fixture.jpg", lines);

        var candidate = Assert.Single(result);
        Assert.Equal(1, candidate.Rank);
        Assert.Equal("CommanderAlpha", candidate.CommanderName);
        Assert.Equal("[DEMO] 測試聯盟", candidate.AllianceName);
        Assert.Equal(12345678, candidate.Score);
        Assert.Equal(0, candidate.SourceTop);
        Assert.Equal(40, candidate.SourceBottom);
    }

    [Fact]
    public void ParseAcceptsFullWidthScoreSeparators()
    {
        var result = OcrCandidateParser.Parse(
            RankingCategory.Monday, "fixture.jpg",
            [new OcrTextLine("2 Commander ７２，１３８，５６９", 0.99f, 0, 1)], 0.95);

        Assert.Empty(result); // full-width digits remain untrusted rather than being silently rewritten
        var normalized = OcrCandidateParser.Parse(
            RankingCategory.Monday, "fixture.jpg",
            [new OcrTextLine("2 Commander 12，345，678", 0.99f, 0, 1)], 0.95);
        Assert.Equal(12345678, Assert.Single(normalized).Score);
    }

    [Theory]
    [InlineData("Monday", RankingCategory.Monday)]
    [InlineData("月曜日", RankingCategory.Monday)]
    [InlineData("화요일", RankingCategory.Tuesday)]
    [InlineData("วันพุธ", RankingCategory.Wednesday)]
    [InlineData("Weekly Ranking", RankingCategory.Weekly)]
    public void DetectCategoryRecognizesSupportedLanguageLabels(string label, RankingCategory expected)
    {
        Assert.Equal(expected, OcrCandidateParser.DetectCategory([label]));
        Assert.True(OcrCandidateParser.IsCategoryResolved([label]));
    }

    [Fact]
    public void ParseCarriesLineConfidenceIntoCandidateFields()
    {
        var result = OcrCandidateParser.Parse(
            RankingCategory.Monday, "fixture.jpg",
            [new OcrTextLine("1\tCommander\tAlliance\t123", 0.72f, 0, 10)], 0.5);

        var candidate = Assert.Single(result);
        Assert.Equal(0.72, candidate.OverallConfidence, 2);
        Assert.False(candidate.IsSelected);
    }

    [Fact]
    public void ParseKeepsLowConfidenceRowsForManualReview()
    {
        var result = OcrCandidateParser.Parse(
            RankingCategory.Monday, "fixture.jpg",
            [new OcrTextLine("1\tCommander\tAlliance\t123", 0.20f, 0, 10)], 0.95);

        var candidate = Assert.Single(result);
        Assert.Equal(0.20, candidate.OverallConfidence, 2);
        Assert.False(candidate.IsSelected);
    }

    [Fact]
    public void ParseRowsRecognizesExplicitNoAlliance()
    {
        var result = OcrCandidateParser.ParseRows(RankingCategory.Monday, "fixture.jpg", [
            new OcrTextLine("1", 0.99f, 0, 1),
            new OcrTextLine("Commander", 0.99f, 2, 3),
            new OcrTextLine("無同盟", 0.99f, 4, 5),
            new OcrTextLine("123", 0.99f, 6, 7)
        ]);

        var candidate = Assert.Single(result);
        Assert.True(candidate.NoAllianceConfirmed);
        Assert.True(candidate.IsValid);
    }

    [Fact]
    public void ParseRowsSkipsTruncatedInlineScoreRow()
    {
        var result = OcrCandidateParser.ParseRows(RankingCategory.Monday, "fixture.jpg", [
            new OcrTextLine("1 123", 0.99f, 0, 1)
        ]);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseRowsAcceptsPunctuationInsertedInsideOcrScore()
    {
        var result = OcrCandidateParser.ParseRows(RankingCategory.Monday, "fixture.jpg", [
            new OcrTextLine("1", .9f, 10, 20, 20, 100),
            new OcrTextLine("CommanderAlpha", .9f, 30, 40, 250, 500),
            new OcrTextLine("[DEMO] 測試聯盟", .9f, 45, 55, 250, 600),
            new OcrTextLine("'1''2'','3''4''5'','6''7''8'", .9f, 30, 55, 650, 840)
        ]);

        var candidate = Assert.Single(result);
        Assert.Equal(12345678, candidate.Score);
    }

    [Fact]
    public void ParseRowsDoesNotTreatNumericCommanderNameAsScore()
    {
        var result = OcrCandidateParser.ParseRows(RankingCategory.Monday, "fixture.jpg", [
            new OcrTextLine("1", .9f, 10, 20, 20, 100),
            new OcrTextLine("Commander 123", .9f, 30, 40, 250, 500),
            new OcrTextLine("Alliance", .9f, 45, 55, 250, 600)
        ]);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseSyntheticMondayRow()
    {
        var result = OcrCandidateParser.ParsePlainText(RankingCategory.Monday, "fixture.jpg", [
            "1\tCommanderAlpha\t[DEMO] 測試聯盟\t12,345,678"
        ]);

        var candidate = Assert.Single(result);
        Assert.Equal(1, candidate.Rank);
        Assert.Equal("CommanderAlpha", candidate.CommanderName);
        Assert.Equal("[DEMO] 測試聯盟", candidate.AllianceName);
        Assert.Equal(12_345_678, candidate.Score);
    }

    [Theory]
    [InlineData("テスト隊長 A", "[DEMO] 試験同盟")]
    [InlineData("ผู้ทดสอบ A", "[DEMO] พันธมิตรทดสอบ")]
    [InlineData("테스트 지휘관 A", "[DEMO] 테스트 동맹")]
    public void ParseRowsPreservesSyntheticMultilingualText(string commander, string alliance)
    {
        var candidate = Assert.Single(OcrCandidateParser.ParseRows(RankingCategory.Monday, "fixture.jpg", [
            new OcrTextLine("1", .99f, 0, 10),
            new OcrTextLine($"  {commander}  ", .99f, 11, 20),
            new OcrTextLine($"  {alliance}  ", .99f, 21, 30),
            new OcrTextLine("12,345,678", .99f, 31, 40)
        ]));

        Assert.Equal(commander, candidate.CommanderName);
        Assert.Equal(alliance, candidate.AllianceName);
    }
}
