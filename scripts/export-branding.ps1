$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repoPath = Split-Path -Parent $PSScriptRoot
$brandPath = Join-Path $repoPath 'assets/branding'
$master = [System.Drawing.Bitmap]::new((Join-Path $brandPath 'tailviewer-master.png'))
try {
    if ($master.Width -ne $master.Height -or $master.GetPixel(0, 0).A -ne 0) {
        throw 'The master must be square with a transparent background.'
    }
    $frames = @{}
    foreach ($size in @(16, 20, 24, 32, 40, 48, 64, 128, 180, 192, 256, 512, 1024)) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [System.IO.MemoryStream]::new()
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($master, [System.Drawing.Rectangle]::new(0, 0, $size, $size))
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames[$size] = $stream.ToArray()
            [System.IO.File]::WriteAllBytes((Join-Path $brandPath "icon-$size.png"), $frames[$size])
            if ($bitmap.Width -ne $size -or $bitmap.GetPixel(0, 0).A -ne 0) {
                throw "Invalid $size px export."
            }
        }
        finally {
            $stream.Dispose()
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }

    $iconSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $iconPath = Join-Path $brandPath 'tailviewer.ico'
    $writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create($iconPath))
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$iconSizes.Count)
        $offset = 6 + 16 * $iconSizes.Count
        foreach ($size in $iconSizes) {
            $dimension = if ($size -eq 256) { 0 } else { $size }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$size].Length)
            $writer.Write([uint32]$offset)
            $offset += $frames[$size].Length
        }
        foreach ($size in $iconSizes) { $writer.Write([byte[]]$frames[$size]) }
    }
    finally { $writer.Dispose() }

    # Keep existing WPF, installer and NuGet paths so every current consumer receives the same mark.
    Copy-Item -LiteralPath $iconPath -Destination (Join-Path $repoPath 'src/Tailviewer/Resources/Tailviewer.ico')
    Copy-Item -LiteralPath (Join-Path $brandPath 'icon-256.png') -Destination (Join-Path $repoPath 'src/Tailviewer/Resources/Tailviewer.png')
    $appAssets = Join-Path $repoPath 'src/Tailviewer.Avalonia/Assets'
    New-Item -ItemType Directory -Path $appAssets -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $brandPath 'icon-64.png') -Destination (Join-Path $appAssets 'Tailviewer.png')
    Write-Output 'Exported 13 transparent PNG sizes and a nine-size Windows ICO from one master.'
}
finally { $master.Dispose() }
