$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root "scenario22\Assets"
New-Item -ItemType Directory -Force -Path $assets | Out-Null

function New-RoundedRectPath {
    param([System.Drawing.RectangleF]$Rect, [float]$Radius)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $Radius * 2
    $path.AddArc($Rect.X, $Rect.Y, $d, $d, 180, 90)
    $path.AddArc($Rect.Right - $d, $Rect.Y, $d, $d, 270, 90)
    $path.AddArc($Rect.Right - $d, $Rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap {
    param([int]$Size)
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(0.4, $Size * 0.025)
    $rect = New-Object System.Drawing.RectangleF($pad, $pad, ($Size - $pad * 2), ($Size - $pad * 2))
    $radius = $Size * 0.225
    $shape = New-RoundedRectPath -Rect $rect -Radius $radius

    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)),
        (New-Object System.Drawing.PointF($Size, $Size)),
        [System.Drawing.Color]::FromArgb(255, 85, 121, 250),
        [System.Drawing.Color]::FromArgb(255, 132, 92, 246)
    )
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend(3)
    $blend.Colors = @(
        [System.Drawing.Color]::FromArgb(255, 88, 150, 255),
        [System.Drawing.Color]::FromArgb(255, 91, 112, 250),
        [System.Drawing.Color]::FromArgb(255, 128, 82, 242)
    )
    $blend.Positions = @(0.0, 0.48, 1.0)
    $brush.InterpolationColors = $blend
    $g.FillPath($brush, $shape)

    # Soft highlight in the top-left corner.
    $highlight = New-Object System.Drawing.Drawing2D.GraphicsPath
    $glowRect = New-Object System.Drawing.RectangleF(($Size * 0.02), ($Size * 0.02), ($Size * 0.92), ($Size * 0.65))
    $highlight.AddEllipse($glowRect)
    $glowBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($highlight)
    $glowBrush.CenterPoint = New-Object System.Drawing.PointF(($Size * 0.32), ($Size * 0.22))
    $glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(52, 255, 255, 255)
    $glowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 255, 255, 255))
    $g.SetClip($shape)
    $g.FillPath($glowBrush, $highlight)
    $g.ResetClip()

    # Bolt.
    $bolt = New-Object System.Drawing.Drawing2D.GraphicsPath
    $points = @(
        (New-Object System.Drawing.PointF(($Size * 0.565), ($Size * 0.145))),
        (New-Object System.Drawing.PointF(($Size * 0.255), ($Size * 0.555))),
        (New-Object System.Drawing.PointF(($Size * 0.465), ($Size * 0.555))),
        (New-Object System.Drawing.PointF(($Size * 0.405), ($Size * 0.865))),
        (New-Object System.Drawing.PointF(($Size * 0.755), ($Size * 0.440))),
        (New-Object System.Drawing.PointF(($Size * 0.535), ($Size * 0.440)))
    )
    $bolt.AddPolygon($points)
    $boltBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(250, 255, 255, 255))
    $g.FillPath($boltBrush, $bolt)

    # Faint inner rim.
    $rimPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(40, 255, 255, 255), [Math]::Max(0.7, $Size * 0.006))
    $inner = New-RoundedRectPath -Rect (New-Object System.Drawing.RectangleF(($pad + 1), ($pad + 1), ($Size - $pad * 2 - 2), ($Size - $pad * 2 - 2))) -Radius ($radius - 1)
    $g.DrawPath($rimPen, $inner)

    $g.Dispose()
    return $bmp
}

function Write-Ico {
    param([string]$Path, [int[]]$Sizes)
    $images = @()
    foreach ($size in $Sizes) {
        $bmp = New-IconBitmap -Size $size
        $images += ,@($size, $bmp)
    }

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$images.Count)
    $offset = 6 + (16 * $images.Count)
    foreach ($item in $images) {
        $size = $item[0]
        $bmp = $item[1]
        $w = [byte]($(if ($size -ge 256) { 0 } else { $size }))
        $h = [byte]($(if ($size -ge 256) { 0 } else { $size }))
        $writer.Write($w)
        $writer.Write($h)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32](40 + ($size * $size * 4) + ([Math]::Ceiling($size / 32.0) * 4 * $size)))
        $writer.Write([UInt32]$offset)
        $offset += 40 + ($size * $size * 4) + ([Math]::Ceiling($size / 32.0) * 4 * $size)
    }

    foreach ($item in $images) {
        $size = $item[0]
        $bmp = $item[1]
        $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
        $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $pixels = New-Object byte[] ($size * $size * 4)
        for ($y = 0; $y -lt $size; $y++) {
            [System.Runtime.InteropServices.Marshal]::Copy(
                [IntPtr]::Add($data.Scan0, ($size - 1 - $y) * $data.Stride),
                $pixels,
                ($y * $size * 4),
                ($size * 4)
            )
        }
        $bmp.UnlockBits($data)

        $writer.Write([UInt32]40)
        $writer.Write([Int32]$size)
        $writer.Write([Int32]($size * 2))
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]($size * $size * 4))
        $writer.Write([Int32]0)
        $writer.Write([Int32]0)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]0)
        $writer.Write($pixels)

        $maskRow = [Math]::Ceiling($size / 32.0) * 4
        $mask = New-Object byte[] ($maskRow * $size)
        $writer.Write($mask)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($Path, $stream.ToArray())
    $writer.Dispose()
    $stream.Dispose()
    foreach ($item in $images) { $item[1].Dispose() }
}

Write-Ico -Path (Join-Path $assets "app.ico") -Sizes @(16, 24, 32, 48, 64, 128, 256)

$preview = New-IconBitmap -Size 512
$preview.Save((Join-Path $assets "app-preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()

Write-Host "Icon generated in $assets"
