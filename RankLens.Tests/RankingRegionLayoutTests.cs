using RankLens.App;

namespace RankLens.Tests;

public class RankingRegionLayoutTests
{
    private static readonly DetectionBox[] FullPageAnchors =
    [
        new(20, 90, 130, 120, .9f),
        new(60, 215, 300, 250, .9f),
        new(330, 215, 560, 250, .9f),
        new(40, 375, 160, 405, .9f),
        new(300, 375, 600, 405, .9f),
        new(680, 375, 840, 405, .9f),
        new(60, 480, 160, 520, .9f),
        new(300, 480, 600, 520, .9f),
        new(680, 480, 840, 520, .9f),
        new(60, 630, 160, 670, .9f),
        new(300, 630, 600, 670, .9f),
        new(680, 630, 840, 670, .9f),
        new(60, 940, 160, 980, .9f),
        new(300, 940, 600, 980, .9f),
        new(680, 940, 840, 980, .9f),
        new(60, 1230, 160, 1270, .9f),
        new(300, 1230, 600, 1270, .9f),
        new(680, 1230, 840, 1270, .9f),
        new(650, 1745, 830, 1785, .9f)
    ];

    [Fact]
    public void FullPageStructureRequiresAllAnnotatedAnchorBands()
    {
        Assert.True(RankingRegionLayout.HasFullPageStructure(FullPageAnchors, 869, 1880));
    }

    [Fact]
    public void FullPageWithOneCompleteCandidateInAValidCardSlotIsAccepted()
    {
        var anchors = FullPageAnchors.Where(box => box.Bottom <= 405 || box.Top >= 1745);
        var oneCandidate = FullPageAnchors.Where(box => box.Top >= 480 && box.Bottom <= 520);

        Assert.True(RankingRegionLayout.HasFullPageStructure(
            anchors.Concat(oneCandidate).ToArray(), 869, 1880));
    }

    [Fact]
    public void SameRatioCropWithRankingRowsButNoTitleOrTabsIsRejected()
    {
        var croppedLayout = FullPageAnchors.Skip(3).ToArray();

        Assert.False(RankingRegionLayout.HasFullPageStructure(croppedLayout, 869, 1880));
    }

    [Fact]
    public void SameRatioStitchWithFullAnchorsAndTooManyRankingRowsIsRejected()
    {
        var extraRows = Enumerable.Range(1, 8).SelectMany(index =>
        {
            var top = 480 + index * 100;
            return new[]
            {
                new DetectionBox(60, top, 160, top + 40, .9f),
                new DetectionBox(300, top, 600, top + 40, .9f),
                new DetectionBox(680, top, 840, top + 40, .9f)
            };
        });
        var stitchedLayout = FullPageAnchors
            .Concat(extraRows)
            .ToArray();

        Assert.False(RankingRegionLayout.HasFullPageStructure(stitchedLayout, 869, 1880));
    }

    [Fact]
    public void SameRatioStitchWithFullAnchorsAndFourViewportSpanningRowsIsRejected()
    {
        var anchors = FullPageAnchors.Where(box => box.Bottom <= 405 || box.Top >= 1745);
        var stitchedRows = new[] { 0.26f, 0.45f, 0.53f, 0.66f }.SelectMany(center =>
        {
            var middle = center * 1880;
            return new[]
            {
                new DetectionBox(60, middle - 20, 160, middle + 20, .9f),
                new DetectionBox(300, middle - 20, 600, middle + 20, .9f),
                new DetectionBox(680, middle - 20, 840, middle + 20, .9f)
            };
        });

        Assert.False(RankingRegionLayout.HasFullPageStructure(
            anchors.Concat(stitchedRows).ToArray(), 869, 1880));
    }

    [Fact]
    public void ColumnsFromDifferentVerticalGroupsDoNotFormACompleteRankingRow()
    {
        var incoherent = FullPageAnchors
            .Where(box => !RankingRegionLayout.IsCommanderColumn((box.Left + box.Right) / 2, 869)
                || (box.Top + box.Bottom) / 2f < 430)
            .Append(new DetectionBox(300, 820, 600, 860, .9f))
            .ToArray();

        Assert.False(RankingRegionLayout.HasFullPageStructure(incoherent, 869, 1880));
    }

    [Fact]
    public void DataRegionIncludesRowsButExcludesTabsAndFooter()
    {
        Assert.True(RankingRegionLayout.IsDataBox(new DetectionBox(40, 450, 180, 520, .9f), 869, 1880));
        Assert.False(RankingRegionLayout.IsDataBox(new DetectionBox(40, 200, 180, 260, .9f), 869, 1880));
        Assert.False(RankingRegionLayout.IsDataBox(new DetectionBox(40, 1750, 180, 1810, .9f), 869, 1880));
    }

    [Fact]
    public void ColumnsMatchAnnotatedScreenshotRegions()
    {
        Assert.True(RankingRegionLayout.IsRankColumn(100, 869));
        Assert.True(RankingRegionLayout.IsCommanderColumn(400, 869));
        Assert.True(RankingRegionLayout.IsScoreColumn(760, 869));
        Assert.False(RankingRegionLayout.IsScoreColumn(400, 869));
    }

    [Fact]
    public void DataBoxesOutsideAnnotatedColumnsAreIgnored()
    {
        Assert.True(RankingRegionLayout.IsRankingColumnBox(new DetectionBox(40, 450, 150, 520, .9f), 869, 1880));
        Assert.True(RankingRegionLayout.IsRankingColumnBox(new DetectionBox(290, 450, 600, 520, .9f), 869, 1880));
        Assert.True(RankingRegionLayout.IsRankingColumnBox(new DetectionBox(650, 450, 830, 520, .9f), 869, 1880));
        Assert.False(RankingRegionLayout.IsRankingColumnBox(new DetectionBox(190, 450, 270, 520, .9f), 869, 1880));
        Assert.False(RankingRegionLayout.IsRankingColumnBox(new DetectionBox(40, 250, 150, 320, .9f), 869, 1880));
    }

    [Fact]
    public void GroupRowsKeepsTextLinesFromSameRankingCardTogether()
    {
        var boxes = new[]
        {
            new DetectionBox(265, 460, 451, 489, .9f),
            new DetectionBox(637, 482, 840, 509, .9f),
            new DetectionBox(226, 508, 661, 532, .9f),
            new DetectionBox(265, 611, 441, 640, .9f),
            new DetectionBox(642, 635, 837, 662, .9f),
            new DetectionBox(252, 661, 505, 687, .9f)
        };

        var rows = RankingRegionLayout.GroupRows(boxes, 1880);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(3, row.Count));
    }
}
