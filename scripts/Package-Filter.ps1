$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'artifacts/filter-package'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$files = @(
    'src/CalibrationSuite.Filter/bin/Release/net8.0/CalibrationSuite.Filter.dll',
    'src/CalibrationSuite.Core/bin/Release/net8.0/CalibrationSuite.Core.dll',
    'artifacts/packages/nettopologysuite/2.6.0/lib/netstandard2.1/NetTopologySuite.dll',
    'packaging/metadata.json',
    'packaging/NOTICE.txt',
    'packaging/NetTopologySuite.LICENSE.md'
)
# An explicit list keeps host-owned and Calibrator dependencies out of the ZIP.
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $file))) {
        throw "Missing $file. Run the README restore and Release build first."
    }
}
$path = Join-Path $output 'CalibrationSuite.zip'
$stream = [IO.File]::Open($path, [IO.FileMode]::Create)
try {
    $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) {
            $entry = $zip.CreateEntry([IO.Path]::GetFileName($file))
            $entry.LastWriteTime = [DateTimeOffset]::new(2026, 9, 13, 0, 0, 0, [TimeSpan]::Zero)
            $target = $entry.Open()
            $source = [IO.File]::OpenRead((Join-Path $root $file))
            try { $source.CopyTo($target) } finally { $source.Dispose(); $target.Dispose() }
        }
    } finally { $zip.Dispose() }
} finally { $stream.Dispose() }
Get-FileHash -LiteralPath $path -Algorithm SHA256
