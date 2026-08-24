// Собирает то, что уезжает игроку: лаунчер, игра и всё, что им нужно рядом.
//
// Раскладка пакета:
//
//   Приключения разбойника Жени/
//     Приключения разбойника Жени.exe   лаунчер, его и запускают
//     launcher.json                     адреса обновлений
//     assets/                           логотип, фон, история версий
//     Game/                             сама игра
//
// Игра лежит в подпапке намеренно. Рядом с её exe находится десяток
// служебных файлов Unity, и человек, открывший корень, должен видеть одну
// программу, а не выбирать из списка, какую из них запускать.
//
// Запуск:
//   node tools/make-package.mjs          собрать папку и zip
//   node tools/make-package.mjs --sfx    плюс самораспаковывающийся exe

import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const NL = String.fromCharCode(10);

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..');

const GAME_BUILD = path.join(repo, 'Build', 'HighFlyingBird');
const LAUNCHER_DIST = path.join(repo, 'Launcher', 'dist');
const PACKAGE_ROOT = path.join(repo, 'Package');
const NAME = 'Приключения разбойника Жени';

const SEVEN_ZIP = 'C:/Program Files/7-Zip/7z.exe';

// --- Версия ----------------------------------------------------------------

function readVersion() {
  const changelog = path.join(repo, 'CHANGELOG.md');
  if (!fs.existsSync(changelog)) return '0.0.0';

  const match = fs.readFileSync(changelog, 'utf8')
    .match(/^##\s+(\d+\.\d+\.\d+)/m);

  return match ? match[1] : '0.0.0';
}

// --- Копирование -----------------------------------------------------------

function copyTree(from, to) {
  fs.mkdirSync(to, { recursive: true });

  for (const entry of fs.readdirSync(from, { withFileTypes: true })) {
    const source = path.join(from, entry.name);
    const target = path.join(to, entry.name);

    if (entry.isDirectory()) copyTree(source, target);
    else fs.copyFileSync(source, target);
  }
}

function folderSize(folder) {
  let total = 0;

  for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
    const full = path.join(folder, entry.name);
    total += entry.isDirectory() ? folderSize(full) : fs.statSync(full).size;
  }

  return total;
}

// --- Сборка пакета ---------------------------------------------------------

const version = readVersion();

if (!fs.existsSync(GAME_BUILD)) {
  console.error('Нет сборки игры: ' + GAME_BUILD);
  console.error('Сначала в Unity: Tools -> IsoRPG -> Собрать игру (Windows).');
  process.exit(1);
}

if (!fs.existsSync(LAUNCHER_DIST)) {
  console.error('Нет сборки лаунчера. Сначала: node Launcher/build.mjs');
  process.exit(1);
}

const target = path.join(PACKAGE_ROOT, NAME);

// Чистим прошлый пакет целиком: остатки старой сборки в новой папке — это
// файлы, которых нет ни в одной версии, и ловить их потом негде.
if (fs.existsSync(target)) fs.rmSync(target, { recursive: true, force: true });

console.log('Собираю пакет версии ' + version + '...');

copyTree(LAUNCHER_DIST, target);
copyTree(GAME_BUILD, path.join(target, 'Game'));

// Версию игры лаунчер читает из version.json. Если сборка игры старая и файла
// в ней нет, говорим об этом вслух: иначе лаунчер молча покажет «не найдена».
const versionFile = path.join(target, 'Game', 'version.json');

if (!fs.existsSync(versionFile)) {
  console.log('  ВНИМАНИЕ: в сборке игры нет version.json — она собрана до того,');
  console.log('  как версии появились. Лаунчер покажет версию игры как неизвестную.');
  console.log('  Лечится пересборкой игры в Unity.');
}

const megabytes = (folderSize(target) / 1024 / 1024).toFixed(1);
console.log('  Папка: ' + target + '  (' + megabytes + ' МБ)');

// --- Архив -----------------------------------------------------------------

const archive = path.join(PACKAGE_ROOT, NAME + ' ' + version + '.zip');
if (fs.existsSync(archive)) fs.rmSync(archive);

if (fs.existsSync(SEVEN_ZIP)) {
  // Через 7-Zip, а не через Compress-Archive: тот пишет в пути обратные
  // слеши, и на macOS такой архив разворачивается кашей из файлов с
  // именами вида «папка\файл».
  const result = spawnSync(SEVEN_ZIP,
    ['a', '-tzip', '-mx=7', archive, target],
    { encoding: 'utf8' });

  if (result.status === 0) {
    const size = (fs.statSync(archive).size / 1024 / 1024).toFixed(1);
    console.log('  Архив: ' + archive + '  (' + size + ' МБ)');
  } else {
    console.log('  Архив не собрался: ' + (result.stderr || '').trim());
  }
} else {
  console.log('  7-Zip не найден, архив пропущен.');
}

// --- Самораспаковывающийся exe ---------------------------------------------

if (process.argv.includes('--sfx') && fs.existsSync(SEVEN_ZIP)) {
  const sfxModule = path.join(path.dirname(SEVEN_ZIP), '7z.sfx');
  const exe = path.join(PACKAGE_ROOT, NAME + ' ' + version + '.exe');

  if (!fs.existsSync(sfxModule)) {
    console.log('  Нет модуля 7z.sfx — самораспаковку пропускаю.');
  } else if (!fs.existsSync(archive)) {
    console.log('  Нет архива — самораспаковку пропускаю.');
  } else {
    // Самораспаковка — это склейка: модуль распаковщика плюс сам архив.
    // Отдельной программы для этого не нужно.
    const parts = [fs.readFileSync(sfxModule), fs.readFileSync(archive)];
    fs.writeFileSync(exe, Buffer.concat(parts));

    const size = (fs.statSync(exe).size / 1024 / 1024).toFixed(1);
    console.log('  Самораспаковка: ' + exe + '  (' + size + ' МБ)');
  }
}

console.log(NL + 'Готово.');
