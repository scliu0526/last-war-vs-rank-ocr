using RankLens.App;

namespace RankLens.Tests;

public class RankingRegionLayoutTests
{
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
}
