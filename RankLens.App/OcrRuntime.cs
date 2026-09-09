using Microsoft.ML.OnnxRuntime;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace RankLens.App;

public sealed record OcrModelManifest(
    string DetectionModel,
    string RecognitionModel,
    string CharacterDictionary,
    string License,
    string DetectionSha256,
    string RecognitionSha256);

public sealed record OcrRuntimeConfiguration(
    RecognitionExecutionMode Mode,
    int AdapterId,
    string ModelDirectory);

public sealed class OcrRuntimeFactory
{
    public InferenceSession Create(OcrRuntimeConfiguration configuration, OcrModelManifest manifest)
    {
        new OcrModelStore().Validate(manifest, configuration.ModelDirectory);
        var modelPath = Path.Combine(configuration.ModelDirectory, manifest.DetectionModel);
        var recognitionPath = Path.Combine(configuration.ModelDirectory, manifest.RecognitionModel);
        var dictionaryPath = Path.Combine(configuration.ModelDirectory, manifest.CharacterDictionary);
        if (!File.Exists(modelPath) || !File.Exists(recognitionPath) || !File.Exists(dictionaryPath))
        {
            throw new FileNotFoundException("找不到完整 OCR 模型組，請先完成模型安裝。", modelPath);
        }

        var options = new SessionOptions();
        if (configuration.Mode == RecognitionExecutionMode.DirectML)
        {
            options.AppendExecutionProvider_DML(configuration.AdapterId);
        }

        return new InferenceSession(modelPath, options);
    }
}

/// <summary>Uses the Windows offline OCR engine when the matching language packs are installed.</summary>
public sealed class WindowsOcrRecognitionSource : IRecognitionSource
{
    public static IReadOnlyList<string> AvailableLanguages() =>
        OcrEngine.AvailableRecognizerLanguages.Select(language => language.LanguageTag).OrderBy(tag => tag).ToArray();

    public static IReadOnlyDictionary<string, bool> RequiredLanguageAvailability()
    {
        var available = AvailableLanguages();
        return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["繁體中文"] = available.Any(tag => tag.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase)),
            ["英文"] = available.Any(tag => tag.StartsWith("en", StringComparison.OrdinalIgnoreCase)),
            ["韓文"] = available.Any(tag => tag.StartsWith("ko", StringComparison.OrdinalIgnoreCase)),
            ["泰文"] = available.Any(tag => tag.StartsWith("th", StringComparison.OrdinalIgnoreCase)),
            ["日文"] = available.Any(tag => tag.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        };
    }

    public async Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(
        IReadOnlyList<string> imagePaths,
        CancellationToken cancellationToken = default)
    {
        if (imagePaths.Count != 1) throw new ArgumentException("一次只能辨識一張圖片。", nameof(imagePaths));
        cancellationToken.ThrowIfCancellationRequested();
        var file = await StorageFile.GetFileFromPathAsync(imagePaths[0]);
        using var stream = await RandomAccessStreamReference.CreateFromFile(file).OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync();
        var engines = OcrEngine.AvailableRecognizerLanguages
            .Where(language => language.LanguageTag.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase)
                || language.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                || language.LanguageTag.StartsWith("ko", StringComparison.OrdinalIgnoreCase)
                || language.LanguageTag.StartsWith("th", StringComparison.OrdinalIgnoreCase)
                || language.LanguageTag.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            .Select(language => OcrEngine.TryCreateFromLanguage(language))
            .Where(engine => engine is not null)
            .Cast<OcrEngine>()
            .ToArray();
        if (engines.Length == 0) throw new InvalidOperationException("Windows 尚未安裝可用的 OCR 語言套件。");
        var results = await Task.WhenAll(engines.Select(async engine => await engine.RecognizeAsync(bitmap)));
        cancellationToken.ThrowIfCancellationRequested();
        var lines = results.SelectMany(result => result.Lines)
            .Select(line =>
            {
                var boxes = line.Words.Select(word => word.BoundingRect).ToArray();
                var top = boxes.Length == 0 ? 0 : (int)boxes.Min(box => box.Y);
                var bottom = boxes.Length == 0 ? 0 : (int)boxes.Max(box => box.Y + box.Height);
                return new OcrTextLine(line.Text, 1, top, bottom);
            })
            .DistinctBy(line => line.Text, StringComparer.Ordinal)
            .ToArray();
        var category = OcrCandidateParser.DetectCategory(lines.Select(line => line.Text));
        var candidates = OcrCandidateParser.ParseRows(category, imagePaths[0], lines, 0);
        if (candidates.Count == 0)
        {
            candidates = OcrCandidateParser.Parse(category, imagePaths[0], lines, 0);
        }
        foreach (var candidate in candidates) candidate.ClassificationResolved = category != RankingCategory.PendingClassification;
        return candidates;
    }
}
