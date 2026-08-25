// Нарезает дорожку на отдельные звуки и выравнивает громкость.
//
// Дорожки со стоков — это записи целиком: девять секунд ходьбы, три секунды
// вокруг одной реплики. Игре нужен один шаг на одно движение ноги и одна
// реплика на одно приветствие, иначе звук начинает жить своей жизнью:
// персонаж шагнул, а в ушах ещё четыре секунды чужих шагов.
//
// Громкость выравниваем здесь, а не в игре. В коде громкость — это роль
// звука («шаги тише ударов»), и если файлы записаны с разным уровнем, роль
// перестаёт работать: женский голос в нашем наборе тише мужского втрое.
//
// Запуск:
//   node tools/cut-audio.mjs

import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const FFMPEG = 'D:/AI/Reference/node_modules/ffmpeg-static/ffmpeg.exe';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..');
const AUDIO = path.join(repo, 'IsoRPG', 'Assets', '_Game', 'Audio');

/**
 * Вырезает кусок и приводит громкость к общему уровню.
 *
 * loudnorm, а не простое усиление: он считает воспринимаемую громкость, а
 * не пик. Два звука с одинаковым пиком могут отличаться на слух вдвое, и
 * выравнивать по пику — значит выравнивать не то, что слышит человек.
 */
function cut(source, target, from, duration, loudness) {
  const args = [
    '-y', '-v', 'error',
    '-ss', String(from),
    '-t', String(duration),
    '-i', source,
    '-af', 'loudnorm=I=' + loudness + ':TP=-1.5:LRA=11,afade=t=in:st=0:d=0.01,' +
           'afade=t=out:st=' + Math.max(0, duration - 0.05).toFixed(3) + ':d=0.05',
    '-ac', '1',
    '-ar', '44100',
    '-c:a', 'libvorbis', '-q:a', '4',
    target,
  ];

  const result = spawnSync(FFMPEG, args, { encoding: 'utf8' });

  if (result.status !== 0) {
    console.log('  НЕ ВЫШЛО ' + path.basename(target) + ': ' +
                (result.stderr || '').trim().slice(0, 200));
    return false;
  }

  console.log('  ' + path.basename(target) + '  ' +
              (fs.statSync(target).size / 1024).toFixed(0) + ' КБ');
  return true;
}

// --- Шаги ------------------------------------------------------------------
//
// Шесть штук из семнадцати: берём те, что звучат чисто и не наложены на
// соседние. Больше не нужно — ухо перестаёт различать повторы уже на пяти,
// а лишние файлы это лишний вес сборки.

const stepsSource = path.join(AUDIO, 'Steps', 'footsteps_snow.mp3');

const steps = [
  [4.36, 0.30], [5.18, 0.30], [6.16, 0.28],
  [6.53, 0.30], [7.45, 0.28], [8.29, 0.32],
];

if (fs.existsSync(stepsSource)) {
  console.log('Шаги:');

  steps.forEach(([from, length], i) => {
    const name = 'footstep_snow_' + String(i).padStart(3, '0') + '.ogg';
    cut(stepsSource, path.join(AUDIO, 'Steps', name), from, length, -20);
  });

  // Исходник в проекте не нужен: девять секунд чужой ходьбы попадут в
  // сборку и будут весить больше, чем все шесть шагов вместе.
  fs.unlinkSync(stepsSource);
  console.log('  исходную дорожку убрал');
}

// --- Голоса NPC ------------------------------------------------------------
//
// Тише остального намеренно: приветствие звучит при каждом подходе к
// торговцу, и на общей громкости оно надоедает за десять минут.

const voices = [
  ['npc_man.mp3', 'npc_man.ogg', 0.03, 1.0],
  ['npc_woman.mp3', 'npc_woman.ogg', 0.05, 0.75],
];

console.log('Голоса:');

for (const [from, to, start, length] of voices) {
  const source = path.join(AUDIO, 'Voices', from);
  if (!fs.existsSync(source)) { console.log('  нет ' + from); continue; }

  if (cut(source, path.join(AUDIO, 'Voices', to), start, length, -22)) {
    fs.unlinkSync(source);
  }
}

// --- Фон -------------------------------------------------------------------
//
// Птиц не режем: две минуты — это как раз хорошая петля, короткий фон
// выдаёт себя повторами. Только приводим громкость, чтобы «Окружение»
// на половине ползунка означало то же, что у остальных звуков.

const birds = path.join(AUDIO, 'Ambience', 'forest_birds.mp3');

if (fs.existsSync(birds)) {
  console.log('Фон:');

  const target = path.join(AUDIO, 'Ambience', 'forest_birds.ogg');

  const result = spawnSync(FFMPEG,
    ['-y', '-v', 'error', '-i', birds,
     '-af', 'loudnorm=I=-24:TP=-2:LRA=11',
     '-ac', '1', '-ar', '44100', '-c:a', 'libvorbis', '-q:a', '3', target],
    { encoding: 'utf8' });

  if (result.status === 0) {
    console.log('  forest_birds.ogg  ' +
                (fs.statSync(target).size / 1024 / 1024).toFixed(1) + ' МБ');
    fs.unlinkSync(birds);
  } else {
    console.log('  НЕ ВЫШЛО: ' + (result.stderr || '').trim().slice(0, 200));
  }
}
