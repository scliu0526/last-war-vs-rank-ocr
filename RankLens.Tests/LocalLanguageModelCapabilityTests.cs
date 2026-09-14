using Microsoft.ML.OnnxRuntime.Tensors;
using RankLens.App;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit.Sdk;

namespace RankLens.Tests;

public sealed class LocalLanguageModelCapabilityTests
{
    [Fact]
    public async Task InstalledModelsRecognizeGeneratedJapaneseAndThaiRasterText()
    {
        var modelDirectory = Path.Combine(AppContext.BaseDirectory, "models");
        var manifestPath = Path.Combine(modelDirectory, "manifest.json");
        if (!File.Exists(manifestPath)
            || !File.Exists(Path.Combine(modelDirectory, "PP-OCRv5_det.onnx")))
        {
            throw SkipException.ForSkip("Local OCR model binaries are intentionally absent from Git and CI.");
        }

        var manifest = new OcrModelStore().LoadManifest(manifestPath);
        using var runtime = new OcrRuntimeFactory().Create(
            new OcrRuntimeConfiguration(RecognitionExecutionMode.Cpu, 0, modelDirectory), manifest);
        var synthetic = new DenseTensor<float>(new[] { 1, 3, 48, 320 });

        Assert.Contains(runtime.RunRecognition(synthetic), IsSequenceTensor);
        Assert.Contains(runtime.Dictionary,
            entry => entry.Any(character => character is >= '\u3040' and <= '\u30FF'));

        var korean = Assert.Single(runtime.RecognitionVariants,
            variant => variant.Language == OcrRecognitionLanguage.Korean);
        Assert.Contains(korean.RunRecognition(synthetic), IsSequenceTensor);
        Assert.Contains(korean.Dictionary,
            entry => OcrLanguageCandidateSelector.ContainsTargetScript(entry, OcrRecognitionLanguage.Korean));

        var thai = Assert.Single(runtime.RecognitionVariants,
            variant => variant.Language == OcrRecognitionLanguage.Thai);
        Assert.Contains(thai.RunRecognition(synthetic), IsSequenceTensor);
        Assert.Contains(thai.Dictionary,
            entry => OcrLanguageCandidateSelector.ContainsTargetScript(entry, OcrRecognitionLanguage.Thai));

        Assert.Equal("日本", await RecognizeRenderedTextAsync(
            "日本", "Yu Gothic UI", "ja-JP", runtime.RunRecognition, runtime.Dictionary));
        Assert.Equal("กขค", await RecognizeRenderedTextAsync(
            "กขค", "Leelawadee UI", "th-TH", thai.RunRecognition, thai.Dictionary));
    }

    [Fact]
    public async Task HundredImageBatchDoesNotRetainProductionPreprocessingOrOnnxBuffers()
    {
        var modelDirectory = Path.Combine(AppContext.BaseDirectory, "models");
        var manifestPath = Path.Combine(modelDirectory, "manifest.json");
        if (!File.Exists(manifestPath)
            || !File.Exists(Path.Combine(modelDirectory, "PP-OCRv5_det.onnx")))
        {
            throw SkipException.ForSkip("Local OCR model binaries are intentionally absent from Git and CI installs them before this release gate.");
        }

        var folder = Path.Combine(Path.GetTempPath(), "ranklens-model-batch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var first = Path.Combine(folder, "001.png");
            var pixels = new byte[460 * 1000 * 4];
            Array.Fill<byte>(pixels, 255);
            var bitmap = BitmapSource.Create(460, 1000, 96, 96, PixelFormats.Bgra32, null, pixels, 460 * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(first)) encoder.Save(stream);
            var paths = new List<string> { first };
            for (var index = 2; index <= 100; index++)
            {
                var path = Path.Combine(folder, $"{index:000}.png");
                File.Copy(first, path);
                paths.Add(path);
            }

            var manifest = new OcrModelStore().LoadManifest(manifestPath);
            using var runtime = new OcrRuntimeFactory().Create(
                new OcrRuntimeConfiguration(RecognitionExecutionMode.Cpu, 0, modelDirectory), manifest);
            var recognitionRuntime = Assert.Single(runtime.RecognitionVariants,
                variant => variant.Language == OcrRecognitionLanguage.Korean);
            var source = new ModelBackedBatchRecognitionSource(recognitionRuntime);
            await new BatchRecognitionProcessor().ProcessAsync(paths.Take(1).ToArray(), source);
            var retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            var privateBytesBefore = process.PrivateMemorySize64;
            var results = await new BatchRecognitionProcessor().ProcessAsync(
                paths, source);
            var retainedAfter = GC.GetTotalMemory(forceFullCollection: true);
            process.Refresh();
            var privateBytesAfter = process.PrivateMemorySize64;

            Assert.Equal(100, results.Count);
            Assert.All(results, result => Assert.Null(result.Error));
            Assert.True(retainedAfter - retainedBefore < 32L * 1024 * 1024,
                $"The 100-image batch retained {retainedAfter - retainedBefore:N0} bytes after a full collection.");
            Assert.True(privateBytesAfter - privateBytesBefore < 128L * 1024 * 1024,
                $"The 100-image batch increased process private memory by {privateBytesAfter - privateBytesBefore:N0} bytes after warm-up.");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static async Task<string> RecognizeRenderedTextAsync(
        string text,
        string fontFamily,
        string culture,
        Func<DenseTensor<float>, IReadOnlyList<OcrTensorOutput>> recognize,
        IReadOnlyList<string> dictionary)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ranklens-language-{Guid.NewGuid():N}.png");
        try
        {
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, 320, 64));
                drawing.DrawText(new FormattedText(
                    text,
                    CultureInfo.GetCultureInfo(culture),
                    FlowDirection.LeftToRight,
                    new Typeface(fontFamily),
                    40,
                    Brushes.Black,
                    1), new Point(8, 7));
            }
            var bitmap = new RenderTargetBitmap(320, 64, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);

            var image = await OcrImagePreprocessor.LoadAsync(path);
            var tensor = OcrImagePreprocessor.CropAndResize(
                image, new DetectionBox(0, 0, image.OriginalWidth, image.OriginalHeight, 1));
            var output = recognize(tensor).Single(IsSequenceTensor);
            return OcrRecognitionDecoder.Decode(output, dictionary).Trim();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool IsSequenceTensor(OcrTensorOutput output) =>
        output.Dimensions.Length == 3
        && output.Dimensions[0] == 1;

    private sealed class ModelBackedBatchRecognitionSource(IOcrRecognitionRuntime runtime) : IRecognitionSource
    {
        public async Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(
            IReadOnlyList<string> imagePaths,
            CancellationToken cancellationToken = default)
        {
            var image = await OcrImagePreprocessor.LoadAsync(imagePaths[0], cancellationToken: cancellationToken);
            var tensor = OcrImagePreprocessor.CropAndResize(
                image, new DetectionBox(0, 0, image.OriginalWidth, image.OriginalHeight, 1));
            if (!runtime.RunRecognition(tensor).Any(IsSequenceTensor))
                throw new InvalidDataException("Recognition model did not return a sequence tensor.");
            return [new RankingCandidate
            {
                Category = RankingCategory.Monday,
                Rank = 1,
                CommanderName = "Synthetic",
                AllianceName = "[TEST]",
                Score = 1,
                SourceImage = imagePaths[0]
            }];
        }
    }
}
