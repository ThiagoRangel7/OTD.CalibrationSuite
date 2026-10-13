param([Parameter(Mandatory)][string] $OutputPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$assets = Get-Content (Join-Path $root 'src/CalibrationSuite.Calibrator/obj/project.assets.json') -Raw | ConvertFrom-Json
$mit = [regex]::Match((Get-Content (Join-Path $root 'LICENSE') -Raw), '(?s)Permission is hereby granted.*').Value
$sections = @('Calibrator runtime dependency notices', 'OpenTabletDriver 0.6.7: LGPL-3.0-or-later. See OpenTabletDriver.LICENSE, GPL-3.0.txt, and the accompanying source archive.')
foreach ($package in $assets.libraries.PSObject.Properties | Sort-Object Name) {
    if ($package.Value.type -ne 'package') { continue }
    $folder = Join-Path $root "artifacts/packages/$($package.Value.path)"
    $manifest = Get-ChildItem -LiteralPath $folder -Filter '*.nuspec' | Select-Object -First 1
    [xml] $xml = Get-Content -LiteralPath $manifest.FullName -Raw
    $metadata = $xml.package.metadata
    $license = $metadata.license.InnerText
    $licenseText = switch ($license) {
        'MIT' { $mit }
        'BSD-3-Clause' { Get-Content (Join-Path $root 'packaging/NetTopologySuite.LICENSE.md') -Raw }
        'ISC' { Get-Content (Join-Path $root 'packaging/licenses/WaylandNET.LICENSE.txt') -Raw }
        default {
            if ($package.Name -ne 'HidSharpCore/1.3.0' -or $metadata.repository.commit -ne 'bca4aee1c349985b0119eed24aba83e2dd63492d') {
                throw "Review the redistribution license for $($package.Name)."
            }
            $license = 'Apache-2.0'
            Get-Content (Join-Path $root 'packaging/licenses/HidSharpCore.LICENSE.txt') -Raw
        }
    }
    $sections += "$($package.Name) [$license]`n$($metadata.copyright)`nAuthors: $($metadata.authors)`nSource: $($metadata.repository.url) $($metadata.repository.commit)`n`n$licenseText"
    foreach ($notice in Get-ChildItem -LiteralPath $folder -Recurse -File | Where-Object { $_.Name -match '^(NOTICE|LICENSE)' }) {
        $sections += "$($package.Name) / $($notice.Name)`n$(Get-Content -LiteralPath $notice.FullName -Raw)"
    }
}
Set-Content -LiteralPath $OutputPath -Value ($sections -join "`n`n----------------------------------------`n`n") -Encoding UTF8
