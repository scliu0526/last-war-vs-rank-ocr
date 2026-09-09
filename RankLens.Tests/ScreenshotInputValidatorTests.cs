using RankLens.App;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RankLens.Tests;

public class ScreenshotInputValidatorTests
{
    [Fact]
    public void RejectsLandscapeImages()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ranklens-{Guid.NewGuid():N}.png");
        try
        {
            var pixels = new byte[320 * 240 * 4];
            var bitmap = BitmapSource.Create(320, 240, 96, 96, PixelFormats.Bgra32, null, pixels, 320 * 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
            Assert.Throws<InvalidDataException>(() => ScreenshotInputValidator.ValidatePortrait(path));
        }
        finally { File.Delete(path); }
    }
}
