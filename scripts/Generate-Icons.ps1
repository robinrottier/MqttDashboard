<#
.SYNOPSIS
    Generates raster PNG icons from mqttdashboard-icon.svg by parsing the SVG
    XML and rendering its <rect> and <line> elements scaled to each target size.

.DESCRIPTION
    Reads the SVG source, extracts the viewBox coordinate space, then scales
    every <rect> and <line> element proportionally to the target PNG dimensions.
    Edit the SVG and run this script (or just build the project) to update PNGs.

    The MSBuild target in MqttDashboard.Client.csproj calls this automatically
    when the SVG is newer than the PNG outputs.

.PARAMETER Force
    Re-generate even if PNGs are already newer than the SVG.

.EXAMPLE
    pwsh scripts/Generate-Icons.ps1
    pwsh scripts/Generate-Icons.ps1 -Force
#>
param([switch]$Force)

$repoRoot = Split-Path -Parent $PSScriptRoot
$wwwroot  = Join-Path $repoRoot "src\MqttDashboard.Client\wwwroot"
$svgPath  = Join-Path $wwwroot "mqttdashboard-icon.svg"

$targets = @(
    @{ Size = 192; File = "icon-192.png" }
    @{ Size = 512; File = "icon-512.png" }
)

if (-not (Test-Path $svgPath)) {
    Write-Error "SVG source not found: $svgPath"
    exit 1
}

$svgTime = (Get-Item $svgPath).LastWriteTimeUtc

# ── Parse SVG ────────────────────────────────────────────────────────────────

[xml]$svg = Get-Content $svgPath -Encoding UTF8
$svgNs    = 'http://www.w3.org/2000/svg'

# viewBox: "minX minY width height"
$vb     = $svg.svg.viewBox -split '\s+'
$vbW    = [double]$vb[2]
$vbH    = [double]$vb[3]

function Parse-HexColor([string]$hex) {
    $hex = $hex.TrimStart('#')
    if ($hex.Length -eq 3) { $hex = "$($hex[0])$($hex[0])$($hex[1])$($hex[1])$($hex[2])$($hex[2])" }
    return [System.Drawing.Color]::FromArgb(
        [Convert]::ToInt32($hex.Substring(0,2),16),
        [Convert]::ToInt32($hex.Substring(2,2),16),
        [Convert]::ToInt32($hex.Substring(4,2),16))
}

Add-Type -AssemblyName System.Drawing

foreach ($t in $targets) {
    $outPath = Join-Path $wwwroot $t.File
    if (-not $Force) {
        if ((Test-Path $outPath) -and ((Get-Item $outPath).LastWriteTimeUtc -ge $svgTime)) {
            Write-Host "  SKIP  $($t.File) (up to date)"
            continue
        }
    }

    $size  = $t.Size
    $scaleX = $size / $vbW
    $scaleY = $size / $vbH

    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # Iterate all child elements of <svg> in document order
    foreach ($el in $svg.svg.ChildNodes) {
        $name = $el.LocalName

        if ($name -eq 'rect') {
            # Skip percentage-based background rects
            if ($el.width -match '%' -or $el.height -match '%') { continue }

            $fill    = $el.GetAttribute('fill')
            $opacity = $el.GetAttribute('opacity')
            if ([string]::IsNullOrEmpty($fill) -or $fill -eq 'none') { continue }
            if ($opacity -eq '0') { continue }

            $color = Parse-HexColor $fill
            if (-not [string]::IsNullOrEmpty($opacity)) {
                $a = [int]([double]$opacity * 255)
                $color = [System.Drawing.Color]::FromArgb($a, $color)
            }
            $brush = New-Object System.Drawing.SolidBrush $color

            $rx = [float]([double]$el.x * $scaleX)
            $ry = [float]([double]$el.y * $scaleY)
            $rw = [float]([double]$el.width  * $scaleX)
            $rh = [float]([double]$el.height * $scaleY)

            # Round corners (rx/ry SVG attributes)?
            $rrx = $el.GetAttribute('rx'); $rry = $el.GetAttribute('ry')
            if (-not [string]::IsNullOrEmpty($rrx) -and $rrx -ne '0') {
                $radius = [float]([double]$rrx * $scaleX * 2)
                $path = New-Object System.Drawing.Drawing2D.GraphicsPath
                $path.AddArc($rx, $ry, $radius, $radius, 180, 90)
                $path.AddArc($rx + $rw - $radius, $ry, $radius, $radius, 270, 90)
                $path.AddArc($rx + $rw - $radius, $ry + $rh - $radius, $radius, $radius, 0, 90)
                $path.AddArc($rx, $ry + $rh - $radius, $radius, $radius, 90, 90)
                $path.CloseFigure()
                $g.FillPath($brush, $path)
                $path.Dispose()
            } else {
                $g.FillRectangle($brush, $rx, $ry, $rw, $rh)
            }
            $brush.Dispose()
        }
        elseif ($name -eq 'line') {
            $stroke = $el.GetAttribute('stroke')
            if ([string]::IsNullOrEmpty($stroke) -or $stroke -eq 'none') { continue }

            $sw  = $el.GetAttribute('stroke-width')
            $swF = if ([string]::IsNullOrEmpty($sw)) { 1.0 } else { [double]$sw }

            $color = Parse-HexColor $stroke
            $pen   = [System.Drawing.Pen]::new($color, [float]($swF * $scaleX))
            $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
            $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

            $g.DrawLine($pen,
                [float]([double]$el.x1 * $scaleX), [float]([double]$el.y1 * $scaleY),
                [float]([double]$el.x2 * $scaleX), [float]([double]$el.y2 * $scaleY))
            $pen.Dispose()
        }
        # Other element types (circle, path, etc.) are not currently used in
        # the icon SVG — add cases here if the SVG design is extended.
    }

    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()

    Write-Host "  GEN   $($t.File) (${size}x${size})"
}

Write-Host "Done."
