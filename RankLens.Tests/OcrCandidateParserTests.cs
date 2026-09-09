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
            new OcrTextLine("GBgogogo", 0.99f, 11, 20),
            new OcrTextLine("[TFIP]965熟成魚中心", 0.99f, 21, 30),
            new OcrTextLine("72,138,569", 0.99f, 31, 40)
        };

        var result = OcrCandidateParser.ParseRows(RankingCategory.Monday, "fixture.jpg", lines);

        var candidate = Assert.Single(result);
        Assert.Equal(1, candidate.Rank);
        Assert.Equal("GBgogogo", candidate.CommanderName);
        Assert.Equal("[TFIP]965熟成魚中心", candidate.AllianceName);
        Assert.Equal(72138569, candidate.Score);
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
            [new OcrTextLine("2 Commander 72，138，569", 0.99f, 0, 1)], 0.95);
        Assert.Equal(72138569, Assert.Single(normalized).Score);
    }
}
