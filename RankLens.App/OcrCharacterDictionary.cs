using System.IO;

namespace RankLens.App;

public static class OcrCharacterDictionary
{
    public static IReadOnlyList<string> Load(string path)
    {
        var lines = File.ReadAllLines(path);
        if (!Path.GetExtension(path).Equals(".yml", StringComparison.OrdinalIgnoreCase)
            && !Path.GetExtension(path).Equals(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            return lines.Select(NormalizeEntry).ToArray();
        }

        var marker = Array.FindIndex(lines, line => line.Trim().Equals("character_dict:", StringComparison.Ordinal));
        if (marker < 0) throw new InvalidDataException("OCR YAML 找不到 character_dict。");
        var entries = lines.Skip(marker + 1)
            .TakeWhile(line => line.StartsWith("  - ", StringComparison.Ordinal))
            .Select(line => NormalizeEntry(line[4..]))
            .ToArray();
        if (entries.Length == 0) throw new InvalidDataException("OCR YAML 的 character_dict 是空的。");
        return new[] { "" }.Concat(entries).Append(" ").ToArray();
    }

    public static string NormalizeEntry(string entry)
    {
        if (entry.Length >= 2 && entry[0] == '\'' && entry[^1] == '\'')
            return entry[1..^1].Replace("''", "'", StringComparison.Ordinal);
        return entry;
    }
}
