namespace RankLens.App;

/// <summary>Normalized regions taken from the annotated ranking screenshot.</summary>
public static class RankingRegionLayout
{
    public static bool HasFullPageStructure(
        IReadOnlyList<DetectionBox> boxes,
        int imageWidth,
        int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0) return false;
        var normalized = boxes.Select(box => new
        {
            X = ((box.Left + box.Right) / 2) / imageWidth,
            Y = ((box.Top + box.Bottom) / 2) / imageHeight
        }).ToArray();
        var hasTitle = normalized.Any(point => point.X < 0.25f && point.Y is >= 0.03f and <= 0.09f);
        var hasRankingTabs = normalized.Count(point => point.Y is >= 0.09f and <= 0.15f) >= 2;
        var tableHeaders = normalized.Where(point => point.Y is >= 0.19f and <= 0.23f).ToArray();
        var hasTableHeaders = tableHeaders.Any(point => point.X < 0.21f)
            && tableHeaders.Any(point => point.X is >= 0.30f and <= 0.73f)
            && tableHeaders.Any(point => point.X > 0.73f);
        var data = normalized.Where(point => point.Y is > 0.23f and <= 0.90f).ToArray();
        var hasCompleteRowColumns = data.Any(point => point.X is >= 0.30f and <= 0.73f)
            && data.Any(point => point.X > 0.73f);
        var hasFooter = normalized.Any(point => point.Y is >= 0.91f and <= 0.98f);
        return hasTitle && hasRankingTabs && hasTableHeaders && hasCompleteRowColumns && hasFooter;
    }

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

    public static IReadOnlyList<IReadOnlyList<DetectionBox>> GroupRows(
        IEnumerable<DetectionBox> boxes, int imageHeight)
    {
        if (imageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(imageHeight));
        var maximumRowSpan = imageHeight * 0.045f;
        var rows = new List<IReadOnlyList<DetectionBox>>();
        List<DetectionBox>? current = null;
        float rowTop = 0;
        foreach (var box in boxes.OrderBy(box => box.Top).ThenBy(box => box.Left))
        {
            if (current is null || box.Top - rowTop > maximumRowSpan)
            {
                current = [];
                rows.Add(current);
                rowTop = box.Top;
            }
            current.Add(box);
        }
        return rows;
    }
}
