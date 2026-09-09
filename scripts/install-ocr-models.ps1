param(
    [Parameter(Mandatory = $true)][string]$DetectionUrl,
    [Parameter(Mandatory = $true)][string]$DetectionSha256,
    [Parameter(Mandatory = $true)][string]$RecognitionUrl,
    [Parameter(Mandatory = $true)][string]$RecognitionSha256,
    [string]$DictionaryUrl,
    [string]$DictionarySha256
)

$ErrorActionPreference = "Stop"
$modelDir = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")) "models"
New-Item -ItemType Directory -Path $modelDir -Force | Out-Null

function Download-Verified([string]$url, [string]$name, [string]$hash) {
    $target = Join-Path $modelDir $name
    Invoke-WebRequest -Uri $url -OutFile $target
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash
    if ($actual -ne $hash) { throw "SHA-256 mismatch for $name" }
}

Download-Verified $DetectionUrl "PP-OCRv5_det.onnx" $DetectionSha256
Download-Verified $RecognitionUrl "PP-OCRv5_rec.onnx" $RecognitionSha256
if ($DictionaryUrl) { Download-Verified $DictionaryUrl "ppocrv5_dict.txt" $DictionarySha256 }
Write-Host "OCR models downloaded and verified in $modelDir. Update models/manifest.json with the same hashes and license notice."
