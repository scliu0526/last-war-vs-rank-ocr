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
        new(300, 480, 600, 520, .9f),
        new(680, 480, 840, 520, .9f),
        new(650, 1745, 830, 1785, .9f)
    ];

    [Fact]
    public void FullPageStructureRequiresAllAnnotatedAnchorBands()
    {
        Assert.True(RankingRegionLayout.HasFullPageStructure(FullPageAnchors, 869, 1880));
    }

    [Fact]
    public void SameRatioCropWithRankingRowsButNoTitleOrTabsIsRejected()
    {
        var croppedLayout = FullPageAnchors.Skip(3).ToArray();

        Assert.False(RankingRegionLayout.HasFullPageStructure(croppedLayout, 869, 1880));
    }

    [Fact]
    public void SameRatioStitchWithRepeatedRankingRowsButNoFooterIsRejected()
    {
        var stitchedLayout = FullPageAnchors[..^1]
            .Concat(FullPageAnchors.Where(box => (box.Top + box.Bottom) / 2f is > 430 and < 900))
            .ToArray();

        Assert.False(RankingRegionLayout.HasFullPageStructure(stitchedLayout, 869, 1880));
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
