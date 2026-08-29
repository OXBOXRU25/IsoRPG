# Обрезает прозрачные поля вокруг картинки.
#
# Генератор оставляет вокруг элемента пустое поле — иногда в треть высоты.
# В игре картинка растягивается целиком, вместе с этой пустотой: рамка
# занимает середину, а по краям остаётся воздух, и панель выглядит узкой
# полоской. Заодно любые доли, снятые с такой картинки, оказываются
# смещёнными относительно того, что видно на экране.
param([Parameter(Mandatory=$true)][string]$File)

Add-Type -AssemblyName System.Drawing

$img = New-Object System.Drawing.Bitmap($File)
$w = $img.Width
$h = $img.Height

$rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
$data = $img.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                      [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

$bytes = New-Object byte[] ($data.Stride * $h)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$img.UnlockBits($data)

$left = $w; $right = -1; $top = $h; $bottom = -1

for ($y = 0; $y -lt $h; $y++) {
  for ($x = 0; $x -lt $w; $x++) {
    if ($bytes[$y * $data.Stride + $x * 4 + 3] -lt 10) { continue }

    if ($x -lt $left) { $left = $x }
    if ($x -gt $right) { $right = $x }
    if ($y -lt $top) { $top = $y }
    if ($y -gt $bottom) { $bottom = $y }
  }
}

if ($right -lt 0) { "пусто: $File"; $img.Dispose(); exit }

$cw = $right - $left + 1
$ch = $bottom - $top + 1

if ($cw -eq $w -and $ch -eq $h) {
  "$([System.IO.Path]::GetFileName($File)): полей нет, $w x $h"
  $img.Dispose()
  exit
}

$out = New-Object System.Drawing.Bitmap($cw, $ch)
$g = [System.Drawing.Graphics]::FromImage($out)
$g.DrawImage($img, (New-Object System.Drawing.Rectangle(0, 0, $cw, $ch)),
             (New-Object System.Drawing.Rectangle($left, $top, $cw, $ch)),
             [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$img.Dispose()

$temp = "$File.trim"
$out.Save($temp, [System.Drawing.Imaging.ImageFormat]::Png)
$out.Dispose()

Move-Item $temp $File -Force

"$([System.IO.Path]::GetFileName($File)): было $w x $h → стало $cw x $ch"
