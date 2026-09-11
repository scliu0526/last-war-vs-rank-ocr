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

    [Fact]
    public void LoadReadsOnlyYamlCharacterDictionaryAndAddsCtcBlank()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ranklens-dict-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(path, "PostProcess:\n  name: CTCLabelDecode\n  character_dict:\n  - '!'\n  - ''''\n  - 김\nOther:\n  value: ignored\n");

            var dictionary = OcrCharacterDictionary.Load(path);

            Assert.Equal(["", "!", "'", "김", " "], dictionary);
        }
        finally { File.Delete(path); }
    }
}
