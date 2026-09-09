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
if (([string]::IsNullOrWhiteSpace($manifestJson.license)) -or ($manifestJson.license -match "PENDING|must be verified|placeholder")) {
    throw "models/manifest.json still contains an unresolved license notice."
}
foreach ($metadata in @($manifestJson.modelVersion, $manifestJson.sourceRevision, $manifestJson.detectionSourceUrl, $manifestJson.recognitionSourceUrl, $manifestJson.dictionarySourceUrl)) {
    if ([string]::IsNullOrWhiteSpace($metadata) -or $metadata -match "PENDING|placeholder") { throw "models/manifest.json is missing verified model provenance metadata." }
}
foreach ($sourceUrl in @($manifestJson.detectionSourceUrl, $manifestJson.recognitionSourceUrl, $manifestJson.dictionarySourceUrl)) {
    $parsedUrl = $null
    if (-not [Uri]::TryCreate($sourceUrl, [UriKind]::Absolute, [ref]$parsedUrl) -or $parsedUrl.Scheme -ne "https") { throw "Model provenance URLs must use HTTPS absolute URLs." }
}
foreach ($entry in @(
    @{ Name = $manifestJson.detectionModel; Hash = $manifestJson.detectionSha256 },
    @{ Name = $manifestJson.recognitionModel; Hash = $manifestJson.recognitionSha256 },
    @{ Name = $manifestJson.characterDictionary; Hash = $manifestJson.characterDictionarySha256 }
)) {
    if ([string]::IsNullOrWhiteSpace($entry.Hash) -or $entry.Hash -notmatch '^[0-9A-Fa-f]{64}$') {
        throw "Manifest hash is not a canonical SHA-256 value: $($entry.Name)"
    }
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
foreach ($document in @("LICENSE", "THIRD-PARTY-NOTICES.md", "README.md", "README.zh-TW.md")) {
    Copy-Item (Join-Path $repo $document) (Join-Path $out $document) -Force
}
$archive = Join-Path (Split-Path $out -Parent) "RankLens-win-x64.zip"
if (Test-Path $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $archive -Force
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $archive
$hash.Hash | Set-Content -LiteralPath "$archive.sha256"

Write-Host "Published RankLens to $archive"
