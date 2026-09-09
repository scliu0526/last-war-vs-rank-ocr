using System.IO;

namespace RankLens.App;

/// <summary>Runs the local PP-OCR detection and recognition sessions and converts text boxes to ranking candidates.</summary>
public sealed class OnnxOcrRecognitionSource(
    IOcrInferenceRuntime runtime,
    float detectionThreshold = 0.30f,
    double textConfidenceThreshold = 0.50) : IRecognitionSource
{
    public async Task<IReadOnlyList<RankingCandidate>> RecognizeAsync(
        IReadOnlyList<string> imagePaths,
        CancellationToken cancellationToken = default)
    {
        if (imagePaths.Count != 1) throw new ArgumentException("一次只能辨識一張圖片。", nameof(imagePaths));
        cancellationToken.ThrowIfCancellationRequested();
        var path = imagePaths[0];
        ScreenshotInputValidator.ValidatePortrait(path);
        var image = await OcrImagePreprocessor.LoadAsync(path, cancellationToken: cancellationToken);
        var detectionOutputs = await Task.Run(() => runtime.RunDetection(image.Tensor), cancellationToken);
        var detection = detectionOutputs.FirstOrDefault(IsProbabilityMap)
            ?? throw new InvalidDataException("Detection 模型沒有 [1,1,height,width] 輸出。");
        var boxes = OcrDetectionPostprocessor.Extract(detection, detectionThreshold, image.OriginalWidth, image.OriginalHeight, image.Scale)
            .OrderBy(box => box.Top).ThenBy(box => box.Left)
            .ToArray();
        var lines = new List<OcrTextLine>(boxes.Length);
        foreach (var box in boxes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var crop = OcrImagePreprocessor.CropAndResize(image, box);
            var outputs = await Task.Run(() => runtime.RunRecognition(crop), cancellationToken);
            var recognition = outputs.FirstOrDefault(IsSequenceTensor)
                ?? throw new InvalidDataException("Recognition 模型沒有 [1,time,classes] 輸出。");
            var decoded = OcrRecognitionDecoder.DecodeWithConfidence(recognition, runtime.Dictionary);
            var confidence = Math.Min(box.Confidence, decoded.Confidence);
            if (!string.IsNullOrWhiteSpace(decoded.Text)) lines.Add(new OcrTextLine(decoded.Text.Trim(), (float)confidence, (int)box.Top, (int)box.Bottom, (int)box.Left));
        }

        var category = OcrCandidateParser.DetectCategory(lines.Select(line => line.Text));
        var candidates = OcrCandidateParser.ParseRows(category, path, lines, textConfidenceThreshold);
        if (candidates.Count == 0) candidates = OcrCandidateParser.Parse(category, path, lines, textConfidenceThreshold);
        foreach (var candidate in candidates) candidate.ClassificationResolved = category != RankingCategory.PendingClassification;
        return candidates;
    }

    private static bool IsProbabilityMap(OcrTensorOutput output) =>
        output.Dimensions is [1, 1, _, _];

    private static bool IsSequenceTensor(OcrTensorOutput output) =>
        output.Dimensions is [1, _, _];
}
