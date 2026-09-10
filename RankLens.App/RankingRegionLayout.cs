namespace RankLens.App;

/// <summary>Normalized regions taken from the annotated ranking screenshot.</summary>
public static class RankingRegionLayout
{
    public static bool IsDataBox(DetectionBox box, int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0) return false;
        var centerX = ((box.Left + box.Right) / 2) / imageWidth;
        var centerY = ((box.Top + box.Bottom) / 2) / imageHeight;
        return centerY is >= 0.19f and <= 0.90f
            && centerX is >= 0.02f and <= 0.98f;
    }

    public static bool IsRankingColumnBox(DetectionBox box, int imageWidth, int imageHeight)
    {
        if (!IsDataBox(box, imageWidth, imageHeight)) return false;
        var centerX = (box.Left + box.Right) / 2;
        return IsRankColumn(centerX, imageWidth)
            || IsCommanderColumn(centerX, imageWidth)
            || IsScoreColumn(centerX, imageWidth);
    }

    public static bool IsRankColumn(float centerX, int imageWidth) =>
        centerX / imageWidth is >= 0.02f and <= 0.21f;

    public static bool IsCommanderColumn(float centerX, int imageWidth) =>
        centerX / imageWidth is >= 0.30f and <= 0.73f;

    public static bool IsScoreColumn(float centerX, int imageWidth) =>
        centerX / imageWidth is >= 0.73f and <= 0.98f;
}
