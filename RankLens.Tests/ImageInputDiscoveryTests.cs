using RankLens.App;

namespace RankLens.Tests;

public class ImageInputDiscoveryTests
{
    private sealed class FakeRecognitionSource : IRecognitionSource
    {
        public Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(IReadOnlyList<string> imagePaths, CancellationToken cancellationToken = default)
        {
            if (imagePaths.Contains("bad.jpg"))
            {
                throw new InvalidDataException("圖片無法讀取");
            }

            return Task.FromResult<IReadOnlyList<RankingCandidate>>([]);
        }
    }

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
            Assert.Single(result.Rejected);
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
            new Progress<int>(progress.Add));

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
}
