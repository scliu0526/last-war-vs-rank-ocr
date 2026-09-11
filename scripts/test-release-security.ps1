$ErrorActionPreference = "Stop"

$root = Join-Path ([IO.Path]::GetTempPath()) "ranklens-release-security-$([Guid]::NewGuid().ToString('N'))"
$models = Join-Path $root "models"
$licenses = Join-Path $root "licenses"
$archive = Join-Path $root "test.zip"
$checksum = "$archive.sha256"

try {
    New-Item -ItemType Directory -Path $models, $licenses -Force | Out-Null
    $modelNotice = Join-Path $licenses "paddleocr-models"
    New-Item -ItemType Directory -Path $modelNotice -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $modelNotice "NOTICE.md") -Value "synthetic model attribution"
    Set-Content -LiteralPath (Join-Path $modelNotice "LICENSE.txt") -Value "synthetic Apache-2.0 license"
    foreach ($document in @("LICENSE", "THIRD-PARTY-NOTICES.md", "README.md", "README.zh-TW.md")) {
        Set-Content -LiteralPath (Join-Path $root $document) -Value "synthetic release test"
    }

    $modelData = @{
        "PP-OCRv5_det.onnx" = [byte[]](1, 2, 3)
        "PP-OCRv5_rec.onnx" = [byte[]](4, 5, 6)
        "ppocrv5_dict.txt" = [Text.Encoding]::UTF8.GetBytes("a`nb`n")
        "korean_PP-OCRv5_rec.onnx" = [byte[]](7, 8, 9)
        "korean_PP-OCRv5_rec.yml" = [Text.Encoding]::UTF8.GetBytes("character_dict:`n  - 김`n")
        "th_PP-OCRv5_rec.onnx" = [byte[]](10, 11, 12)
        "th_PP-OCRv5_rec.yml" = [Text.Encoding]::UTF8.GetBytes("character_dict:`n  - ก`n")
    }
    foreach ($item in $modelData.GetEnumerator()) {
        [IO.File]::WriteAllBytes((Join-Path $models $item.Key), $item.Value)
    }
    $manifest = @{
        license = "MIT"
        modelVersion = "synthetic-test"
        sourceRevision = "synthetic-test"
        detectionSourceUrl = "https://example.invalid/detection"
        recognitionSourceUrl = "https://example.invalid/recognition"
        dictionarySourceUrl = "https://example.invalid/dictionary"
        detectionModel = "PP-OCRv5_det.onnx"
        recognitionModel = "PP-OCRv5_rec.onnx"
        characterDictionary = "ppocrv5_dict.txt"
        detectionSha256 = (Get-FileHash -Algorithm SHA256 (Join-Path $models "PP-OCRv5_det.onnx")).Hash
        recognitionSha256 = (Get-FileHash -Algorithm SHA256 (Join-Path $models "PP-OCRv5_rec.onnx")).Hash
        characterDictionarySha256 = (Get-FileHash -Algorithm SHA256 (Join-Path $models "ppocrv5_dict.txt")).Hash
        recognitionVariants = @(
            @{
                language = "korean"
                recognitionModel = "korean_PP-OCRv5_rec.onnx"
                characterDictionary = "korean_PP-OCRv5_rec.yml"
                recognitionSha256 = (Get-FileHash -Algorithm SHA256 (Join-Path $models "korean_PP-OCRv5_rec.onnx")).Hash
                characterDictionarySha256 = (Get-FileHash -Algorithm SHA256 (Join-Path $models "korean_PP-OCRv5_rec.yml")).Hash
                sourceRevision = "synthetic-test"
                recognitionSourceUrl = "https://example.invalid/korean"
                dictionarySourceUrl = "https://example.invalid/korean-yml"
            },
            @{
                language = "thai"
                recognitionModel = "th_PP-OCRv5_rec.onnx"
                characterDictionary = "th_PP-OCRv5_rec.yml"
                recognitionSha256 = (Get-FileHash -Algorithm SHA256 (Join-Path $models "th_PP-OCRv5_rec.onnx")).Hash
                characterDictionarySha256 = (Get-FileHash -Algorithm SHA256 (Join-Path $models "th_PP-OCRv5_rec.yml")).Hash
                sourceRevision = "synthetic-test"
                recognitionSourceUrl = "https://example.invalid/thai"
                dictionarySourceUrl = "https://example.invalid/thai-yml"
            }
        )
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $models "manifest.json")
    foreach ($package in @(
        "documentformat.openxml-3.3.0",
        "documentformat.openxml.framework-3.3.0",
        "microsoft.ai.directml-1.15.4",
        "microsoft.ml.onnxruntime-1.24.1",
        "microsoft.ml.onnxruntime.managed-1.24.1",
        "microsoft.ml.onnxruntime.directml-1.24.1",
        "system.numerics.tensors-9.0.0",
        "vortice.dxgi-3.8.3",
        "vortice.directx-3.8.3",
        "vortice.mathematics-2.1.0",
        "sharpgen.runtime-2.4.2-beta",
        "sharpgen.runtime.com-2.4.2-beta"
    )) {
        $packagePath = Join-Path $licenses $package
        New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $packagePath "LICENSE.txt") -Value "synthetic notice"
    }

    $verifier = Join-Path $PSScriptRoot "verify-release.ps1"
    Compress-Archive -Path (Join-Path $root "*") -DestinationPath $archive -Force
    (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash | Set-Content -LiteralPath $checksum
    & $verifier -Archive $archive -ChecksumFile $checksum

    foreach ($forbidden in @("screenshot/private.png", "private.gif", "private.bmp", "private.webp", "private.tiff", "private.ico")) {
        $entryPath = Join-Path $root $forbidden
        New-Item -ItemType Directory -Path (Split-Path $entryPath -Parent) -Force | Out-Null
        Set-Content -LiteralPath $entryPath -Value "not allowed"
        if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
        Compress-Archive -Path (Join-Path $root "*") -DestinationPath $archive -Force
        (Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash | Set-Content -LiteralPath $checksum
        $accepted = $false
        try {
            & $verifier -Archive $archive -ChecksumFile $checksum
            $accepted = $true
        }
        catch {
            if ($_.Exception.Message -notmatch "private screenshot/image content") { throw }
        }
        if ($accepted) { throw "Release verifier accepted forbidden entry: $forbidden" }
        Remove-Item -LiteralPath $entryPath -Force
        if ($forbidden -match '/') {
            $parent = Split-Path $entryPath -Parent
            if (Test-Path -LiteralPath $parent) { Remove-Item -LiteralPath $parent -Recurse -Force }
        }
    }
    Write-Host "Release security checks passed for synthetic forbidden-image entries."
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
