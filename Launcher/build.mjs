// Сборка лаунчера.
//
// Ничего ставить не нужно: компилятор C# и все сборки WPF входят в саму
// Windows. Отсюда и выбор платформы — .NET Framework 4.8 есть на любой
// Windows 10 и 11 из коробки, поэтому лаунчер весит около сотни килобайт
// и запускается у игрока без установки рантайма.
//
// Цена решения: компилятор в системе — старый, до Roslyn, и понимает язык
// уровня C# 5. Никаких «=>» вместо тел свойств, интерполяции строк и
// оператора «?.». Если сборка вдруг падает на правильном с виду коде,
// причина почти всегда в этом.
//
// Запуск:
//   node build.mjs           — собрать
//   node build.mjs --run     — собрать и запустить

import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const NL = String.fromCharCode(10);

// Через fileURLToPath, а не разбором самого URL: в пути проекта есть пробел,
// и в адресе он записан как %20 — обычный разбор так его и оставит, а папки
// с таким именем на диске нет.
const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..');
const dist = path.join(here, 'dist');
const assets = path.join(dist, 'assets');

const CSC = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe';
const GAC = 'C:/Windows/Microsoft.NET/assembly/GAC_MSIL';
const WPF = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/WPF';
const FW = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319';

const OUTPUT = 'Приключения разбойника Жени.exe';

// --- Ссылки на сборки ------------------------------------------------------
//
// Пути к GAC содержат версию и открытый ключ, а те у разных установок Windows
// совпадают не всегда. Поэтому не прописываем путь целиком, а ищем.

function findInGac(name) {
  const folder = path.join(GAC, name);
  if (!fs.existsSync(folder)) return null;

  for (const version of fs.readdirSync(folder)) {
    const candidate = path.join(folder, version, name + '.dll');
    if (fs.existsSync(candidate)) return candidate;
  }

  return null;
}

function references() {
  const list = [];
  const missing = [];

  for (const name of ['PresentationFramework', 'WindowsBase', 'System.Xaml']) {
    const found = findInGac(name);
    if (found) list.push(found); else missing.push(name);
  }

  const core = path.join(WPF, 'PresentationCore.dll');
  if (fs.existsSync(core)) list.push(core); else missing.push('PresentationCore');

  for (const name of ['System.dll', 'System.Core.dll', 'System.Xml.dll']) {
    const found = path.join(FW, name);
    if (fs.existsSync(found)) list.push(found); else missing.push(name);
  }

  if (missing.length) {
    console.error('Не нашлись сборки: ' + missing.join(', '));
    process.exit(2);
  }

  return list;
}

// --- Сборка ----------------------------------------------------------------

function compile() {
  if (!fs.existsSync(CSC)) {
    console.error('Не найден компилятор ' + CSC);
    process.exit(2);
  }

  fs.mkdirSync(dist, { recursive: true });

  const sources = fs.readdirSync(path.join(here, 'src'))
    .filter((name) => name.endsWith('.cs'))
    .map((name) => path.join(here, 'src', name));

  const args = [
    '-nologo',
    '-target:winexe',
    '-optimize+',
    // Кодировка исходников. Без этого флага компилятор читает файлы без BOM
    // в кодовой странице системы, и все русские комментарии и строки
    // превращаются в кашу — включая те, что видит игрок.
    '-codepage:65001',
    // И ответы компилятора тоже в UTF-8, иначе сообщения об ошибках
    // приходят в кодировке консоли и читаются как набор символов.
    '-utf8output',
    '-out:' + path.join(dist, OUTPUT),
    '-win32manifest:' + path.join(here, 'app.manifest'),
  ];

  const icon = path.join(here, 'assets', 'launcher.ico');
  if (fs.existsSync(icon)) args.push('-win32icon:' + icon);

  for (const reference of references()) args.push('-reference:' + reference);

  const result = spawnSync(CSC, args.concat(sources), { encoding: 'utf8' });

  const output = ((result.stdout || '') + (result.stderr || '')).trim();
  if (output) console.log(output);

  if (result.status !== 0) {
    console.error('Сборка не удалась.');
    process.exit(1);
  }
}

// --- Файлы рядом с программой ----------------------------------------------

function copy(from, to, what) {
  if (!fs.existsSync(from)) {
    console.log('  пропущено (нет файла): ' + what);
    return false;
  }

  fs.copyFileSync(from, to);
  console.log('  ' + what);
  return true;
}

function collectAssets() {
  fs.mkdirSync(assets, { recursive: true });

  console.log('Файлы:');

  // История версий. Тот же файл, что читают сборщик игры и генератор сайта, —
  // копия, а не пересказ, поэтому разойтись им негде.
  copy(path.join(repo, 'CHANGELOG.md'),
       path.join(assets, 'CHANGELOG.md'), 'CHANGELOG.md');

  copy(path.join(repo, 'IsoRPG/Assets/_Game/Art/UI/Logo.png'),
       path.join(assets, 'logo.png'), 'логотип');

  // Фон баннера: берём подготовленный кадр, если он есть, иначе снимок сцены.
  const prepared = path.join(here, 'assets', 'background.jpg');
  const fallback = path.join(repo, 'shots', 'shot.png');

  if (fs.existsSync(prepared)) {
    copy(prepared, path.join(assets, 'background.jpg'), 'фон баннера');
  } else {
    copy(fallback, path.join(assets, 'background.png'), 'фон баннера (кадр из игры)');
  }

  const config = path.join(dist, 'launcher.json');

  if (!fs.existsSync(config)) {
    // Адреса пустые намеренно: пока сайта нет, проверять обновления негде,
    // и лаунчер молча этого не делает вместо того, чтобы стучаться в никуда.
    fs.writeFileSync(config, [
      '{',
      '  "updateUrl": "",',
      '  "siteUrl": ""',
      '}',
      '',
    ].join(NL));

    console.log('  launcher.json (адреса пока пустые)');
  }
}

// --- Поехали ---------------------------------------------------------------

console.log('Собираю лаунчер...');
compile();
collectAssets();

const exe = path.join(dist, OUTPUT);
const size = (fs.statSync(exe).size / 1024).toFixed(0);

console.log(NL + 'Готово: ' + exe);
console.log('Размер: ' + size + ' КБ');

if (process.argv.includes('--run')) {
  spawnSync(exe, [], { detached: true, stdio: 'ignore' });
}
