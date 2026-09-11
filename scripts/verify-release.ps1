param(
    [Parameter(Mandatory = $true)][string]$Archive,
    [string]$ChecksumFile
)

$ErrorActionPreference = "Stop"
$archivePath = (Resolve-Path -LiteralPath $Archive).Path
if ([string]::IsNullOrWhiteSpace($ChecksumFile)) { $ChecksumFile = "$archivePath.sha256" }
$checksumPath = (Resolve-Path -LiteralPath $ChecksumFile).Path
$expected = (Get-Content -LiteralPath $checksumPath -Raw).Trim().Split()[0]
if ($expected -notmatch '^[0-9A-Fa-f]{64}$') { throw "Checksum file must contain a canonical SHA-256 value." }
$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash
if (-not [string]::Equals($actual, $expected, [StringComparison]::OrdinalIgnoreCase)) { throw "Release archive SHA-256 mismatch." }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $required = @("LICENSE", "THIRD-PARTY-NOTICES.md", "README.md", "README.zh-TW.md", "licenses/paddleocr-models/LICENSE.txt", "licenses/paddleocr-models/NOTICE.md", "models/manifest.json", "models/PP-OCRv5_det.onnx", "models/PP-OCRv5_rec.onnx", "models/ppocrv5_dict.txt", "models/korean_PP-OCRv5_rec.onnx", "models/korean_PP-OCRv5_rec.yml", "models/th_PP-OCRv5_rec.onnx", "models/th_PP-OCRv5_rec.yml")
    $entryNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $zip.Entries) { [void]$entryNames.Add($entry.FullName.Replace('\', '/')) }
    $forbiddenEntries = @($entryNames | Where-Object {
        $_ -match '(^|/)screenshot(/|$)' -or $_ -match '\.(jpg|jpeg|png|gif|bmp|webp|tif|tiff|ico)$'
    })
    if ($forbiddenEntries.Count -gt 0) {
        throw "Release archive contains private screenshot/image content: $($forbiddenEntries[0])"
    }
    foreach ($name in $required) { if (-not $entryNames.Contains($name)) { throw "Release archive is missing $name" } }
    $allowedModelEntries = @("models/manifest.json", "models/README.md", "models/PP-OCRv5_det.onnx", "models/PP-OCRv5_rec.onnx", "models/ppocrv5_dict.txt", "models/korean_PP-OCRv5_rec.onnx", "models/korean_PP-OCRv5_rec.yml", "models/th_PP-OCRv5_rec.onnx", "models/th_PP-OCRv5_rec.yml")
    $unexpectedModelEntries = @($entryNames | Where-Object { $_.StartsWith("models/", [StringComparison]::OrdinalIgnoreCase) -and $_ -notin $allowedModelEntries })
    if ($unexpectedModelEntries.Count -gt 0) { throw "Release archive contains an unexpected model/staging entry: $($unexpectedModelEntries[0])" }
    foreach ($package in @(
        @{ Id = "documentformat.openxml"; Version = "3.3.0" },
        @{ Id = "documentformat.openxml.framework"; Version = "3.3.0" },
        @{ Id = "microsoft.ai.directml"; Version = "1.15.4" },
        @{ Id = "microsoft.ml.onnxruntime"; Version = "1.24.1" },
        @{ Id = "microsoft.ml.onnxruntime.managed"; Version = "1.24.1" },
        @{ Id = "microsoft.ml.onnxruntime.directml"; Version = "1.24.1" },
        @{ Id = "system.numerics.tensors"; Version = "9.0.0" },
        @{ Id = "vortice.dxgi"; Version = "3.8.3" },
        @{ Id = "vortice.directx"; Version = "3.8.3" },
        @{ Id = "vortice.mathematics"; Version = "2.1.0" },
        @{ Id = "sharpgen.runtime"; Version = "2.4.2-beta" },
        @{ Id = "sharpgen.runtime.com"; Version = "2.4.2-beta" }
    )) {
        $prefix = "licenses/$($package.Id)-$($package.Version)/"
        if (@($entryNames | Where-Object { $_.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and $_ -notmatch '/$' }).Count -eq 0) {
            throw "Release archive is missing dependency notices for $($package.Id) $($package.Version)."
        }
    }
    $manifestEntry = $zip.GetEntry("models/manifest.json")
    $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ([string]::IsNullOrWhiteSpace($manifest.license) -or $manifest.license -match "PENDING|must be verified|placeholder") { throw "Release manifest contains an unresolved license notice." }
    foreach ($metadata in @($manifest.modelVersion, $manifest.sourceRevision, $manifest.detectionSourceUrl, $manifest.recognitionSourceUrl, $manifest.dictionarySourceUrl)) {
        if ([string]::IsNullOrWhiteSpace($metadata) -or $metadata -match "PENDING|placeholder") { throw "Release manifest is missing verified model provenance metadata." }
    }
    foreach ($sourceUrl in @($manifest.detectionSourceUrl, $manifest.recognitionSourceUrl, $manifest.dictionarySourceUrl)) {
        $parsedUrl = $null
        if (-not [Uri]::TryCreate($sourceUrl, [UriKind]::Absolute, [ref]$parsedUrl) -or $parsedUrl.Scheme -ne "https") { throw "Release manifest model provenance URLs must use HTTPS absolute URLs." }
    }
    foreach ($hash in @($manifest.detectionSha256, $manifest.recognitionSha256, $manifest.characterDictionarySha256)) {
        if ($hash -notmatch '^[0-9A-Fa-f]{64}$') { throw "Release manifest contains a non-canonical model SHA-256 value." }
    }
    $modelEntries = @(
        @{ Name = $manifest.detectionModel; Hash = $manifest.detectionSha256 },
        @{ Name = $manifest.recognitionModel; Hash = $manifest.recognitionSha256 },
        @{ Name = $manifest.characterDictionary; Hash = $manifest.characterDictionarySha256 }
    )
    foreach ($variant in @($manifest.recognitionVariants)) {
        if ($variant.language -notin @("korean", "thai") -or [string]::IsNullOrWhiteSpace($variant.sourceRevision)) { throw "Release manifest recognition variant provenance is invalid." }
        foreach ($sourceUrl in @($variant.recognitionSourceUrl, $variant.dictionarySourceUrl)) {
            $parsedUrl = $null
            if (-not [Uri]::TryCreate($sourceUrl, [UriKind]::Absolute, [ref]$parsedUrl) -or $parsedUrl.Scheme -ne "https") { throw "Release manifest recognition variant URLs must use HTTPS absolute URLs." }
        }
        $modelEntries += @{ Name = $variant.recognitionModel; Hash = $variant.recognitionSha256 }
        $modelEntries += @{ Name = $variant.characterDictionary; Hash = $variant.characterDictionarySha256 }
    }
    foreach ($model in $modelEntries) {
        $entry = $zip.GetEntry("models/$($model.Name)")
        if ($null -eq $entry) { throw "Release archive is missing manifest model $($model.Name)" }
        $sha = [System.Security.Cryptography.SHA256]::Create()
        $stream = $entry.Open()
        try { $actualModelHash = ([System.BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '') }
        finally { $stream.Dispose(); $sha.Dispose() }
        if (-not [string]::Equals($actualModelHash, $model.Hash, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Model SHA-256 mismatch in release archive: $($model.Name)"
        }
    }
}
finally { $zip.Dispose() }

Write-Host "Release archive checksum, model entries, and manifest validation passed."
