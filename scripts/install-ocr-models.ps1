param(
    [Parameter(Mandatory = $true)][string]$DetectionUrl,
    [Parameter(Mandatory = $true)][string]$DetectionSha256,
    [Parameter(Mandatory = $true)][string]$RecognitionUrl,
    [Parameter(Mandatory = $true)][string]$RecognitionSha256,
    [Parameter(Mandatory = $true)][string]$DictionaryUrl,
    [Parameter(Mandatory = $true)][string]$DictionarySha256,
    [Parameter(Mandatory = $true)][string]$ModelVersion,
    [Parameter(Mandatory = $true)][string]$SourceRevision,
    [Parameter(Mandatory = $true)][string]$LicenseNotice
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($LicenseNotice) -or $LicenseNotice -match "PENDING|must be verified|placeholder") {
    throw "LicenseNotice must be a verified, non-placeholder license statement."
}
if (([string]::IsNullOrWhiteSpace($ModelVersion)) -or ($ModelVersion -match "PENDING|placeholder") -or ([string]::IsNullOrWhiteSpace($SourceRevision)) -or ($SourceRevision -match "PENDING|placeholder")) {
    throw "ModelVersion and SourceRevision must be provided and non-placeholder."
}
$modelDir = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")) "models"
New-Item -ItemType Directory -Path $modelDir -Force | Out-Null

function Download-Verified([string]$url, [string]$name, [string]$hash) {
    $uri = [Uri]$url
    if ($uri.Scheme -ne "https") { throw "Model downloads must use HTTPS: $name" }
    if ($hash -notmatch '^[0-9A-Fa-f]{64}$') { throw "SHA-256 must contain exactly 64 hexadecimal characters: $name" }
    $target = Join-Path $modelDir $name
    $temporary = "$target.$([Guid]::NewGuid().ToString('N')).download"
    try {
        Invoke-WebRequest -Uri $url -OutFile $temporary
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $temporary).Hash
        if ($actual -ne $hash) { throw "SHA-256 mismatch for $name" }
        Move-Item -LiteralPath $temporary -Destination $target -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}

foreach ($hash in @($DetectionSha256, $RecognitionSha256, $DictionarySha256)) {
    if ($hash -notmatch '^[0-9A-Fa-f]{64}$') { throw "All model SHA-256 values must contain exactly 64 hexadecimal characters." }
}

Download-Verified $DetectionUrl "PP-OCRv5_det.onnx" $DetectionSha256
Download-Verified $RecognitionUrl "PP-OCRv5_rec.onnx" $RecognitionSha256
Download-Verified $DictionaryUrl "ppocrv5_dict.txt" $DictionarySha256

$manifest = [ordered]@{
    detectionModel = "PP-OCRv5_det.onnx"
    recognitionModel = "PP-OCRv5_rec.onnx"
    characterDictionary = "ppocrv5_dict.txt"
    license = $LicenseNotice
    modelVersion = $ModelVersion
    sourceRevision = $SourceRevision
    detectionSourceUrl = $DetectionUrl
    recognitionSourceUrl = $RecognitionUrl
    dictionarySourceUrl = $DictionaryUrl
    detectionSha256 = $DetectionSha256.ToUpperInvariant()
    recognitionSha256 = $RecognitionSha256.ToUpperInvariant()
    characterDictionarySha256 = $DictionarySha256.ToUpperInvariant()
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $modelDir "manifest.json")
Write-Host "OCR models downloaded, verified, and manifest.json generated in $modelDir."
