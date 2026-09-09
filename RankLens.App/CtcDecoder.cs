namespace RankLens.App;

public static class CtcDecoder
{
    public sealed record DecodedText(string Text, double Confidence);

    public static DecodedText DecodeWithConfidence(IReadOnlyList<IReadOnlyList<float>> timesteps, IReadOnlyList<string> dictionary, int blankIndex = 0)
    {
        var text = new System.Text.StringBuilder();
        var previous = -1;
        var confidenceTotal = 0d;
        var confidenceCount = 0;
        foreach (var timestep in timesteps)
        {
            if (timestep.Count == 0) continue;
            var index = 0;
            for (var candidate = 1; candidate < timestep.Count; candidate++)
                if (timestep[candidate] > timestep[index]) index = candidate;
            if (index != blankIndex && index != previous && index < dictionary.Count)
            {
                text.Append(dictionary[index]);
                var sum = timestep.Sum();
                if (sum > 0 && sum <= 1.01 && timestep.All(value => value >= 0 && value <= 1))
                {
                    confidenceTotal += timestep[index];
                }
                else
                {
                    var maximum = timestep.Max();
                    var denominator = timestep.Sum(value => Math.Exp(value - maximum));
                    confidenceTotal += denominator == 0 ? 0 : Math.Exp(timestep[index] - maximum) / denominator;
                }
                confidenceCount++;
            }
            previous = index;
        }
        return new DecodedText(text.ToString(), confidenceCount == 0 ? 0 : confidenceTotal / confidenceCount);
    }

    public static string Decode(IReadOnlyList<IReadOnlyList<float>> timesteps, IReadOnlyList<string> dictionary, int blankIndex = 0)
    {
        var output = new System.Text.StringBuilder();
        var previous = -1;
        foreach (var timestep in timesteps)
        {
            if (timestep.Count == 0) continue;
            var index = 0;
            for (var candidate = 1; candidate < timestep.Count; candidate++)
            {
                if (timestep[candidate] > timestep[index]) index = candidate;
            }

            if (index != blankIndex && index != previous && index >= 0 && index < dictionary.Count)
            {
                output.Append(dictionary[index]);
            }
            previous = index;
        }
        return output.ToString();
    }
}
