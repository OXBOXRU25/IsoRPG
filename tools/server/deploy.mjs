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
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const NL = String.fromCharCode(10);

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');

const HOST = 'root@5.129.195.139';
const KEY = 'C:/Users/OXBOX/.ssh/id_ed25519_game';
const REMOTE_ROOT = '/var/www/game';

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
console.log('Адрес: http://5.129.195.139/');
