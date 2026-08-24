// Выкладывает на сервер сайт, историю версий и сборки игры.
//
// Одной командой, потому что выкладка из трёх шагов, которые делают руками,
// рано или поздно выполняется наполовину: страницу обновили, а файл истории
// забыли — и лаунчер у игроков продолжает считать, что новой версии нет.
//
// Запуск:
//   node tools/server/deploy.mjs             сайт и история версий
//   node tools/server/deploy.mjs --builds    плюс установщик и архив

import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const NL = String.fromCharCode(10);

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');

const HOST = 'root@5.129.195.139';

// Адрес, по которому сервер виден снаружи. Он попадает в описание обновления,
// поэтому меняется здесь же при переезде на домен — и только здесь.
const SITE_URL = 'https://mygame.oxboxdigital.ru';
const KEY = 'C:/Users/OXBOX/.ssh/id_ed25519_game';
const REMOTE_ROOT = '/var/www/game';

// Имя пакета — такое же, как в сборщике. Файлы на диске названы по нему.
const NAME = 'Приключения разбойника Жени';

const SITE = path.join(repo, 'site', 'index.html');
const CHANGELOG = path.join(repo, 'CHANGELOG.md');
const PACKAGE_ROOT = path.join(repo, 'Package');

// --- Мелочи ----------------------------------------------------------------

function run(command, args) {
  const result = spawnSync(command, args, { encoding: 'utf8' });

  const output = ((result.stdout || '') + (result.stderr || '')).trim();
  return { ok: result.status === 0, output };
}

function scp(local, remote) {
  // Путь приводим к системному виду: у scp на Windows тот же изъян, что у
  // компилятора C# — прямые слеши вместе с пробелом в имени он рвёт по
  // пробелу и ищет обрубок.
  return run('scp', ['-i', KEY, path.resolve(local), HOST + ':' + remote]);
}

function ssh(command) {
  return run('ssh', ['-i', KEY, HOST, command]);
}

