using Microsoft.ML.OnnxRuntime;
using System.IO;

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
        var modelPath = Path.Combine(configuration.ModelDirectory, manifest.DetectionModel);
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("找不到 OCR 偵測模型，請先完成模型安裝。", modelPath);
        }

        var options = new SessionOptions();
        if (configuration.Mode == RecognitionExecutionMode.DirectML)
        {
            options.AppendExecutionProvider_DML(configuration.AdapterId);
        }

        return new InferenceSession(modelPath, options);
    }
}
