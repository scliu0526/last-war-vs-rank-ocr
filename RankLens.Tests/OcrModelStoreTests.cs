using RankLens.App;

namespace RankLens.Tests;

public class OcrModelStoreTests
{
    [Fact]
    public void PendingLicenseAndHashAreRejectedBeforeModelLoad()
    {
        var manifest = new OcrModelManifest("det.onnx", "rec.onnx", "dict.txt", "must be verified", "PENDING_HASH", "PENDING_HASH");
        var exception = Assert.Throws<InvalidOperationException>(() => new OcrModelStore().Validate(manifest, Path.GetTempPath()));
        Assert.Contains("授權", exception.Message);
    }

    [Fact]
    public void PendingLicenseMarkerIsRejectedBeforeModelLoad()
    {
        var manifest = new OcrModelManifest("det.onnx", "rec.onnx", "dict.txt", "PENDING_MODEL_LICENSE", new string('a', 64), new string('b', 64));
        var exception = Assert.Throws<InvalidOperationException>(() => new OcrModelStore().Validate(manifest, Path.GetTempPath()));
        Assert.Contains("授權", exception.Message);
    }

    [Fact]
    public void ManifestCannotEscapeModelDirectory()
    {
        var manifest = new OcrModelManifest("..\\det.onnx", "rec.onnx", "dict.txt", "MIT", "abc", "def");
        Assert.Throws<InvalidDataException>(() => new OcrModelStore().Validate(manifest, Path.GetTempPath()));
    }

    [Fact]
    public void MalformedHashIsRejected()
    {
        var manifest = new OcrModelManifest("det.onnx", "rec.onnx", "dict.txt", "MIT", "1234", new string('a', 64));
        Assert.Throws<InvalidOperationException>(() => new OcrModelStore().Validate(manifest, Path.GetTempPath()));
    }
}
