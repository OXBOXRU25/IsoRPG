# Builds launcher.ico from the game logo.
#
# Why a script and not a drawing: the logo is a wide banner, and an icon must
# be square. Cropping it by hand once means redoing it by hand every time the
# logo changes. Here the crop is a number that can be adjusted.
#
# The ICO is assembled by hand from PNG frames. Icon.Save on a Bitmap handle
# produces a single 32x32 frame, which looks like mush on the taskbar of a
# modern screen; a real ICO carries several sizes and Windows picks one.
#
# ASCII only on purpose: PowerShell 5.1 reads non-BOM files as ANSI.

param(
    [string]$Source = "D:/GAME Ai/IsoRPG/Assets/_Game/Art/UI/Logo.png",
    [string]$Out    = "D:/GAME Ai/Launcher/assets/launcher.ico",

    # The crest with the eagle sits in the middle of the banner. These are
    # fractions of the source, so they survive the logo being re-exported at
    # another resolution.
    [double]$CentreX = 0.5,
    [double]$CentreY = 0.58,
    [double]$Span    = 0.56
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $Source)) {
    Write-Error "No logo at $Source"
    exit 1
}

$outDir = Split-Path $Out -Parent
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$logo = [System.Drawing.Image]::FromFile($Source)

try {
    $side = [int]([Math]::Min($logo.Width, $logo.Height) * $Span)
    $left = [int]($logo.Width * $CentreX - $side / 2)
    $top  = [int]($logo.Height * $CentreY - $side / 2)

    # Keep the crop inside the picture: a rectangle sticking out gives an
    # exception rather than a clamped result.
    if ($left -lt 0) { $left = 0 }
    if ($top -lt 0) { $top = 0 }
    if ($left + $side -gt $logo.Width) { $left = $logo.Width - $side }
    if ($top + $side -gt $logo.Height) { $top = $logo.Height - $side }

    $crop = New-Object System.Drawing.Bitmap($side, $side)
    $g = [System.Drawing.Graphics]::FromImage($crop)
    $g.DrawImage($logo, (New-Object System.Drawing.Rectangle(0, 0, $side, $side)),
                 (New-Object System.Drawing.Rectangle($left, $top, $side, $side)),
                 [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()

    $sizes = @(256, 128, 64, 48, 32, 16)
    $frames = @()

    foreach ($size in $sizes) {
        $frame = New-Object System.Drawing.Bitmap($size, $size)
        $gg = [System.Drawing.Graphics]::FromImage($frame)

        $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $gg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $gg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $gg.DrawImage($crop, 0, 0, $size, $size)
        $gg.Dispose()

        $stream = New-Object System.IO.MemoryStream
        $frame.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)

        $frames += ,@($size, $stream.ToArray())

        $frame.Dispose()
        $stream.Dispose()
    }

    $crop.Dispose()

    # ICO layout: a 6-byte header, then a 16-byte record per frame, then the
    # image data. Frames are stored as PNG, which Windows Vista and newer read
    # directly - that is what keeps a 256px frame from bloating the file.
    $file = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($file)

    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$frames.Count)

    $offset = 6 + 16 * $frames.Count

    foreach ($frame in $frames) {
        $size = $frame[0]
        $data = $frame[1]

        # 256 is written as zero: the field is one byte and cannot hold 256.
        # Spelled out rather than inline - PowerShell 5.1 has no ternary, and
        # an "if" is a statement here, not an expression.
        $stored = $size
        if ($size -ge 256) { $stored = 0 }

        $writer.Write([Byte]$stored)
        $writer.Write([Byte]$stored)
        $writer.Write([Byte]0)
        $writer.Write([Byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$data.Length)
        $writer.Write([UInt32]$offset)

        $offset += $data.Length
    }

    foreach ($frame in $frames) { $writer.Write($frame[1]) }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($Out, $file.ToArray())

    $writer.Dispose()
    $file.Dispose()

    $kb = [Math]::Round((Get-Item $Out).Length / 1KB, 1)
    Write-Output "ok $Out  frames: $($sizes -join ', ')  size: $kb KB"
}
finally {
    $logo.Dispose()
}
