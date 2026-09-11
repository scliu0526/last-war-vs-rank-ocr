using RankLens.App;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RankLens.Tests;

public class ScreenshotInputValidatorTests
{
    [Theory]
    [InlineData(320, 240)]
    [InlineData(869, 1500)]
    [InlineData(869, 2500)]
    public void RejectsRotatedCroppedAndStitchedAspectRatios(int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ranklens-{Guid.NewGuid():N}.png");
        try
        {
            var pixels = new byte[width * height * 4];
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
            Assert.Throws<InvalidDataException>(() => ScreenshotInputValidator.ValidatePortrait(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AcceptsProportionallyScaledFullScreenshotShape()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ranklens-{Guid.NewGuid():N}.png");
        try
        {
            var pixels = new byte[460 * 1000 * 4];
            var bitmap = BitmapSource.Create(460, 1000, 96, 96, PixelFormats.Bgra32, null, pixels, 460 * 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);

            ScreenshotInputValidator.ValidatePortrait(path);
        }
        finally { File.Delete(path); }
    }
}
