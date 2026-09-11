using RankLens.App;

namespace RankLens.Tests;

public sealed class OcrLanguageCandidateSelectorTests
{
    [Fact]
    public void SelectsConfidentMixedKoreanCandidateWithoutRatioGate()
    {
        var primary = new CtcDecoder.DecodedText("pin", 0.80);
        var mixed = new CtcDecoder.DecodedText("테스트 pin", 0.78);

        var selected = OcrLanguageCandidateSelector.Select(primary,
            [new OcrLanguageCandidate(OcrRecognitionLanguage.Korean, mixed)]);

        Assert.Equal(mixed, selected);
    }

    [Fact]
    public void KeepsPrimaryWhenTargetScriptCandidateHasWeakEvidence()
    {
        var primary = new CtcDecoder.DecodedText("CommanderAlpha", 0.95);
        var hallucination = new CtcDecoder.DecodedText("ีCommanderAlpha", 0.70);

        var selected = OcrLanguageCandidateSelector.Select(primary,
            [new OcrLanguageCandidate(OcrRecognitionLanguage.Thai, hallucination)]);

        Assert.Equal(primary, selected);
    }

    [Fact]
    public void PreservesLeadingAndTrailingPunctuation()
    {
        var primary = new CtcDecoder.DecodedText("name", 0.50);
        var quoted = new CtcDecoder.DecodedText("'김'", 0.95);

        var selected = OcrLanguageCandidateSelector.Select(primary,
            [new OcrLanguageCandidate(OcrRecognitionLanguage.Korean, quoted)]);

        Assert.Equal("'김'", selected.Text);
    }

    [Fact]
    public void ExpandedCropCannotWinOnlyBecauseOfOneHallucinatedThaiCharacter()
    {
        var original = new CtcDecoder.DecodedText("CommanderAlpha", 0.96);
        var expanded = new CtcDecoder.DecodedText("ีCommanderAlpha", 0.60);

        Assert.Equal(original, OcrLanguageCandidateSelector.SelectCrop(original, expanded));
    }
}
