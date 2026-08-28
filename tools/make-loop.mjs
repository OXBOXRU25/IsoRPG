// Сборка бесшовной петли из нового ролика.
//
// Два шага, и порядок важен.
//
// 1. Обрезка по найденной паре кадров. Прошлая попытка перетекания провалилась
//    (1.72 против 1.60) именно потому, что шла первой: смешивать две разные
//    фазы движения — значит показать обе сразу. Сначала надо найти кадры,
//    которые и так похожи.
//
// 2. Перетекание поверх найденной пары. Ролик берётся длиннее петли на
//    величину перехода, и этот излишек подмешивается в начало. Тогда стык
//    приходится на два СОСЕДНИХ кадра исходника, то есть шва нет по
//    построению, а качество определяется тем, насколько похожи начало и
//    конец — а их мы и подбирали.
//
// Пара ищется не по одному кадру, а по всему окну перехода: перетекание
// смешивает дюжину кадров, и совпасть должны все.

import { execFileSync } from 'node:child_process';
import fs from 'node:fs';

const FF = 'D:/AI/Reference/node_modules/ffmpeg-static/ffmpeg.exe';
const SRC = 'D:/GAME Ai/hf_20260825_131039_a81a7966-f27c-423f-8ea6-ebb4c5b5e36e.mp4';
const OUT = 'C:/Temp/claude/D--GAME-Ai/571594e5-e664-4794-9101-be6606ca4d5c/scratchpad';

const W = 96, H = 54, SIZE = W * H;
const FPS = 24;
const FADE = 12;              // полсекунды перетекания
const FADE_SEC = FADE / FPS;
const MIN_LEN = 8 * FPS;

function grey(file) {
  const raw = execFileSync(FF, [
    '-v', 'error', '-i', file,
    '-vf', `scale=${W}:${H}`,
    '-pix_fmt', 'gray', '-f', 'rawvideo', '-',
  ], { maxBuffer: 1 << 30 });

  const n = Math.floor(raw.length / SIZE);
  const out = [];
  for (let i = 0; i < n; i++) out.push(raw.subarray(i * SIZE, (i + 1) * SIZE));
  return out;
}

function diff(a, b) {
  let s = 0;
  for (let i = 0; i < SIZE; i++) s += Math.abs(a[i] - b[i]);
  return s / SIZE;
}

const frames = grey(SRC);
const count = frames.length;

// Расхождение по всему окну перетекания, а не по одному кадру.
function windowDiff(s, e) {
  let sum = 0;
  for (let k = 0; k < FADE; k++) sum += diff(frames[s + k], frames[e + k]);
  return sum / FADE;
}

let best = null;
for (let s = 0; s < count - MIN_LEN - FADE; s++) {
  for (let e = s + MIN_LEN; e + FADE < count; e++) {
    const d = windowDiff(s, e);
    if (best == null || d < best.d) best = { s, e, d };
  }
}

const startSec = best.s / FPS;
const loopLen = (best.e - best.s) / FPS;
const takeLen = loopLen + FADE_SEC;

console.log(`петля: с ${startSec.toFixed(3)} с, длина ${loopLen.toFixed(3)} с`);
console.log(`окно перетекания расходится на ${best.d.toFixed(2)} (пол ~0.19)`);

// Вес головы: 0 в начале перехода, 1 после него.
const w = `min(1,T/${FADE_SEC.toFixed(4)})`;

const filter =
  `[0:v]format=yuv420p,split[a][b];` +
  `[a]trim=0:${loopLen.toFixed(4)},setpts=PTS-STARTPTS[body];` +
  `[b]trim=${loopLen.toFixed(4)}:${takeLen.toFixed(4)},setpts=PTS-STARTPTS[tail];` +
  `[body][tail]blend=all_expr='A*${w}+B*(1-${w})'[out]`;

execFileSync(FF, [
  '-y', '-v', 'error',
  '-ss', startSec.toFixed(4), '-t', takeLen.toFixed(4), '-i', SRC,
  '-filter_complex', filter, '-map', '[out]',
  '-c:v', 'libx264', '-profile:v', 'high', '-pix_fmt', 'yuv420p',
  '-crf', '20', '-preset', 'slow', '-g', '48',
  '-movflags', '+faststart', '-an',
  `${OUT}/loop-hq.mp4`,
], { stdio: 'inherit' });

// Проверка утверждением: стык готового файла, а не намерение скрипта.
const made = grey(`${OUT}/loop-hq.mp4`);

console.log('');
console.log(`готово: ${made.length} кадров (${(made.length / FPS).toFixed(2)} с), ` +
            `${(fs.statSync(`${OUT}/loop-hq.mp4`).size / 1048576).toFixed(1)} МБ`);
console.log(`стык ${diff(made[0], made[made.length - 1]).toFixed(2)}, ` +
            `шум соседних кадров ${diff(made[1], made[2]).toFixed(2)}`);
