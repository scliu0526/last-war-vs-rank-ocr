using System.IO;

namespace RankLens.App;

public static class OcrCharacterDictionary
{
    public static IReadOnlyList<string> Load(string path) =>
        File.ReadAllLines(path).Select(NormalizeEntry).ToArray();

    public static string NormalizeEntry(string entry)
    {
        if (entry.Length >= 2 && entry[0] == '\'' && entry[^1] == '\'')
            return entry[1..^1].Replace("''", "'", StringComparison.Ordinal);
        return entry;
    }
}
