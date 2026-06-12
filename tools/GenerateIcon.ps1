Add-Type -AssemblyName System.Drawing

$outputPath = Join-Path $PSScriptRoot "..\BrowserBlocker\Assets\BrowserBlock.ico"
$outputDirectory = Split-Path $outputPath
if (!(Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()

foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(24, 26, 31))

    $scale = $size / 64.0
    $inset = [Math]::Max(2, [Math]::Round(10 * $scale))
    $outlineWidth = [Math]::Max(1.5, 3 * $scale)
    $titleWidth = [Math]::Max(2.5, 5 * $scale)
    $slashWidth = [Math]::Max(2.5, 6 * $scale)

    $whitePen = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::White,
        [single]$outlineWidth)
    $whitePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $titlePen = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::White,
        [single]$titleWidth)
    $titlePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $titlePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $redPen = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::FromArgb(232, 63, 70),
        [single]$slashWidth)
    $redPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $redPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $box = New-Object System.Drawing.RectangleF(
        [single]$inset,
        [single]$inset,
        [single]($size - (2 * $inset)),
        [single]($size - (2 * $inset)))
    $graphics.DrawRectangle(
        $whitePen,
        $box.X,
        $box.Y,
        $box.Width,
        $box.Height)
    $titleY = [single]($inset + (12 * $scale))
    $graphics.DrawLine(
        $titlePen,
        [single]($inset + (2 * $scale)),
        $titleY,
        [single]($size - $inset - (2 * $scale)),
        $titleY)
    $graphics.DrawLine(
        $redPen,
        [single](15 * $scale),
        [single](49 * $scale),
        [single](49 * $scale),
        [single](15 * $scale))

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,$stream.ToArray()

    $redPen.Dispose()
    $titlePen.Dispose()
    $whitePen.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
    $stream.Dispose()
}

$file = [System.IO.File]::Open(
    $outputPath,
    [System.IO.FileMode]::Create,
    [System.IO.FileAccess]::Write)
$writer = New-Object System.IO.BinaryWriter($file)

$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$images.Count)

$offset = 6 + (16 * $images.Count)
for ($index = 0; $index -lt $images.Count; $index++) {
    $size = $sizes[$index]
    $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
    $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$index].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$index].Length
}

foreach ($image in $images) {
    $writer.Write($image)
}

$writer.Dispose()
$file.Dispose()
