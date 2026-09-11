param(
    [string]$Configuration = "Release",
    [string]$Output = "artifacts\win-x64"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$out = Join-Path $repo $Output
$outFullPath = [IO.Path]::GetFullPath($out)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repo "artifacts"))
if (-not $outFullPath.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Output must remain under the repository artifacts directory."
}
if (Test-Path -LiteralPath $outFullPath) { Remove-Item -LiteralPath $outFullPath -Recurse -Force }
New-Item -ItemType Directory -Path $outFullPath -Force | Out-Null
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
$modelEntries = @(
    @{ Name = $manifestJson.detectionModel; Hash = $manifestJson.detectionSha256 },
    @{ Name = $manifestJson.recognitionModel; Hash = $manifestJson.recognitionSha256 },
    @{ Name = $manifestJson.characterDictionary; Hash = $manifestJson.characterDictionarySha256 }
)
foreach ($variant in @($manifestJson.recognitionVariants)) {
    if ($variant.language -notin @("korean", "thai") -or [string]::IsNullOrWhiteSpace($variant.sourceRevision)) {
        throw "Recognition variant provenance is invalid."
    }
    foreach ($sourceUrl in @($variant.recognitionSourceUrl, $variant.dictionarySourceUrl)) {
        $parsedUrl = $null
        if (-not [Uri]::TryCreate($sourceUrl, [UriKind]::Absolute, [ref]$parsedUrl) -or $parsedUrl.Scheme -ne "https") { throw "Recognition variant provenance URLs must use HTTPS absolute URLs." }
    }
    $modelEntries += @{ Name = $variant.recognitionModel; Hash = $variant.recognitionSha256 }
    $modelEntries += @{ Name = $variant.characterDictionary; Hash = $variant.characterDictionarySha256 }
}
foreach ($entry in $modelEntries) {
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

$publishedModels = Join-Path $out "models"
New-Item -ItemType Directory -Path $publishedModels -Force | Out-Null
foreach ($modelFile in @($modelEntries.Name) + @("manifest.json", "README.md")) {
    Copy-Item (Join-Path $repo "models\$modelFile") (Join-Path $publishedModels $modelFile) -Force
}
foreach ($document in @("LICENSE", "THIRD-PARTY-NOTICES.md", "README.md", "README.zh-TW.md")) {
    Copy-Item (Join-Path $repo $document) (Join-Path $out $document) -Force
}
$modelNotice = Join-Path $out "licenses\paddleocr-models"
New-Item -ItemType Directory -Path $modelNotice -Force | Out-Null
Copy-Item (Join-Path $repo "licenses\paddleocr-models\NOTICE.md") (Join-Path $modelNotice "NOTICE.md") -Force
$globalPackages = $env:NUGET_PACKAGES
if ([string]::IsNullOrWhiteSpace($globalPackages)) {
    $localsOutput = & dotnet nuget locals global-packages --list 2>$null
    $localsLine = $localsOutput | Where-Object { $_ -match ':' } | Select-Object -First 1
    if ($null -ne $localsLine) { $globalPackages = ($localsLine -split ':', 2)[1].Trim() }
}
if ([string]::IsNullOrWhiteSpace($globalPackages)) {
    $configPath = Join-Path $repo "NuGet.Config"
    if (Test-Path -LiteralPath $configPath) {
        $config = [xml](Get-Content -LiteralPath $configPath -Raw)
        $configuredFolder = $config.configuration.config.add | Where-Object { $_.key -eq "globalPackagesFolder" } | Select-Object -First 1 -ExpandProperty value
        if (-not [string]::IsNullOrWhiteSpace($configuredFolder)) {
            $globalPackages = if ([IO.Path]::IsPathRooted($configuredFolder)) { $configuredFolder } else { Join-Path $repo $configuredFolder }
        }
    }
}
if ([string]::IsNullOrWhiteSpace($globalPackages)) { $globalPackages = Join-Path $env:USERPROFILE ".nuget\packages" }
$noticeRoot = Join-Path $out "licenses"
New-Item -ItemType Directory -Path $noticeRoot -Force | Out-Null
foreach ($package in @(
    @{ Id = "documentformat.openxml"; Version = "3.3.0" },
    @{ Id = "documentformat.openxml.framework"; Version = "3.3.0" },
    @{ Id = "microsoft.ai.directml"; Version = "1.15.4" },
    @{ Id = "microsoft.ml.onnxruntime"; Version = "1.24.1" },
    @{ Id = "microsoft.ml.onnxruntime.managed"; Version = "1.24.1" },
    @{ Id = "microsoft.ml.onnxruntime.directml"; Version = "1.24.1" },
    @{ Id = "system.management"; Version = "9.0.9" },
    @{ Id = "system.numerics.tensors"; Version = "9.0.0" }
)) {
    $packageDir = Join-Path (Join-Path $globalPackages $package.Id) $package.Version
    if (-not (Test-Path -LiteralPath $packageDir)) { throw "Resolved package is missing from the NuGet cache: $($package.Id) $($package.Version)" }
    $targetDir = Join-Path $noticeRoot "$($package.Id)-$($package.Version)"
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    $noticeFiles = Get-ChildItem -LiteralPath $packageDir -File | Where-Object { $_.Name -match '^(LICENSE|ThirdPartyNotices|THIRD-PARTY-NOTICES)' }
    if ($noticeFiles.Count -gt 0) {
        Copy-Item -LiteralPath $noticeFiles.FullName -Destination $targetDir -Force
    }
    else {
        $repositoryNoticeDir = Join-Path $repo "licenses\$($package.Id)-$($package.Version)"
        if (-not (Test-Path -LiteralPath $repositoryNoticeDir)) { throw "No package notice file found: $($package.Id) $($package.Version)" }
        $repositoryNoticeFiles = Get-ChildItem -LiteralPath $repositoryNoticeDir -File
        if ($repositoryNoticeFiles.Count -eq 0) { throw "Repository notice directory is empty: $($package.Id) $($package.Version)" }
        Copy-Item -LiteralPath $repositoryNoticeFiles.FullName -Destination $targetDir -Force
    }
}
$archive = Join-Path (Split-Path $out -Parent) "RankLens-win-x64.zip"
if (Test-Path $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $archive -Force
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $archive
$hash.Hash | Set-Content -LiteralPath "$archive.sha256"

Write-Host "Published RankLens to $archive"
