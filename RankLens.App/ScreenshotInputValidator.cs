using System.IO;
using System.Windows.Media.Imaging;

namespace RankLens.App;

public static class ScreenshotInputValidator
{
    public static void ValidatePortrait(string path, int minimumDimension = 240)
    {
        using var stream = File.OpenRead(path);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var aspectRatio = frame.PixelWidth / (double)frame.PixelHeight;
        if (frame.PixelWidth < minimumDimension || frame.PixelHeight < minimumDimension
            || aspectRatio is < 0.43 or > 0.49)
            throw new InvalidDataException("圖片必須是完整、未裁切或拼接的直向手機截圖，且需維持支援的等比例解析度。");
    }
}
