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
}
