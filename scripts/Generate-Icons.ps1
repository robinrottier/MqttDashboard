<#
.SYNOPSIS
    Generates raster PNG icons from the mqttdashboard-icon.svg source.

.DESCRIPTION
    Reads the SVG layout and renders it to PNG at standard PWA icon sizes
    (192x192 and 512x512). Uses System.Drawing — no external dependencies.

    Run after editing mqttdashboard-icon.svg, or let the MSBuild target in
    MqttDashboard.Client.csproj trigger this automatically on build.

.PARAMETER Force
    Re-generate even if PNGs are newer than the SVG.

.EXAMPLE
    pwsh scripts/Generate-Icons.ps1
    pwsh scripts/Generate-Icons.ps1 -Force
#>
param([switch]$Force)

$repoRoot = Split-Path -Parent $PSScriptRoot
$wwwroot   = Join-Path $repoRoot "src\MqttDashboard.Client\wwwroot"
$svgPath   = Join-Path $wwwroot "mqttdashboard-icon.svg"

$targets = @(
    @{ Size = 192; File = "icon-192.png" }
    @{ Size = 512; File = "icon-512.png" }
)

if (-not (Test-Path $svgPath)) {
    Write-Error "SVG source not found: $svgPath"
    exit 1
}

$svgTime = (Get-Item $svgPath).LastWriteTimeUtc

Add-Type -AssemblyName System.Drawing

foreach ($t in $targets) {
    $outPath = Join-Path $wwwroot $t.File
    if (-not $Force) {
        if ((Test-Path $outPath) -and ((Get-Item $outPath).LastWriteTimeUtc -ge $svgTime)) {
            Write-Host "  SKIP  $($t.File) (up to date)"
            continue
        }
    }

    $size   = $t.Size
    $color  = [System.Drawing.Color]::FromArgb(0x59, 0x4a, 0xe2)
    $brush  = New-Object System.Drawing.SolidBrush $color
    $penW   = [int][math]::Max(2, [math]::Round($size * 0.046875))
    $pen    = New-Object System.Drawing.Pen $brush, $penW

    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Three square nodes: bottom-left, bottom-right, top-centre (rotated 90 deg vs original)
    [double]$cx1 = $size * 0.25;  [double]$cy1 = $size * 0.75
    [double]$cx2 = $size * 0.75;  [double]$cy2 = $size * 0.75
    [double]$cx3 = $size * 0.50;  [double]$cy3 = $size * 0.25
    [double]$half = $size * 0.0833333
    [double]$side = $half * 2

    $g.FillRectangle($brush, [float]($cx1 - $half), [float]($cy1 - $half), [float]$side, [float]$side)
    $g.FillRectangle($brush, [float]($cx2 - $half), [float]($cy2 - $half), [float]$side, [float]$side)
    $g.FillRectangle($brush, [float]($cx3 - $half), [float]($cy3 - $half), [float]$side, [float]$side)

    # Connecting lines from cx3 (top) down to cx1 and cx2
    $g.DrawLine($pen, [float]($cx1 + $half), [float]$cy1, [float]$cx3, [float]($cy3 + $half))
    $g.DrawLine($pen, [float]$cx3,           [float]($cy3 + $half), [float]($cx2 - $half), [float]$cy2)

    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()

    Write-Host "  GEN   $($t.File) (${size}x${size})"
}

Write-Host "Done."
