# Cursor builder: strips the magenta backdrop, trims to content, scales to size.
# ASCII only in code: PowerShell 5.1 reads non-BOM files as ANSI.
#
# Why magenta and not a transparent request: generators paint the whole raster,
# and alpha only happens when nothing is drawn around the object. Ask for
# atmosphere and you get a vignette instead. Magenta is dragged to the very
# edge reliably, and it is removed here exactly, without cropping the art.

param(
    [Parameter(Mandatory = $true)][string]$In,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$Size = 32,
    [int]$Tolerance = 90
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $In)) { Write-Error "no input: $In"; exit 1 }

$src = [System.Drawing.Bitmap]::FromFile($In)
$w = $src.Width; $h = $src.Height

# Work on a 32bpp copy so the byte layout is known: B G R A per pixel.
$img = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($img)
$g.DrawImage($src, 0, 0, $w, $h)
$g.Dispose(); $src.Dispose()

$rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
$data = $img.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, $img.PixelFormat)
$bytes = New-Object byte[] ($data.Stride * $h)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)

# Sample the four corners: the backdrop colour is whatever sits in them.
$keyR = 0; $keyG = 0; $keyB = 0
foreach ($p in @(@(0,0), @(($w-1),0), @(0,($h-1)), @(($w-1),($h-1)))) {
    $i = $p[1] * $data.Stride + $p[0] * 4
    $keyB += $bytes[$i]; $keyG += $bytes[$i+1]; $keyR += $bytes[$i+2]
}
$keyR = [int]($keyR / 4); $keyG = [int]($keyG / 4); $keyB = [int]($keyB / 4)

$minX = $w; $minY = $h; $maxX = -1; $maxY = -1
$cut = 0

for ($y = 0; $y -lt $h; $y++) {
    $row = $y * $data.Stride
    for ($x = 0; $x -lt $w; $x++) {
        $i = $row + $x * 4
        $db = [int]$bytes[$i] - $keyB
        $dg = [int]$bytes[$i+1] - $keyG
        $dr = [int]$bytes[$i+2] - $keyR
        $dist = [math]::Sqrt($dr*$dr + $dg*$dg + $db*$db)

        if ($dist -lt $Tolerance) {
            # Backdrop. Zero the colour too, or the halo bleeds back when scaled.
            $bytes[$i] = 0; $bytes[$i+1] = 0; $bytes[$i+2] = 0; $bytes[$i+3] = 0
            $cut++
        } else {
            $bytes[$i+3] = 255
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

[System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
$img.UnlockBits($data)

if ($maxX -lt 0) { Write-Error "everything read as backdrop - raise -Tolerance"; exit 1 }

$cw = $maxX - $minX + 1
$ch = $maxY - $minY + 1

# Square canvas so the shape keeps its proportions at any cursor size.
$side = [math]::Max($cw, $ch)
$square = New-Object System.Drawing.Bitmap $side, $side, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sg = [System.Drawing.Graphics]::FromImage($square)
$sg.Clear([System.Drawing.Color]::Transparent)
$sg.DrawImage($img, [int](($side - $cw) / 2), [int](($side - $ch) / 2),
              (New-Object System.Drawing.Rectangle $minX, $minY, $cw, $ch),
              [System.Drawing.GraphicsUnit]::Pixel)
$sg.Dispose()

$final = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$fg = [System.Drawing.Graphics]::FromImage($final)
$fg.Clear([System.Drawing.Color]::Transparent)
$fg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$fg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$fg.DrawImage($square, 0, 0, $Size, $Size)
$fg.Dispose()

$dir = Split-Path $Out -Parent
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$final.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)

"key rgb $keyR,$keyG,$keyB | backdrop pixels $cut of $($w*$h)"
"content $cw x $ch at $minX,$minY -> square $side -> saved $Size x $Size"
"$Out"

$final.Dispose(); $square.Dispose(); $img.Dispose()
