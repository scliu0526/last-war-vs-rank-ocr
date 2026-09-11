using Microsoft.ML.OnnxRuntime.Tensors;
using RankLens.App;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RankLens.Tests;

public class OnnxOcrRecognitionSourceTests
{
    [Fact]
    public async Task RunsDetectionCropRecognitionAndParsesCandidate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ranklens-{Guid.NewGuid():N}.png");
        try
        {
            var pixels = new byte[320 * 480 * 4];
            for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = 255; pixels[i + 1] = 255; pixels[i + 2] = 255; pixels[i + 3] = 255; }
            var bitmap = BitmapSource.Create(320, 480, 96, 96, PixelFormats.Bgra32, null, pixels, 320 * 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);

            var recognitionText = "1\tCommander\tAlliance\t123";
            var dictionary = new[] { "" }.Concat(recognitionText.Distinct().Select(character => character.ToString())).ToArray();
            var runtime = new FakeRuntime(dictionary,
                new OcrTensorOutput("det", [1, 1, 2, 2], [0.9f, 0, 0, 0]),
                MakeRecognition(dictionary, recognitionText));
            Assert.Equal(recognitionText, OcrRecognitionDecoder.Decode(MakeRecognition(dictionary, recognitionText), dictionary));
            Assert.Single(OcrCandidateParser.Parse(RankingCategory.PendingClassification, path, [new OcrTextLine(recognitionText, .9f, 0, 8)], .5));
            var candidates = await new OnnxOcrRecognitionSource(runtime, 0.5f, 0.5).RecognizeAsync([path]);

            var candidate = Assert.Single(candidates);
            Assert.Equal(1, candidate.Rank);
            Assert.Equal("Commander", candidate.CommanderName);
            Assert.Equal("Alliance", candidate.AllianceName);
            Assert.Equal(123, candidate.Score);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task RetriesRankColumnWhenDetectionBoxStartsAtLeftEdge()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ranklens-{Guid.NewGuid():N}.png");
        try
        {
            var pixels = new byte[320 * 480 * 4];
            for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = 255; pixels[i + 1] = 255; pixels[i + 2] = 255; pixels[i + 3] = 255; }
            var bitmap = BitmapSource.Create(320, 480, 96, 96, PixelFormats.Bgra32, null, pixels, 320 * 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);

            var content = "GBgogogo\tAlliance\t123";
            var rank = "1";
            var dictionary = new[] { "" }.Concat((content + rank).Distinct().Select(character => character.ToString())).ToArray();
            var runtime = new SequenceRuntime(dictionary,
                new OcrTensorOutput("det", [1, 1, 2, 2], [0, 0, 0.9f, 0]),
                [MakeRecognition(dictionary, content), MakeRecognition(dictionary, rank)]);

            var candidates = await new OnnxOcrRecognitionSource(runtime, 0.5f, 0.5).RecognizeAsync([path]);

            var candidate = Assert.Single(candidates);
            Assert.Equal(1, candidate.Rank);
            Assert.Equal("GBgogogo", candidate.CommanderName);
            Assert.Equal("Alliance", candidate.AllianceName);
            Assert.Equal(123, candidate.Score);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("김강민아빠", "korean", true)]
    [InlineData("ภาษาไทย", "thai", true)]
    [InlineData("GBgogogo", "korean", false)]
    [InlineData("[rock] BAND", "thai", false)]
    public void DetectsOnlyTheRequestedLanguageScript(string text, string language, bool expected)
    {
        Assert.Equal(expected, OnnxOcrRecognitionSource.ContainsTargetScript(text, language));
    }

    [Theory]
    [InlineData("김강민아빠", "korean", true)]
    [InlineData("김스풍스R", "korean", true)]
    [InlineData("그린핀 pin", "korean", true)]
    [InlineData("ีGBgogogo", "thai", false)]
    [InlineData("ปายณpin", "thai", false)]
    [InlineData("ก-ฮร", "thai", false)]
    [InlineData("ภาษาไทย", "thai", true)]
    public void RequiresAStrongTargetScriptMajority(string text, string language, bool expected)
    {
        Assert.Equal(expected, OnnxOcrRecognitionSource.HasStrongTargetScript(text, language));
    }

    private static OcrTensorOutput MakeRecognition(IReadOnlyList<string> dictionary, string text)
    {
        var symbols = new List<int>();
        var previous = -1;
        for (var index = 0; index < text.Length; index++)
        {
            var symbol = text[index].ToString();
            var character = Enumerable.Range(0, dictionary.Count).First(dictionaryIndex => dictionary[dictionaryIndex] == symbol);
            if (character == previous) symbols.Add(0);
            symbols.Add(character);
            previous = character;
        }
        var values = new float[symbols.Count * dictionary.Count];
        for (var index = 0; index < symbols.Count; index++) values[index * dictionary.Count + symbols[index]] = 10;
        return new OcrTensorOutput("rec", [1, symbols.Count, dictionary.Count], values);
    }

    private sealed class FakeRuntime(IReadOnlyList<string> dictionary, OcrTensorOutput detection, OcrTensorOutput recognition) : IOcrInferenceRuntime
    {
        public IReadOnlyList<string> Dictionary { get; } = dictionary;
        public IReadOnlyList<OcrTensorOutput> RunDetection(DenseTensor<float> imageTensor) => [detection];
        public IReadOnlyList<OcrTensorOutput> RunRecognition(DenseTensor<float> imageTensor) => [recognition];
        public void Dispose() { }
    }

    private sealed class SequenceRuntime(
        IReadOnlyList<string> dictionary,
        OcrTensorOutput detection,
        IReadOnlyList<OcrTensorOutput> recognitions) : IOcrInferenceRuntime
    {
        private int recognitionIndex;
        public IReadOnlyList<string> Dictionary { get; } = dictionary;
        public IReadOnlyList<OcrTensorOutput> RunDetection(DenseTensor<float> imageTensor) => [detection];
        public IReadOnlyList<OcrTensorOutput> RunRecognition(DenseTensor<float> imageTensor) =>
            [recognitions[Math.Min(recognitionIndex++, recognitions.Count - 1)]];
        public void Dispose() { }
    }
}
