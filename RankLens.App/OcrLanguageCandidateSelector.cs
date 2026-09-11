namespace RankLens.App;

public sealed record OcrLanguageCandidate(
    OcrRecognitionLanguage Language,
    CtcDecoder.DecodedText Recognition);

public static class OcrLanguageCandidateSelector
{
    public static CtcDecoder.DecodedText Select(
        CtcDecoder.DecodedText primary,
        IEnumerable<OcrLanguageCandidate> candidates)
    {
        var selected = primary;
        var selectedScore = primary.Confidence;
        foreach (var candidate in candidates)
        {
            var targetCount = CountTargetScript(candidate.Recognition.Text, candidate.Language);
            if (targetCount == 0) continue;
            var letterCount = Math.Max(1, candidate.Recognition.Text.Count(char.IsLetter));
            var scriptEvidence = targetCount / (double)letterCount * 0.08
                + Math.Min(targetCount, 4) * 0.01;
            var score = candidate.Recognition.Confidence + scriptEvidence;
            if (score > selectedScore)
            {
                selected = candidate.Recognition;
                selectedScore = score;
            }
        }
        return selected;
    }

    public static bool ContainsTargetScript(string text, OcrRecognitionLanguage language) =>
        CountTargetScript(text, language) > 0;

    private static int CountTargetScript(string text, OcrRecognitionLanguage language) => language switch
    {
        OcrRecognitionLanguage.Korean => text.Count(character => character is >= '\u1100' and <= '\u11FF'
            or >= '\u3130' and <= '\u318F' or >= '\uAC00' and <= '\uD7AF'),
        OcrRecognitionLanguage.Thai => text.Count(character => character is >= '\u0E00' and <= '\u0E7F'),
        _ => 0
    };
}
