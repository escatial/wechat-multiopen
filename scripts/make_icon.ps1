# Generate the app icon: WeChat-green gradient rounded square + two white chat
# bubbles (multi-instance motif). Writes a multi-size PNG-encoded .ico and a
# 256px PNG preview.
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\src\WechatMultiopen\Assets\app.ico"),
    [string]$PreviewPath = (Join-Path $PSScriptRoot "..\artifacts\icon-preview-256.png")
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    try {
        $rect = New-Object System.Drawing.RectangleF(0, 0, [float]$s, [float]$s)
        $corner = [Math]::Max(2.0, [float]($s * 0.225))

        # Background gradient (WeChat green)
        $bgPath = New-RoundedRectPath 0 0 $s $s $corner
        $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $rect,
            [System.Drawing.ColorTranslator]::FromHtml("#2ED57F"),
            [System.Drawing.ColorTranslator]::FromHtml("#049D4E"),
            [float]45)
        $g.FillPath($gradient, $bgPath)
        $gradient.Dispose(); $bgPath.Dispose()

        # Back bubble (semi-transparent for depth)
        $backPath = New-RoundedRectPath ([float]($s * 0.50)) ([float]($s * 0.13)) ([float]($s * 0.36)) ([float]($s * 0.27)) ([float]($s * 0.075))
        $backBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(165, 255, 255, 255))
        $g.FillPath($backBrush, $backPath)
        $backBrush.Dispose(); $backPath.Dispose()

        # Front bubble + bottom-left tail, filled once to avoid seams
        $front = New-RoundedRectPath ([float]($s * 0.15)) ([float]($s * 0.30)) ([float]($s * 0.58)) ([float]($s * 0.36)) ([float]($s * 0.10))
        $tail = New-Object System.Drawing.Drawing2D.GraphicsPath
        $tail.AddPolygon([System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF([float]($s * 0.26), [float]($s * 0.60))),
            (New-Object System.Drawing.PointF([float]($s * 0.26), [float]($s * 0.78))),
            (New-Object System.Drawing.PointF([float]($s * 0.45), [float]($s * 0.61)))
        ))
        $front.AddPath($tail, $false)
        $g.FillPath([System.Drawing.Brushes]::White, $front)
        $tail.Dispose(); $front.Dispose()

        # Typing dots inside the front bubble (large sizes only)
        if ($s -ge 40) {
            $dotBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml("#07A55A"))
            foreach ($cx in @([float]($s * 0.34), [float]($s * 0.54))) {
                $dotRect = New-Object System.Drawing.RectangleF(
                    [float]($cx - $s * 0.045), [float]($s * 0.435), [float]($s * 0.09), [float]($s * 0.09))
                $g.FillEllipse($dotBrush, $dotRect)
            }
            $dotBrush.Dispose()
        }
    }
    finally { $g.Dispose() }
    return $bmp
}

$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }

$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$pngs = @{}
foreach ($s in $sizes) {
    $bitmap = New-IconBitmap $s
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs[$s] = $stream.ToArray()
    if ($s -eq 256 -and $PreviewPath) {
        $previewDir = Split-Path -Parent $PreviewPath
        if (-not (Test-Path $previewDir)) { New-Item -ItemType Directory -Path $previewDir | Out-Null }
        $bitmap.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $bitmap.Dispose()
}

$writer = New-Object System.IO.BinaryWriter([System.IO.File]::Create($OutputPath))
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($s in $sizes) {
    $dim = if ($s -ge 256) { 0 } else { $s }
    $writer.Write([byte]$dim); $writer.Write([byte]$dim)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$pngs[$s].Length); $writer.Write([uint32]$offset)
    $offset += $pngs[$s].Length
}
foreach ($s in $sizes) { $writer.Write($pngs[$s]) }
$writer.Flush(); $writer.Dispose()
Write-Host "Icon generated: $OutputPath"
