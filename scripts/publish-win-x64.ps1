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
