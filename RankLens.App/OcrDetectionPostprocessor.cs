using Microsoft.ML.OnnxRuntime.Tensors;
using System.IO;

namespace RankLens.App;

public sealed record DetectionBox(float Left, float Top, float Right, float Bottom, float Confidence);

public static class OcrDetectionPostprocessor
{
    public static IReadOnlyList<DetectionBox> Extract(OcrTensorOutput output, float threshold, int originalWidth, int originalHeight)
    {
        if (output.Dimensions.Length != 4 || output.Dimensions[0] != 1 || output.Dimensions[1] != 1)
            throw new InvalidDataException("Detection 模型輸出必須是 [1,1,height,width] 機率圖。");
        if (output.Dimensions[2] <= 0 || output.Dimensions[3] <= 0
            || output.Dimensions[2] > int.MaxValue / output.Dimensions[3]
            || output.Values.Length != output.Dimensions[2] * output.Dimensions[3])
            throw new InvalidDataException("Detection 模型輸出長度與 shape 不一致。");
        var tensor = new DenseTensor<float>(output.Values, output.Dimensions);
        return Extract(tensor, threshold, originalWidth, originalHeight);
    }

    public static IReadOnlyList<DetectionBox> Extract(DenseTensor<float> map, float threshold, int originalWidth, int originalHeight)
    {
        if (originalWidth <= 0 || originalHeight <= 0 || threshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(threshold), "圖片尺寸必須為正數，threshold 必須介於 0 與 1 之間。");
        var dimensions = map.Dimensions.ToArray();
        if (dimensions.Length != 4 || dimensions[0] != 1 || dimensions[1] != 1)
        {
            throw new InvalidDataException("Detection 模型輸出必須是 [1,1,height,width] 機率圖。");
        }

        var height = dimensions[2];
        var width = dimensions[3];
        var visited = new bool[height, width];
        var boxes = new List<DetectionBox>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (visited[y, x] || map[0, 0, y, x] < threshold) continue;
                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue((x, y)); visited[y, x] = true;
                var left = x; var right = x; var top = y; var bottom = y; var confidence = map[0, 0, y, x];
                while (queue.Count > 0)
                {
                    var point = queue.Dequeue();
                    left = Math.Min(left, point.X); right = Math.Max(right, point.X);
                    top = Math.Min(top, point.Y); bottom = Math.Max(bottom, point.Y);
                    confidence = Math.Max(confidence, map[0, 0, point.Y, point.X]);
                    foreach (var next in new[] { (point.X + 1, point.Y), (point.X - 1, point.Y), (point.X, point.Y + 1), (point.X, point.Y - 1) })
                    {
                        if (next.Item1 < 0 || next.Item1 >= width || next.Item2 < 0 || next.Item2 >= height || visited[next.Item2, next.Item1] || map[0, 0, next.Item2, next.Item1] < threshold) continue;
                        visited[next.Item2, next.Item1] = true; queue.Enqueue(next);
                    }
                }
                boxes.Add(new DetectionBox(
                    left * originalWidth / (float)width, top * originalHeight / (float)height,
                    (right + 1) * originalWidth / (float)width, (bottom + 1) * originalHeight / (float)height, confidence));
            }
        }
        return MergeTextBoxes(boxes);
    }

    private static IReadOnlyList<DetectionBox> MergeTextBoxes(IReadOnlyList<DetectionBox> boxes)
    {
        var merged = new List<DetectionBox>();
        foreach (var box in boxes.OrderBy(item => item.Top).ThenBy(item => item.Left))
        {
            var index = merged.FindIndex(existing =>
            {
                var overlap = Math.Min(existing.Bottom, box.Bottom) - Math.Max(existing.Top, box.Top);
                var height = Math.Min(existing.Bottom - existing.Top, box.Bottom - box.Top);
                var gap = box.Left - existing.Right;
                return height > 0 && overlap / height >= 0.45f && gap >= -2 && gap <= Math.Max(12, height * 2.5f);
            });
            if (index < 0)
            {
                merged.Add(box);
                continue;
            }

            var current = merged[index];
            merged[index] = new DetectionBox(
                Math.Min(current.Left, box.Left), Math.Min(current.Top, box.Top),
                Math.Max(current.Right, box.Right), Math.Max(current.Bottom, box.Bottom),
                Math.Max(current.Confidence, box.Confidence));
        }
        return merged;
    }

    public static IReadOnlyList<DetectionBox> Extract(
        OcrTensorOutput output, float threshold, int originalWidth, int originalHeight, float scale)
    {
        if (output.Dimensions.Length != 4 || output.Dimensions[0] != 1 || output.Dimensions[1] != 1)
            throw new InvalidDataException("Detection 模型輸出必須是 [1,1,height,width] 機率圖。 ");
        return Extract(new DenseTensor<float>(output.Values, output.Dimensions), threshold, originalWidth, originalHeight, scale);
    }

    public static IReadOnlyList<DetectionBox> Extract(
        DenseTensor<float> map, float threshold, int originalWidth, int originalHeight, float scale)
    {
        if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        var padded = Extract(map, threshold, map.Dimensions[3], map.Dimensions[2]);
        return padded.Select(box => new DetectionBox(
            Math.Clamp(box.Left / scale, 0, originalWidth),
            Math.Clamp(box.Top / scale, 0, originalHeight),
            Math.Clamp(box.Right / scale, 0, originalWidth),
            Math.Clamp(box.Bottom / scale, 0, originalHeight),
            box.Confidence)).Where(box => box.Right > box.Left && box.Bottom > box.Top).ToArray();
    }
}