function version() {
  const match = fs.readFileSync(CHANGELOG, 'utf8').match(/^##\s+(\d+\.\d+\.\d+)/m);
  return match ? match[1] : '0.0.0';
}

// --- Сайт ------------------------------------------------------------------

console.log('Пересобираю страницу...');

const build = run('node', [path.join(repo, 'tools', 'build-site.mjs')]);
if (!build.ok) { console.error(build.output); process.exit(1); }

console.log('Выкладываю...');

for (const [local, remote, label] of [
  [SITE, REMOTE_ROOT + '/index.html', 'страница истории'],
  [CHANGELOG, REMOTE_ROOT + '/CHANGELOG.md', 'файл версий для лаунчера'],
]) {
  const result = scp(local, remote);
  console.log(result.ok ? '  ' + label : '  НЕ УДАЛОСЬ: ' + label + NL + result.output);
}

// --- Сборки ----------------------------------------------------------------

if (process.argv.includes('--builds')) {
  const current = version();

  // На сервере имена латиницей и без пробелов. Кириллическое имя в адресе
  // превращается в три строки процентов, и такую ссылку нельзя ни прочитать,
  // ни надёжно переслать: половина мессенджеров рвёт её по пробелу.
  const files = [
    ['Установка Приключения разбойника Жени ' + current + '.exe',
     'HighFlyingBird-Setup-' + current + '.exe',
     'HighFlyingBird-Setup-latest.exe', 'установщик'],

    ['Приключения разбойника Жени ' + current + '.zip',
     'HighFlyingBird-' + current + '.zip',
     'HighFlyingBird-latest.zip', 'архив'],
  ];

  for (const [name, remoteName, latestName, label] of files) {
    const local = path.join(PACKAGE_ROOT, name);

    if (!fs.existsSync(local)) {
      console.log('  пропущено (нет файла): ' + label);
      continue;
    }

    const size = (fs.statSync(local).size / 1024 / 1024).toFixed(1);
    console.log('  ' + label + ', ' + size + ' МБ — заливаю, это не быстро');

    const result = scp(local, REMOTE_ROOT + '/downloads/' + remoteName);

    if (!result.ok) {
      console.log('  НЕ УДАЛОСЬ: ' + result.output);
      continue;
    }

    // Ссылка на «последнюю версию» — одна и та же навсегда, её можно дать
    // человеку один раз. Символическая ссылка, а не вторая копия: файл на
    // шестьдесят мегабайт незачем держать дважды.
    ssh('ln -sfn ' + REMOTE_ROOT + '/downloads/' + remoteName + ' ' +
        REMOTE_ROOT + '/downloads/' + latestName);
  }

  // --- Лаунчер отдельным файлом -----------------------------------------
  //
  // Три мегабайта против шестидесяти у установщика. Лаунчер меняется своим
  // темпом и переустанавливать ради него всю игру незачем: достаточно
  // заменить несколько файлов рядом с ней.

  const launcherZip = path.join(PACKAGE_ROOT, 'HighFlyingBird-Launcher.zip');

  if (fs.existsSync(launcherZip)) {
    const size = (fs.statSync(launcherZip).size / 1024 / 1024).toFixed(1);
    const sent = scp(launcherZip, REMOTE_ROOT + '/downloads/HighFlyingBird-Launcher.zip');

    console.log(sent.ok
      ? '  лаунчер отдельно, ' + size + ' МБ'
      : '  НЕ УДАЛОСЬ лаунчер: ' + sent.output);
  }

  // --- Описание обновления для лаунчера ---------------------------------
  //
  // Отдельный файл, а не CHANGELOG: лаунчеру нужен адрес архива, его размер
  // и контрольная сумма. Сумма здесь не для порядка — лаунчер кладёт
  // скачанное прямо в папку игры, то есть ставит на компьютер исполняемый
  // код, и без проверки любой сбой при передаче становится запуском
  // неизвестно чего.

  const gameArchive = path.join(PACKAGE_ROOT, NAME + ' игра ' + current + '.zip');

  if (!fs.existsSync(gameArchive)) {
    console.log('  архива игры нет — обновление лаунчер не увидит');
  } else {
    const remoteName = 'HighFlyingBird-game-' + current + '.zip';
    const size = fs.statSync(gameArchive).size;

    console.log('  архив игры для обновлений, ' +
                (size / 1024 / 1024).toFixed(1) + ' МБ — заливаю');

    const sent = scp(gameArchive, REMOTE_ROOT + '/downloads/' + remoteName);

    if (!sent.ok) {
      console.log('  НЕ УДАЛОСЬ: ' + sent.output);
    } else {
      const hash = crypto.createHash('sha256')
        .update(fs.readFileSync(gameArchive))
        .digest('hex');

      const q = String.fromCharCode(34);

      // Собираем JSON построчно с кавычкой из кода символа: экранированные
      // кавычки внутри строк внутри генератора читаются отвратительно и
      // ломаются при первой же правке.
      const manifest = [
        '{',
        '  ' + q + 'version' + q + ': ' + q + current + q + ',',
        '  ' + q + 'url' + q + ': ' + q + SITE_URL + '/downloads/' + remoteName + q + ',',
        '  ' + q + 'size' + q + ': ' + size + ',',
        '  ' + q + 'sha256' + q + ': ' + q + hash + q,
        '}',
        '',
      ].join(NL);

      const local = path.join(repo, 'site', 'update.json');
      fs.writeFileSync(local, manifest);

      const put = scp(local, REMOTE_ROOT + '/update.json');
      console.log(put.ok ? '  описание обновления' : '  НЕ УДАЛОСЬ описание: ' + put.output);
      console.log('  сумма: ' + hash.slice(0, 16) + '...');
    }
  }

  // Прошлые выкладки с кириллицей в имени, если они остались.
  ssh('find ' + REMOTE_ROOT + '/downloads -maxdepth 1 -name "*Приключения*" -delete');
}

// --- Права -----------------------------------------------------------------
//
// Файлы приезжают от root, а отдаёт их веб-сервер под своим пользователем.
// Без этого шага сайт отвечает «доступ запрещён» на совершенно нормальные
// файлы, и причина ищется где угодно, только не в правах.

ssh('chown -R www-data:www-data ' + REMOTE_ROOT + ' && chmod -R a+rX ' + REMOTE_ROOT);

// --- Проверка --------------------------------------------------------------

const check = ssh('curl -sS -o /dev/null -w "%{http_code}" http://localhost/ ; ' +
                  'echo " " ; ' +
                  'curl -sS -o /dev/null -w "%{http_code}" http://localhost/CHANGELOG.md');

console.log(NL + 'Ответы сервера (страница, файл версий): ' + check.output.trim());
console.log('Адрес: https://mygame.oxboxdigital.ru/');
