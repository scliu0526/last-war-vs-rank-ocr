param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts\win-x64"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$out = Join-Path $repo $Output
dotnet publish (Join-Path $repo "RankLens.App\RankLens.App.csproj") `
    --configuration $Configuration --runtime win-x64 --self-contained true `
    --output $out

Write-Host "Published RankLens to $out"
