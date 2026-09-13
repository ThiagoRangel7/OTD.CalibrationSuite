$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = [IO.Path]::GetFullPath((Join-Path $root '../OpenTabletDriver-0.6.7'))
$destination = Join-Path $root 'artifacts/otd-source'
# Build an unmodified local source copy: never write bin/obj into the reference repository.
$projects = @('OpenTabletDriver', 'OpenTabletDriver.Plugin', 'OpenTabletDriver.Native', 'OpenTabletDriver.Configurations', 'OpenTabletDriver.Desktop')
$files = @('Directory.Build.props', 'README.md', 'LICENSE')
foreach ($project in $projects) {
    $files += Get-ChildItem -LiteralPath (Join-Path $source $project) -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.git)[\\/]' } |
        ForEach-Object { [IO.Path]::GetRelativePath($source, $_.FullName) }
}
$manifest = foreach ($relative in ($files | Sort-Object)) {
    $original = Join-Path $source $relative
    $hash = (Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
    "$hash  $($relative.Replace('\', '/'))"
}
$expected = Join-Path $PSScriptRoot 'otd-source.sha256'
if (Compare-Object (Get-Content -LiteralPath $expected) $manifest) {
    throw 'OTD reference source differs from the reviewed manifest. Review the dependency change before building.'
}
foreach ($relative in $files) {
    $target = Join-Path $destination $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath (Join-Path $source $relative) -Destination $target
}
foreach ($project in $projects) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "otd-locks/$project.json") -Destination (Join-Path $destination "$project/packages.lock.json")
}
Write-Output "Prepared OTD 0.6.7 sources in $destination"
