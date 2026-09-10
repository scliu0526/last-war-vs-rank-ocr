using RankLens.App;

namespace RankLens.Tests;

public sealed class OcrCharacterDictionaryTests
{
    [Theory]
    [InlineData("'7'", "7")]
    [InlineData("'['", "[")]
    [InlineData("']'", "]")]
    [InlineData("''''", "'")]
    [InlineData("骨", "骨")]
    [InlineData("$", "$")]
    public void NormalizeEntryRemovesYamlScalarQuotes(string input, string expected)
    {
        Assert.Equal(expected, OcrCharacterDictionary.NormalizeEntry(input));
    }
}
