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
}
