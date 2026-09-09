using System.IO;
using System.Windows.Media.Imaging;

namespace RankLens.App;

public static class ScreenshotInputValidator
{
    public static void ValidatePortrait(string path, int minimumDimension = 240)
    {
        using var stream = File.OpenRead(path);
        var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (frame.PixelWidth < minimumDimension || frame.PixelHeight < minimumDimension || frame.PixelHeight <= frame.PixelWidth)
            throw new InvalidDataException("圖片必須是完整直向手機截圖，且解析度不足以辨識排名列。");
    }
}
