using RankLens.App;

namespace RankLens.Tests;

public class ImageInputDiscoveryTests
{
    private sealed class RecordingProgress(List<int> values) : IProgress<int>
    {
        public void Report(int value) => values.Add(value);
    }

    private sealed class FakeRecognitionSource : IRecognitionSource
    {
        public Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(IReadOnlyList<string> imagePaths, CancellationToken cancellationToken = default)
        {
            if (imagePaths.Contains("bad.jpg"))
            {
                throw new InvalidDataException("圖片無法讀取");
            }

            return Task.FromResult<IReadOnlyList<RankingCandidate>>([CreateCandidate(imagePaths[0])]);
        }
    }

    private sealed class ShapeOnlyRecognitionSource : IRecognitionSource
    {
        public Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(
            IReadOnlyList<string> imagePaths,
            CancellationToken cancellationToken = default)
        {
            ScreenshotInputValidator.ValidatePortrait(imagePaths[0]);
            return Task.FromResult<IReadOnlyList<RankingCandidate>>([]);
        }
    }

    private sealed class TrackingRecognitionSource : IRecognitionSource
    {
        private int inFlight;
        public int MaximumInFlight { get; private set; }

        public async Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(
            IReadOnlyList<string> imagePaths,
            CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref inFlight);
            MaximumInFlight = Math.Max(MaximumInFlight, current);
            try
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                return [CreateCandidate(imagePaths[0])];
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        }
    }

    private static RankingCandidate CreateCandidate(string sourceImage) => new()
    {
        Category = RankingCategory.Monday,
        Rank = 1,
        CommanderName = "Commander",
        AllianceName = "Alliance",
        Score = 1,
        SourceImage = sourceImage
    };

    [Fact]
    public void FolderDiscoveryIsNonRecursiveAndFiltersSupportedExtensions()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ranklens-input", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "nested"));
        File.WriteAllText(Path.Combine(folder, "one.JPG"), string.Empty);
        File.WriteAllText(Path.Combine(folder, "two.png"), string.Empty);
        File.WriteAllText(Path.Combine(folder, "notes.txt"), string.Empty);
        File.WriteAllText(Path.Combine(folder, "nested", "three.jpg"), string.Empty);

        try
        {
            var result = ImageInputDiscovery.Discover([], folder);
            Assert.Equal(2, result.Accepted.Count);
            var rejected = Assert.Single(result.Rejected);
            Assert.Equal(Path.Combine(folder, "notes.txt"), rejected.Path);
            Assert.Contains("不支援", rejected.Error);
            Assert.Contains("notes.txt", ImageFailureText.Format(result.Rejected));
            Assert.DoesNotContain(result.Accepted, path => path.Contains("nested", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void MoreThanOneHundredImagesIsRejectedBeforeProcessing()
    {
        var files = Enumerable.Range(1, 101).Select(index => $"{index}.jpg");
        var error = Assert.Throws<InvalidOperationException>(() => ImageInputDiscovery.Discover(files, null));
        Assert.Contains("100", error.Message);
    }

    [Fact]
    public async Task BatchProcessingContinuesAfterOneFileErrorAndReportsProgress()
    {
        var progress = new List<int>();
        var result = await new BatchRecognitionProcessor().ProcessAsync(
            ["one.jpg", "bad.jpg", "three.png"],
            new FakeRecognitionSource(),
            new RecordingProgress(progress));

        Assert.Equal(3, result.Count);
        Assert.Null(result[0].Error);
        Assert.Contains("無法讀取", result[1].Error);
        Assert.Null(result[2].Error);
        Assert.Equal(100, progress[^1]);
    }

    [Fact]
    public async Task CancellationStopsBeforeWritingAnyResult()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new BatchRecognitionProcessor().ProcessAsync(
                ["one.jpg"], new FakeRecognitionSource(), cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task SameAspectRatioImageWithoutRankingRowsIsReportedAsUnsupported()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ranklens-{Guid.NewGuid():N}.png");
        try
        {
            var pixels = new byte[460 * 1000 * 4];
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
                460, 1000, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, 460 * 4);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);

            var result = Assert.Single(await new BatchRecognitionProcessor().ProcessAsync(
                [path], new ShapeOnlyRecognitionSource()));

            Assert.Null(result.Candidates);
            Assert.Contains("完整排名列", result.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OneHundredImageBatchKeepsOneImageInFlightAndReportsMonotonicProgress()
    {
        var progress = new List<int>();
        var paths = Enumerable.Range(1, 100).Select(index => $"{index}.jpg").ToArray();
        var source = new TrackingRecognitionSource();
        var results = await new BatchRecognitionProcessor().ProcessAsync(
            paths, source, new RecordingProgress(progress));

        Assert.Equal(100, results.Count);
        Assert.Equal(1, source.MaximumInFlight);
        Assert.Equal(100, progress[^1]);
        Assert.True(progress.Zip(progress.Skip(1), (before, after) => after >= before).All(value => value));
    }
}
