using System.IO;

namespace RankLens.App;

public sealed record ImageBatchResult(IReadOnlyList<string> Accepted, IReadOnlyList<string> Rejected);
public sealed record ImageProcessingResult(string Path, IReadOnlyList<RankingCandidate>? Candidates, string? Error);

public static class ImageInputDiscovery
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    public static ImageBatchResult Discover(
        IEnumerable<string> selectedFiles,
        string? selectedFolder,
        int maximum = 100)
    {
        var candidates = selectedFiles
            .Concat(selectedFolder is null ? [] : Directory.EnumerateFiles(selectedFolder, "*", SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var accepted = candidates.Where(path => Extensions.Contains(Path.GetExtension(path))).ToList();
        if (accepted.Count > maximum)
        {
            throw new InvalidOperationException($"每批最多處理 {maximum} 張圖片。");
        }

        var rejected = candidates.Where(path => !Extensions.Contains(Path.GetExtension(path))).ToList();
        return new ImageBatchResult(accepted, rejected);
    }
}

public sealed class BatchRecognitionProcessor
{
    public async Task<IReadOnlyList<ImageProcessingResult>> ProcessAsync(
        IReadOnlyList<string> imagePaths,
        IRecognitionSource recognitionSource,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ImageProcessingResult>();
        for (var index = 0; index < imagePaths.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = imagePaths[index];
            try
            {
                var candidates = await recognitionSource.RecognizeAsync([path], cancellationToken);
                results.Add(new ImageProcessingResult(path, candidates, null));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                results.Add(new ImageProcessingResult(path, null, exception.Message));
            }

            progress?.Report((index + 1) * 100 / imagePaths.Count);
        }

        return results;
    }
}
