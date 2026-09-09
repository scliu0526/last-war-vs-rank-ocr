param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts\win-x64"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$out = Join-Path $repo $Output
$manifest = Join-Path $repo "models\manifest.json"
$manifestText = Get-Content $manifest -Raw
if ($manifestText -match "PENDING_") { throw "models/manifest.json still contains an unresolved hash." }
$manifestJson = $manifestText | ConvertFrom-Json
foreach ($entry in @(
    @{ Name = $manifestJson.detectionModel; Hash = $manifestJson.detectionSha256 },
    @{ Name = $manifestJson.recognitionModel; Hash = $manifestJson.recognitionSha256 },
    @{ Name = $manifestJson.characterDictionary; Hash = $null }
)) {
    $source = Join-Path (Join-Path $repo "models") $entry.Name
    if (-not (Test-Path -LiteralPath $source)) { throw "Missing model file: $($entry.Name)" }
    if ($entry.Hash) {
        $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash
        if ($actual -ne $entry.Hash) { throw "Model hash mismatch: $($entry.Name)" }
    }
}
dotnet publish (Join-Path $repo "RankLens.App\RankLens.App.csproj") `
    --configuration $Configuration --runtime win-x64 --self-contained true `
    --output $out

Copy-Item (Join-Path $repo "models") (Join-Path $out "models") -Recurse -Force
$archive = Join-Path (Split-Path $out -Parent) "RankLens-win-x64.zip"
if (Test-Path $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $archive -Force
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $archive
$hash.Hash | Set-Content -LiteralPath "$archive.sha256"

Write-Host "Published RankLens to $archive"
