$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$definitions = Join-Path $root 'dependencies/otd'
$pin = Get-Content (Join-Path $definitions 'source.json') -Raw | ConvertFrom-Json
$destination = Join-Path $root 'artifacts/otd-source'
$stamp = Join-Path $destination '.prepared'
if ((Test-Path $stamp) -and (Get-Content $stamp -Raw).Trim() -eq $pin.commit) { return }
New-Item -ItemType Directory -Force (Join-Path $root 'artifacts') | Out-Null
# Serialize preparation across parallel MSBuild nodes.
$lockPath = Join-Path $root 'artifacts/otd-source.lock'
$deadline = [DateTime]::UtcNow.AddMinutes(3)
$lock = $null
while (-not $lock) {
    try { $lock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None') }
    catch [IO.IOException] {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Timed out waiting for OTD source preparation.' }
        Start-Sleep -Milliseconds 200
    }
}
try {
    if ((Test-Path $stamp) -and (Get-Content $stamp -Raw).Trim() -eq $pin.commit) { return }
    $archive = Join-Path $root "artifacts/otd-$($pin.commit).zip"
    if (-not (Test-Path $archive)) {
        Write-Output "Downloading OTD $($pin.version) source ($($pin.commit))..."
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri "https://codeload.github.com/OpenTabletDriver/OpenTabletDriver/zip/$($pin.commit)" -OutFile $archive -TimeoutSec 120
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $pin.sha256) {
        Remove-Item -LiteralPath $archive
        throw 'OTD source checksum mismatch. Archive removed; retry the build or review the source pin.'
    }
    # Only replace this fixed repository-local generated folder.
    if (Test-Path $destination) {
        if ([IO.Path]::GetFullPath($destination) -ne [IO.Path]::GetFullPath((Join-Path $root 'artifacts/otd-source'))) { throw 'Unexpected source destination.' }
        Remove-Item -LiteralPath $destination -Recurse -Force
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $prefix = "OpenTabletDriver-$($pin.commit)/"
        $projects = Get-ChildItem $definitions -Directory | Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).csproj") } | Select-Object -ExpandProperty Name
        foreach ($entry in $zip.Entries) {
            if (-not $entry.FullName.StartsWith($prefix) -or -not $entry.Name) { continue }
            $relative = $entry.FullName.Substring($prefix.Length)
            if ($relative.Split('/')[0] -notin $projects -and $relative -notin @('LICENSE', 'README.md')) { continue }
            $target = [IO.Path]::GetFullPath((Join-Path $destination $relative))
            if (-not $target.StartsWith([IO.Path]::GetFullPath($destination) + [IO.Path]::DirectorySeparatorChar)) { throw 'Invalid archive path.' }
            New-Item -ItemType Directory -Force (Split-Path $target -Parent) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
        foreach ($project in $projects) {
            $file = "$project/$project.csproj"
            if ((Get-FileHash -LiteralPath (Join-Path $destination $file) -Algorithm SHA256).Hash -ne
                (Get-FileHash -LiteralPath (Join-Path $definitions $file) -Algorithm SHA256).Hash) {
                throw "Pinned build definition differs from upstream: $file. Update definitions with the source pin."
            }
        }
        Set-Content $stamp $pin.commit
    } finally { $zip.Dispose() }
    Write-Output "Prepared OTD $($pin.version) source."
} finally { $lock.Dispose() }
