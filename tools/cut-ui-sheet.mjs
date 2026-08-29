// Режет лист элементов интерфейса на отдельные картинки.
//
// Генератор кладёт детали на один прозрачный лист, а игре нужен каждый
// элемент отдельным файлом: рамка окна тянется девятью кусками, слот
// повторяется сорок раз, кнопка живёт в трёх состояниях. Резать это руками
// в редакторе — полчаса возни и неточные края.
//
// Ищем связные области непрозрачных пикселей: детали на листе не касаются
// друг друга, поэтому каждая область — отдельный элемент.
//
// Запуск:
//   node tools/cut-ui-sheet.mjs <лист.png> <куда> [префикс]

import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const source = process.argv[2];
const outDir = process.argv[3];
const prefix = process.argv[4] || 'ui';

// Порог прозрачности. По умолчанию низкий — берём всё, что хоть немного
// видно. Поднимать нужно для листов, вокруг элементов которых генератор
// нарисовал свечение: оно полупрозрачное, и при низком пороге куски
// уезжают вместе с ореолом.
const threshold = Number(process.argv[5] || 30);

if (!source || !fs.existsSync(source)) {
  console.error('Нет листа: ' + source);
  process.exit(1);
}

fs.mkdirSync(outDir, { recursive: true });

// Разбор PNG отдаём .NET через PowerShell: своего декодера у нас нет, а
// System.Drawing есть в любой Windows.
const script = `
Add-Type -AssemblyName System.Drawing

$img = New-Object System.Drawing.Bitmap('${source.replace(/\\/g, '/')}')
$w = $img.Width
$h = $img.Height

$rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
$data = $img.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes = New-Object byte[] ($data.Stride * $h)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$img.UnlockBits($data)

# Карта занятости с шагом 4 пикселя: для поиска областей точность не нужна,
# а обход миллиона точек по одной занимает минуты.
$step = 4
$threshold = ${threshold}
$cols = [int]($w / $step)
$rows = [int]($h / $step)
$busy = New-Object 'bool[]' ($cols * $rows)

for ($cy = 0; $cy -lt $rows; $cy++) {
  for ($cx = 0; $cx -lt $cols; $cx++) {
    $x = $cx * $step
    $y = $cy * $step
    $a = $bytes[$y * $data.Stride + $x * 4 + 3]
    if ($a -gt $threshold) { $busy[$cy * $cols + $cx] = $true }
  }
}

# Заливка по связным клеткам: каждая область — одна деталь листа.
$seen = New-Object 'bool[]' ($cols * $rows)
$found = @()

for ($cy = 0; $cy -lt $rows; $cy++) {
  for ($cx = 0; $cx -lt $cols; $cx++) {
    $i = $cy * $cols + $cx
    if (-not $busy[$i] -or $seen[$i]) { continue }

    $stack = New-Object System.Collections.Stack
    $stack.Push(@($cx, $cy))
    $seen[$i] = $true

    $minX = $cx; $maxX = $cx; $minY = $cy; $maxY = $cy; $count = 0

    while ($stack.Count -gt 0) {
      $p = $stack.Pop()
      $px = $p[0]; $py = $p[1]
      $count++

      if ($px -lt $minX) { $minX = $px }
      if ($px -gt $maxX) { $maxX = $px }
      if ($py -lt $minY) { $minY = $py }
      if ($py -gt $maxY) { $maxY = $py }

      foreach ($d in @(@(1,0), @(-1,0), @(0,1), @(0,-1), @(1,1), @(-1,-1), @(1,-1), @(-1,1))) {
        $nx = $px + $d[0]; $ny = $py + $d[1]
        if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $cols -or $ny -ge $rows) { continue }

        $ni = $ny * $cols + $nx
        if (-not $busy[$ni] -or $seen[$ni]) { continue }

        $seen[$ni] = $true
        $stack.Push(@($nx, $ny))
      }
    }

    # Мелочь пропускаем: это огрехи генератора, а не детали.
    if ($count -lt 40) { continue }

    # Значения считаем ДО сборки массива: внутри @() запятая
    # разбирается раньше умножения, и PowerShell пытается умножить
    # массив на число.
    $bx = $minX * $step
    $by = $minY * $step
    $bw = ($maxX - $minX + 1) * $step
    $bh = ($maxY - $minY + 1) * $step

    $found += ,@($bx, $by, $bw, $bh)
  }
}

# Сверху вниз, слева направо — в том порядке, в каком их видит человек.
$found = $found | Sort-Object { $_[1] * 10000 + $_[0] }

$n = 0
foreach ($f in $found) {
  $n++
  $x = [Math]::Max(0, $f[0] - 2)
  $y = [Math]::Max(0, $f[1] - 2)
  $cw = [Math]::Min($w - $x, $f[2] + 4)
  $ch = [Math]::Min($h - $y, $f[3] + 4)

  $out = New-Object System.Drawing.Bitmap($cw, $ch)
  $g = [System.Drawing.Graphics]::FromImage($out)
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle(0, 0, $cw, $ch)), (New-Object System.Drawing.Rectangle($x, $y, $cw, $ch)), [System.Drawing.GraphicsUnit]::Pixel)
  $g.Dispose()

  $name = '${outDir.replace(/\\/g, '/')}/${prefix}_' + $n + '.png'
  $out.Save($name, [System.Drawing.Imaging.ImageFormat]::Png)
  $out.Dispose()

  Write-Output ('  ' + $n + ': ' + $cw + 'x' + $ch + ' at ' + $x + ',' + $y)
}

$img.Dispose()
Write-Output ('деталей: ' + $found.Count)
`;

const result = spawnSync('powershell', ['-NoProfile', '-Command', script],
                         { encoding: 'utf8', maxBuffer: 1024 * 1024 * 32 });

console.log((result.stdout || '').trim());
if (result.stderr && result.stderr.trim()) console.error(result.stderr.trim().slice(0, 500));
