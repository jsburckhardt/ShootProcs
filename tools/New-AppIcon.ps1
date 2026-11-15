$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $root 'src\ShootProcs.App\Assets\ShootProcs.ico'
[System.IO.Directory]::CreateDirectory((Split-Path $destination)) | Out-Null
$images = @()
foreach ($size in @(256, 64, 48, 32, 16)) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml('#101925'))
    $pen = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#6BF4C5'), [single]($size * .055))
    $graphics.DrawEllipse($pen, [single]($size * .23), [single]($size * .23), [single]($size * .54), [single]($size * .54))
    foreach ($line in @(@(.5, .10, .5, .34), @(.5, .66, .5, .90), @(.10, .5, .34, .5), @(.66, .5, .90, .5))) {
        $graphics.DrawLine($pen, [single]($size * $line[0]), [single]($size * $line[1]), [single]($size * $line[2]), [single]($size * $line[3]))
    }
    $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#6BF4C5'))
    $graphics.FillEllipse($brush, [single]($size * .43), [single]($size * .43), [single]($size * .14), [single]($size * .14))
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += [pscustomobject]@{ Size = $size; Data = $stream.ToArray() }
    $stream.Dispose(); $brush.Dispose(); $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$file = [System.IO.File]::Create($destination)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($image in $images) {
        $dimension = if ($image.Size -eq 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Data.Length); $writer.Write([uint32]$offset)
        $offset += $image.Data.Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image.Data) }
}
finally { $writer.Dispose(); $file.Dispose() }
Write-Output $destination
