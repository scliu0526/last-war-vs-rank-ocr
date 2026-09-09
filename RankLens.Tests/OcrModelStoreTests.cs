using RankLens.App;

namespace RankLens.Tests;

public class OcrModelStoreTests
{
    private static OcrModelManifest Manifest(string license, string detectionHash, string recognitionHash) =>
        new("det.onnx", "rec.onnx", "dict.txt", license, detectionHash, recognitionHash, new string('c', 64), "v1", "rev1", "https://example.test/det", "https://example.test/rec", "https://example.test/dict");

    [Fact]
    public void PendingLicenseAndHashAreRejectedBeforeModelLoad()
    {
        var manifest = Manifest("must be verified", "PENDING_HASH", "PENDING_HASH");
        var exception = Assert.Throws<InvalidOperationException>(() => new OcrModelStore().Validate(manifest, Path.GetTempPath()));
        Assert.Contains("授權", exception.Message);
    }

    [Fact]
    public void PendingLicenseMarkerIsRejectedBeforeModelLoad()
    {
        var manifest = Manifest("PENDING_MODEL_LICENSE", new string('a', 64), new string('b', 64));
        var exception = Assert.Throws<InvalidOperationException>(() => new OcrModelStore().Validate(manifest, Path.GetTempPath()));
        Assert.Contains("授權", exception.Message);
    }

    [Fact]
    public void ManifestCannotEscapeModelDirectory()
    {
        var manifest = Manifest("MIT", "abc", "def") with { DetectionModel = "..\\det.onnx" };
        Assert.Throws<InvalidDataException>(() => new OcrModelStore().Validate(manifest, Path.GetTempPath()));
    }

    [Fact]
    public void MalformedHashIsRejected()
    {
        var manifest = Manifest("MIT", "1234", new string('a', 64));
        Assert.Throws<InvalidOperationException>(() => new OcrModelStore().Validate(manifest, Path.GetTempPath()));
    }

    [Fact]
    public void LoadManifestReadsReleaseCamelCaseJson()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ranklens-manifest-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"detectionModel\":\"det.onnx\",\"recognitionModel\":\"rec.onnx\",\"characterDictionary\":\"dict.txt\",\"license\":\"MIT\",\"detectionSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"recognitionSha256\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}");
            var manifest = new OcrModelStore().LoadManifest(path);
            Assert.Equal("det.onnx", manifest.DetectionModel);
            Assert.Equal("rec.onnx", manifest.RecognitionModel);
            Assert.Equal("MIT", manifest.License);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
