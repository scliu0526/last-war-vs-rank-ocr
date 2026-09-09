using RankLens.App;

namespace RankLens.Tests;

public class CtcDecoderTests
{
    [Fact]
    public void GreedyDecodeRemovesBlankAndRepeatedTokens()
    {
        var timesteps = new IReadOnlyList<float>[]
        {
            new[] { 0f, 5f, 0f }, new[] { 0f, 4f, 0f },
            new[] { 9f, 0f, 0f }, new[] { 0f, 0f, 6f },
            new[] { 0f, 0f, 7f }
        };
        Assert.Equal("AB", CtcDecoder.Decode(timesteps, ["_", "A", "B"]));
    }
}
