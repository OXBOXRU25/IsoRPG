# Склейка PNG-последовательности из Unity в видео для ютуба.
# Отдельно от съёмки: кадры снимаются редактором, видео собирается здесь.

param(
    [string]$Frames = "D:\GAME Ai\Youtube\Frames",
    [string]$Out    = "D:\GAME Ai\Youtube\oblet.mp4",
    [int]$Fps       = 30,
    [switch]$Vertical          # 9:16 под шортсы: кроп по центру, не сжатие
)

$FF = "D:\AI\Artdom\_tools\node_modules\ffmpeg-static\ffmpeg.exe"

$count = (Get-ChildItem $Frames -Filter "kadr_*.png" -ErrorAction SilentlyContinue).Count
if ($count -eq 0) { Write-Host "  Кадров нет в $Frames" -ForegroundColor Red; exit 1 }
Write-Host "  Кадров: $count, это $([math]::Round($count/$Fps,1)) сек при $Fps к/с" -ForegroundColor Cyan

# yuv420p обязателен: без него ютуб и половина плееров видео не покажут.
$args = @("-y", "-framerate", $Fps, "-i", "$Frames\kadr_%04d.png")

if ($Vertical) {
    # Кроп 9:16 из центра кадра. Сжимать 16:9 в 9:16 нельзя - лица и деревья
    # растянет; берём вертикальную полосу из середины.
    $args += @("-vf", "crop=ih*9/16:ih,scale=1080:1920")
    $Out = $Out -replace "\.mp4$", "-vert.mp4"
} else {
    $args += @("-vf", "scale=1920:1080")
}

$args += @("-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", $Out)

& $FF @args 2>&1 | Select-String "frame=" | Select-Object -Last 1

if (Test-Path $Out) {
    $sz = (Get-Item $Out).Length / 1MB
    Write-Host ""
    Write-Host "  ГОТОВО: $Out" -ForegroundColor Green
    Write-Host ("  Размер: {0:N1} МБ" -f $sz)
    & $FF -i $Out 2>&1 | Select-String "Duration|Stream #0:0" | ForEach-Object { "  " + "$_".Trim() }
} else {
    Write-Host "  Видео не собралось" -ForegroundColor Red
}
