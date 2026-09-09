using Microsoft.ML.OnnxRuntime.Tensors;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RankLens.App;

public sealed record OcrImageTensor(DenseTensor<float> Tensor, int OriginalWidth, int OriginalHeight, float Scale);

public static class OcrImagePreprocessor
{
    public static async Task<OcrImageTensor> LoadAsync(string path, int targetSize = 960, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var loaded = await Task.Run(() => LoadBitmap(path, targetSize), cancellationToken);
        var bitmap = loaded.Bitmap;
        var stride = bitmap.PixelWidth * 4;
        var source = new byte[bitmap.PixelHeight * stride];
        bitmap.CopyPixels(source, stride, 0);
        var tensor = new DenseTensor<float>(new[] { 1, 3, bitmap.PixelHeight, bitmap.PixelWidth });
        for (var y = 0; y < bitmap.PixelHeight; y++)
        {
            for (var x = 0; x < bitmap.PixelWidth; x++)
            {
                var offset = y * stride + x * 4;
                tensor[0, 0, y, x] = source[offset + 2] / 255f;
                tensor[0, 1, y, x] = source[offset + 1] / 255f;
                tensor[0, 2, y, x] = source[offset] / 255f;
            }
        }
        return new OcrImageTensor(tensor, loaded.OriginalWidth, loaded.OriginalHeight, loaded.Scale);
    }

    private static (BitmapSource Bitmap, int OriginalWidth, int OriginalHeight, float Scale) LoadBitmap(string path, int targetSize)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var scale = Math.Min(1d, Math.Min((double)targetSize / frame.PixelWidth, (double)targetSize / frame.PixelHeight));
        var scaled = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
        var converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return (converted, frame.PixelWidth, frame.PixelHeight, (float)scale);
    }
}
