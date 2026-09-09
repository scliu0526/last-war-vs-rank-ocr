using Microsoft.ML.OnnxRuntime.Tensors;
using RankLens.App;

namespace RankLens.Tests;

public class OcrDetectionPostprocessorTests
{
    [Fact]
    public void ExtractFindsConnectedTextRegionAndMapsCoordinates()
    {
        var map = new DenseTensor<float>(new[] { 1, 1, 4, 4 });
        map[0, 0, 1, 1] = 0.9f;
        map[0, 0, 1, 2] = 0.8f;
        var boxes = OcrDetectionPostprocessor.Extract(map, 0.5f, 800, 400);
        var box = Assert.Single(boxes);
        Assert.Equal(200f, box.Left);
        Assert.Equal(100f, box.Top);
        Assert.Equal(600f, box.Right);
        Assert.Equal(200f, box.Bottom);
    }

    [Fact]
    public void ExtractDbExpandsMappedRegionWithinImageBounds()
    {
        var map = new DenseTensor<float>(new[] { 1, 1, 4, 4 });
        map[0, 0, 1, 1] = 0.9f;
        map[0, 0, 1, 2] = 0.8f;
        map[0, 0, 2, 1] = 0.85f;
        map[0, 0, 2, 2] = 0.75f;
        var boxes = OcrDetectionPostprocessor.ExtractDb(
            new OcrTensorOutput("det", [1, 1, 4, 4], [0, 0, 0, 0, 0, 0.9f, 0.8f, 0, 0.85f, 0.75f, 0, 0, 0, 0, 0, 0]),
            0.5f, 800, 400, 1, 4, 4);
        var box = Assert.Single(boxes);
        Assert.True(box.Left < 1f);
        Assert.True(box.Right > 3f);
        Assert.InRange(box.Top, 0, 400);
        Assert.InRange(box.Bottom, 0, 400);
    }
}
