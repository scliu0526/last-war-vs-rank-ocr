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
            var pixels = new byte[16 * 16 * 4];
            for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = 255; pixels[i + 1] = 255; pixels[i + 2] = 255; pixels[i + 3] = 255; }
            var bitmap = BitmapSource.Create(16, 16, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4);
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
        for (var index = 0; index < symbols.Count; index++) values[index * dictionary.Count + symbols[index]] = 1;
        return new OcrTensorOutput("rec", [1, symbols.Count, dictionary.Count], values);
    }

    private sealed class FakeRuntime(IReadOnlyList<string> dictionary, OcrTensorOutput detection, OcrTensorOutput recognition) : IOcrInferenceRuntime
    {
        public IReadOnlyList<string> Dictionary { get; } = dictionary;
        public IReadOnlyList<OcrTensorOutput> RunDetection(DenseTensor<float> imageTensor) => [detection];
        public IReadOnlyList<OcrTensorOutput> RunRecognition(DenseTensor<float> imageTensor) => [recognition];
        public void Dispose() { }
    }
}
