$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $PSScriptRoot '..\Assets'
$output = [IO.File]::Create((Join-Path $assets 'FoolsFavour.ico'))
$writer = [IO.BinaryWriter]::new($output)
$images = [Collections.Generic.List[byte[]]]::new()
$sizes = @(16, 24, 32, 48, 64, 128, 256)
function Points([single[]] $coordinates) {
    for ($i = 0; $i -lt $coordinates.Length; $i += 2) {
        [Drawing.PointF]::new($coordinates[$i], $coordinates[$i + 1])
    }
}
try {
    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size, $size)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $dark = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(24, 28, 24))
        $green = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(61, 76, 46))
        $gold = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(204, 172, 101))
        $steel = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(225, 228, 213))
        $shadow = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(134, 151, 139))
        $outline = [Drawing.Pen]::new($gold.Color, 8)
        try {
            $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.ScaleTransform($size / 256.0, $size / 256.0)
            $shield = [Drawing.PointF[]](Points @(30, 20, 226, 20, 226, 126, 212, 178, 176, 214, 128, 242, 80, 214, 44, 178, 30, 126))
            $graphics.FillPolygon($dark, $shield)
            $graphics.FillPolygon($green, [Drawing.PointF[]](Points @(128, 26, 220, 26, 220, 126, 206, 174, 172, 208, 128, 234)))
            $graphics.DrawPolygon($outline, $shield)
            # A broken blade and a fool's two-point crown distinguish it from the game's icon.
            $graphics.FillPolygon($steel, [Drawing.PointF[]](Points @(109, 154, 109, 70, 124, 81, 135, 58, 147, 70, 147, 154)))
            $graphics.FillPolygon($shadow, [Drawing.PointF[]](Points @(128, 154, 128, 75, 135, 58, 147, 70, 147, 154)))
            $graphics.FillPolygon($gold, [Drawing.PointF[]](Points @(81, 161, 85, 145, 128, 153, 171, 145, 175, 161, 137, 171, 137, 202, 119, 202, 119, 171)))
            $graphics.FillEllipse($gold, 116, 199, 24, 18)
            $graphics.FillPolygon($gold, [Drawing.PointF[]](Points @(64, 47, 86, 66, 94, 41, 101, 82, 72, 82)))
            $graphics.FillEllipse($gold, 59, 39, 12, 12)
            $graphics.FillEllipse($gold, 89, 33, 12, 12)
            $stream = [IO.MemoryStream]::new()
            try {
                $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
                $images.Add($stream.ToArray())
                if ($size -eq 256) { $bitmap.Save((Join-Path $assets 'FoolsFavour.png'), [Drawing.Imaging.ImageFormat]::Png) }
            }
            finally { $stream.Dispose() }
        }
        finally {
            $outline.Dispose()
            $dark.Dispose(); $green.Dispose(); $gold.Dispose(); $steel.Dispose(); $shadow.Dispose()
            $graphics.Dispose(); $bitmap.Dispose()
        }
    }
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) { $writer.Write($image) }
}
finally { $writer.Dispose(); $output.Dispose() }
