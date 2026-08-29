# Снимок окна игры БЕЗ вывода его на передний план.
#
# Обычный CopyFromScreen снимает экран целиком — и если поверх игры лежит
# чат или мессенджер, в кадр попадают они, а не игра. PrintWindow рисует
# окно через композитор Windows, поэтому чужие окна сверху не мешают.
#
# Флаг PW_RENDERFULLCONTENT (2) обязателен: без него окна на DirectX
# (а Unity именно такое) отдают чёрный прямоугольник.
#
# Использование:
#   powershell -File tools/shot-window.ps1 -Process AdventuresOfZhenya -Out C:\путь\shot.png

param(
    [string]$Process = "AdventuresOfZhenya",
    [string]$Out = "$env:TEMP\window-shot.png",
    [double]$Scale = 0.65
)

Add-Type -AssemblyName System.Drawing

Add-Type -Namespace Shot -Name Api -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
'@

$p = Get-Process $Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($null -eq $p) { Write-Output "НЕТ ОКНА: процесс '$Process' не найден или у него нет окна"; exit 1 }

$rect = New-Object Shot.Api+RECT
[Shot.Api]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
$w = $rect.R - $rect.L
$h = $rect.B - $rect.T
if ($w -le 0 -or $h -le 0) { Write-Output "ОКНО СВЁРНУТО В НОЛЬ: $w x $h"; exit 1 }

$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$dc = $g.GetHdc()
$ok = [Shot.Api]::PrintWindow($p.MainWindowHandle, $dc, 2)
$g.ReleaseHdc($dc)
$g.Dispose()

# Проверка на чёрный кадр: PrintWindow иногда отрабатывает «успешно»,
# отдавая пустоту. Молчаливый чёрный снимок хуже честной ошибки.
$probe = 0
for ($i = 1; $i -lt 12; $i++) {
    $px = $bmp.GetPixel([int]($w * $i / 12), [int]($h / 2))
    $probe += $px.R + $px.G + $px.B
}

$small = New-Object System.Drawing.Bitmap $bmp, ([System.Drawing.Size]::new([int]($w * $Scale), [int]($h * $Scale)))
$small.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); $small.Dispose()

if ($probe -lt 60) {
    Write-Output "ЧЁРНЫЙ КАДР (сумма по 11 точкам $probe): окно не отдалось композитору. Снято в $Out, но смотреть нечего — понадобится поднять окно."
} else {
    Write-Output "снято $w x $h -> $Out (PrintWindow=$ok, яркость $probe)"
}
