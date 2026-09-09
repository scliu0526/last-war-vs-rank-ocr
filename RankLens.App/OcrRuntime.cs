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
        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? throw new InvalidOperationException("Windows 尚未安裝可用的 OCR 語言套件。");
        var result = await engine.RecognizeAsync(bitmap);
        cancellationToken.ThrowIfCancellationRequested();
        var lines = result.Lines.Select(line => new OcrTextLine(line.Text, 1, 0, 0)).ToArray();
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
