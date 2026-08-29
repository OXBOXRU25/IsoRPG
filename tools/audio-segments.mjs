// Показывает, из чего состоит звуковой файл: где звук, где тишина.
//
// Нужно, чтобы не резать вслепую. Дорожка шагов с фристаунда — это обычно
// серия ударов подряд, а игре нужен один шаг на одно движение ноги; голос
// NPC — несколько реплик в одном файле, и играть их всегда целиком значит
// слышать одно и то же.
//
// Запуск:
//   node tools/audio-segments.mjs файл.mp3 [порог]

import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const FFMPEG = 'D:/AI/Reference/node_modules/ffmpeg-static/ffmpeg.exe';
const RATE = 22050;

const file = process.argv[2];
const threshold = Number(process.argv[3] || 0.035);

if (!file || !fs.existsSync(file)) {
  console.error('Нет файла: ' + file);
  process.exit(1);
}

// Декодируем в сырые числа: разбирать mp3 самим незачем, а ffmpeg уже есть.
const raw = spawnSync(FFMPEG,
  ['-v', 'error', '-i', file, '-ac', '1', '-ar', String(RATE),
   '-f', 'f32le', '-'],
  { maxBuffer: 1024 * 1024 * 256, encoding: 'buffer' });

if (raw.status !== 0) {
  console.error('ffmpeg не смог: ' + raw.stderr.toString().slice(0, 300));
  process.exit(1);
}

const samples = new Float32Array(raw.stdout.buffer, raw.stdout.byteOffset,
                                Math.floor(raw.stdout.length / 4));

// Громкость считаем окнами по 10 мс: по отдельным отсчётам не видно ничего,
// звук — это всегда колебание вокруг нуля.
const window = Math.round(RATE * 0.01);
const levels = [];

for (let i = 0; i + window <= samples.length; i += window) {
  let sum = 0;
  for (let k = 0; k < window; k++) sum += samples[i + k] * samples[i + k];

  levels.push(Math.sqrt(sum / window));
}

const peak = Math.max(...levels);

// Ищем куски громче порога. Короткие провалы внутри звука не считаем концом:
// у шага есть хвост, и рвать по нему — значит получить обрубок.
const gapAllowed = 12;     // 120 мс
const minLength = 4;       // 40 мс

const segments = [];
let start = -1;
let quiet = 0;

for (let i = 0; i < levels.length; i++) {
  const loud = levels[i] > threshold;

  if (loud) {
    if (start < 0) start = i;
    quiet = 0;
    continue;
  }

  if (start < 0) continue;

  quiet++;
  if (quiet < gapAllowed) continue;

  if (i - quiet - start >= minLength) segments.push([start, i - quiet]);
  start = -1;
  quiet = 0;
}

if (start >= 0 && levels.length - start >= minLength) {
  segments.push([start, levels.length - 1]);
}

console.log(path.basename(file));
console.log('  длительность: ' + (samples.length / RATE).toFixed(2) + ' с' +
            ', пик: ' + peak.toFixed(3) + ', порог: ' + threshold);
console.log('  кусков: ' + segments.length);

segments.forEach(([from, to], i) => {
  const a = from / 100;
  const b = (to + 1) / 100;

  let loudest = 0;
  for (let k = from; k <= to; k++) loudest = Math.max(loudest, levels[k]);

  console.log('   ' + String(i + 1).padStart(2) + '. ' +
              a.toFixed(2) + ' — ' + b.toFixed(2) + ' с' +
              '  (' + (b - a).toFixed(2) + ' с, громкость ' + loudest.toFixed(3) + ')');
});
