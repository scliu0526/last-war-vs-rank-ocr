using System.IO;
using System.Text.RegularExpressions;

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
        var boxes = OcrDetectionPostprocessor.ExtractDb(detection, detectionThreshold, image.OriginalWidth, image.OriginalHeight, image.Scale,
                image.Tensor.Dimensions[3], image.Tensor.Dimensions[2])
            .OrderBy(box => box.Top).ThenBy(box => box.Left)
            .ToArray();
        var dataBoxes = boxes.Where(box => RankingRegionLayout.IsDataBox(box, image.OriginalWidth, image.OriginalHeight)).ToArray();
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
            if (!string.IsNullOrWhiteSpace(decoded.Text)) lines.Add(new OcrTextLine(decoded.Text.Trim(), (float)confidence, (int)box.Top, (int)box.Bottom, (int)box.Left, (int)box.Right));
        }

        // The game uses a high-contrast outlined glyph for the rank column that
        // the detector can miss. Re-read only the left rank column for rows
        // that already have detected content; no rank is inferred if OCR fails.
        foreach (var row in dataBoxes.Where(box => box.Top > image.OriginalHeight * 0.2)
            .GroupBy(box => (int)Math.Round(box.Top / 70d)).Select(group => group.ToArray()))
        {
            var top = Math.Max(0, row.Min(box => box.Top) - 8);
            var bottom = Math.Min(image.OriginalHeight, row.Max(box => box.Bottom) + 8);
            // The purple ranking region contains a medal/icon to the left of the
            // numeral. Try several numeral bands so both one-digit and multi-digit
            // ranks can be read without allowing artwork into the crop.
            var rankReads = new List<(CtcDecoder.DecodedText Decoded, float Left, float Right)>();
            foreach (var (leftRatio, rightRatio) in new[] { (0.06f, 0.19f), (0.09f, 0.18f), (0.11f, 0.16f) })
            {
                var rankBox = new DetectionBox(image.OriginalWidth * leftRatio, top,
                    Math.Min(image.OriginalWidth * rightRatio, 180), bottom, 1);
                var crop = OcrImagePreprocessor.CropAndResize(image, rankBox);
                var outputs = await Task.Run(() => runtime.RunRecognition(crop), cancellationToken);
                var recognition = outputs.FirstOrDefault(IsSequenceTensor)
                    ?? throw new InvalidDataException("Recognition 模型沒有 [1,time,classes] 輸出。");
                var decoded = OcrRecognitionDecoder.DecodeWithConfidence(recognition, runtime.Dictionary);
                if (Regex.IsMatch(decoded.Text.Trim(), @"^\d{1,3}$"))
                    rankReads.Add((decoded, leftRatio, rightRatio));
            }

            var bestRank = rankReads.OrderByDescending(read => read.Decoded.Confidence).FirstOrDefault();
            if (bestRank.Decoded is not null)
                lines.Add(new OcrTextLine(bestRank.Decoded.Text.Trim(), (float)bestRank.Decoded.Confidence, (int)top, (int)bottom,
                    (int)(image.OriginalWidth * bestRank.Left), (int)(image.OriginalWidth * bestRank.Right)));
        }

        var category = OcrCandidateParser.DetectCategory(lines.Select(line => line.Text));
        var dataLines = lines.Where(line => RankingRegionLayout.IsDataBox(
            new DetectionBox(line.Left, line.Top, line.Right > line.Left ? line.Right : line.Left + 1, line.Bottom, line.Confidence),
            image.OriginalWidth, image.OriginalHeight)).ToArray();
        var candidates = OcrCandidateParser.ParseRows(category, path, dataLines, textConfidenceThreshold);
        if (candidates.Count == 0) candidates = OcrCandidateParser.Parse(category, path, dataLines, textConfidenceThreshold);
        foreach (var candidate in candidates) candidate.ClassificationResolved = category != RankingCategory.PendingClassification;
        return candidates;
    }

    private static bool IsProbabilityMap(OcrTensorOutput output) =>
        output.Dimensions is [1, 1, _, _];

    private static bool IsSequenceTensor(OcrTensorOutput output) =>
        output.Dimensions is [1, _, _];
}
