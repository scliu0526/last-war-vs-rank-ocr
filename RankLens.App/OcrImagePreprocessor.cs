using Microsoft.ML.OnnxRuntime.Tensors;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RankLens.App;

public sealed record OcrImageTensor(DenseTensor<float> Tensor, int OriginalWidth, int OriginalHeight, float Scale);

public static class OcrImagePreprocessor
{
    // PaddleOCR inference preprocessing uses (pixel / 255 - 0.5) / 0.5
    // for all RGB channels; ImageNet normalization would materially change
    // the distribution expected by the PP-OCRv5 models.
    private static readonly float[] Mean = [0.5f, 0.5f, 0.5f];
    private static readonly float[] Std = [0.5f, 0.5f, 0.5f];

    public static async Task<OcrImageTensor> LoadAsync(string path, int targetSize = 960, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var loaded = await Task.Run(() => LoadBitmap(path, targetSize), cancellationToken);
        var bitmap = loaded.Bitmap;
        var stride = bitmap.PixelWidth * 4;
        var source = new byte[bitmap.PixelHeight * stride];
        bitmap.CopyPixels(source, stride, 0);
        var tensorWidth = AlignTo32(bitmap.PixelWidth);
        var tensorHeight = AlignTo32(bitmap.PixelHeight);
        var tensor = new DenseTensor<float>(new[] { 1, 3, tensorHeight, tensorWidth });
        for (var y = 0; y < bitmap.PixelHeight; y++)
        {
            for (var x = 0; x < bitmap.PixelWidth; x++)
            {
                var offset = y * stride + x * 4;
                tensor[0, 0, y, x] = Normalize(source[offset + 2], 0);
                tensor[0, 1, y, x] = Normalize(source[offset + 1], 1);
                tensor[0, 2, y, x] = Normalize(source[offset], 2);
            }
        }
        return new OcrImageTensor(tensor, loaded.OriginalWidth, loaded.OriginalHeight, loaded.Scale);
    }

    public static DenseTensor<float> CropAndResize(OcrImageTensor image, DetectionBox box, int targetWidth = 320, int targetHeight = 48)
    {
        var left = Math.Clamp((int)Math.Floor(box.Left * image.Scale), 0, image.Tensor.Dimensions[3] - 1);
        var top = Math.Clamp((int)Math.Floor(box.Top * image.Scale), 0, image.Tensor.Dimensions[2] - 1);
        var right = Math.Clamp((int)Math.Ceiling(box.Right * image.Scale), left + 1, image.Tensor.Dimensions[3]);
        var bottom = Math.Clamp((int)Math.Ceiling(box.Bottom * image.Scale), top + 1, image.Tensor.Dimensions[2]);
        var result = new DenseTensor<float>(new[] { 1, 3, targetHeight, targetWidth });
        var resizedWidth = Math.Clamp((int)Math.Round((right - left) * (double)targetHeight / (bottom - top)), 1, targetWidth);
        for (var y = 0; y < targetHeight; y++)
        for (var x = 0; x < resizedWidth; x++)
        {
            var sourceX = left + Math.Min(right - left - 1, x * (right - left) / resizedWidth);
            var sourceY = top + Math.Min(bottom - top - 1, y * (bottom - top) / targetHeight);
            for (var channel = 0; channel < 3; channel++) result[0, channel, y, x] = image.Tensor[0, channel, sourceY, sourceX];
        }
        return result;
    }

    private static float Normalize(byte value, int channel) => (value / 255f - Mean[channel]) / Std[channel];
    private static int AlignTo32(int value) => Math.Max(32, (value + 31) / 32 * 32);

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
