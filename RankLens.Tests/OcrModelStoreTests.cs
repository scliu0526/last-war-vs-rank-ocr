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
}
