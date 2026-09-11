using Microsoft.ML.OnnxRuntime.Tensors;
using RankLens.App;
using Xunit.Sdk;

namespace RankLens.Tests;

public sealed class LocalLanguageModelCapabilityTests
{
    [Fact]
    public void InstalledModelsRunOnSyntheticTensorAndExposeJapaneseKoreanAndThaiCharacters()
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
    }

    private static bool IsSequenceTensor(OcrTensorOutput output) =>
        output.Dimensions.Length == 3
        && output.Dimensions[0] == 1;
}
