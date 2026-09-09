using System.Security.Cryptography;
using System.Text.Json;
using System.IO;

namespace RankLens.App;

public sealed class OcrModelStore
{
    public OcrModelManifest LoadManifest(string manifestPath)
    {
        using var stream = File.OpenRead(manifestPath);
        return JsonSerializer.Deserialize<OcrModelManifest>(stream)
            ?? throw new InvalidDataException("OCR 模型 manifest 無法解析。");
    }

    public void Validate(OcrModelManifest manifest, string modelDirectory)
    {
        if (string.IsNullOrWhiteSpace(manifest.License)
            || manifest.License.Contains("PENDING", StringComparison.OrdinalIgnoreCase)
            || manifest.License.Contains("must be verified", StringComparison.OrdinalIgnoreCase)
            || manifest.License.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("OCR 模型授權聲明尚未完成核對，拒絕載入。");
        }
        ValidateFile(manifest.DetectionModel, manifest.DetectionSha256, modelDirectory);
        ValidateFile(manifest.RecognitionModel, manifest.RecognitionSha256, modelDirectory);
        ValidateFile(manifest.CharacterDictionary, manifest.CharacterDictionarySha256, modelDirectory);
    }

    private static void ValidateFile(string fileName, string expectedHash, string directory)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || Path.GetFileName(fileName) != fileName
            || fileName.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException("OCR manifest 檔名無效。");
        }
        if (string.IsNullOrWhiteSpace(expectedHash)
            || expectedHash.StartsWith("PENDING_", StringComparison.OrdinalIgnoreCase)
            || expectedHash.Length != 64
            || expectedHash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException("OCR 模型尚未設定固定 SHA-256，拒絕載入未鎖定的模型。");
        }

        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path)) throw new FileNotFoundException("找不到 OCR 模型檔。", path);
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"OCR 模型雜湊不符：{fileName}。");
        }
    }
}
