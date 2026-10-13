$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$metadata = Get-Content (Join-Path $root 'packaging/metadata.json') -Raw | ConvertFrom-Json
$version = $metadata.PluginVersion
if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Use a four-part release version in metadata.json.' }
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts/releases'))
$output = Join-Path $releaseRoot $version
# Recreate only this version's generated staging directory, so obsolete DLLs cannot ship.
if (-not ([IO.Path]::GetFullPath($output)).StartsWith($releaseRoot + [IO.Path]::DirectorySeparatorChar)) { throw 'Unexpected release path.' }
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
$calibrator = Join-Path $output 'calibrator'
New-Item -ItemType Directory -Force -Path $calibrator | Out-Null
Push-Location $root
try {
    dotnet build src/CalibrationSuite.Filter/CalibrationSuite.Filter.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Filter build failed.' }
    dotnet publish src/CalibrationSuite.Calibrator/CalibrationSuite.Calibrator.csproj -c Release --no-restore --self-contained false -p:DebugType=None -o $calibrator
    if ($LASTEXITCODE -ne 0) { throw 'Calibrator publish failed.' }
    Copy-Item -LiteralPath (Join-Path $root 'artifacts/filter-package/Release/CalibrationSuite.zip') -Destination $output
    Copy-Item -LiteralPath (Join-Path $root 'packaging/CALIBRATOR-README.txt') -Destination (Join-Path $calibrator 'README.txt')
    & (Join-Path $PSScriptRoot 'Write-DependencyNotices.ps1') -OutputPath (Join-Path $calibrator 'notices/THIRD-PARTY-NOTICES.txt')
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $appArchive = [IO.Compression.ZipFile]::Open((Join-Path $output 'CalibrationSuite.Calibrator.Windows.zip'), 'Create')
    try {
        foreach ($file in Get-ChildItem -LiteralPath $calibrator -Recurse -File | Sort-Object FullName) {
            $relative = $file.FullName.Substring($calibrator.Length + 1).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($appArchive, $file.FullName, $relative) | Out-Null
        }
    } finally { $appArchive.Dispose() }
    # Include the actual working-tree build inputs and the exact upstream LGPL source.
    $source = [IO.Compression.ZipFile]::Open((Join-Path $output 'CalibrationSuite.Source.zip'), 'Create')
    try {
        foreach ($file in git ls-files --cached --others --exclude-standard) {
            if (Test-Path -LiteralPath $file -PathType Leaf) {
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($source, (Join-Path $root $file), $file) | Out-Null
            }
        }
        $pin = Get-Content (Join-Path $root 'dependencies/otd/source.json') -Raw | ConvertFrom-Json
        $upstream = Join-Path $root "artifacts/otd-$($pin.commit).zip"
        if ((Get-FileHash -LiteralPath $upstream -Algorithm SHA256).Hash -ne $pin.sha256) { throw 'OTD source archive does not match the pin.' }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($source, $upstream, 'third-party/OpenTabletDriver-source.zip') | Out-Null
    } finally { $source.Dispose() }
    $metadata | Add-Member -NotePropertyName DownloadUrl -NotePropertyValue "$($metadata.RepositoryUrl)/releases/download/v$version/CalibrationSuite.zip"
    $metadata | Add-Member -NotePropertyName SHA256 -NotePropertyValue (Get-FileHash (Join-Path $output 'CalibrationSuite.zip') -Algorithm SHA256).Hash.ToLowerInvariant()
    $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'CalibrationSuite.json') -Encoding UTF8
    Get-ChildItem -LiteralPath $output -Filter '*.zip' | ForEach-Object {
        "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($_.Name)"
    } | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
    Write-Output "Release candidates and registry metadata: $output"
} finally { Pop-Location }
