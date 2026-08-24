// Собирает страницу истории версий из CHANGELOG.md.
//
// Источник тот же, что читают сборщик игры и лаунчер. Второго списка
// изменений не заводим намеренно: два списка расходятся в первый же занятый
// день, и тогда игрок, лаунчер и сайт начинают рассказывать разное.
//
// Страница получается самодостаточной: логотип вшит, шрифты подключены
// ссылкой. Её можно открыть с диска, положить на любой хостинг или
// опубликовать как artifact — файл один и тот же.
//
// Запуск:
//   node tools/build-site.mjs

import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const NL = String.fromCharCode(10);

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..');

const CHANGELOG = path.join(repo, 'CHANGELOG.md');
const LOGO = path.join(repo, 'IsoRPG/Assets/_Game/Art/UI/Logo.png');
const LOGO_WEB = path.join(here, 'logo-web.png');
const OUT = path.join(repo, 'site', 'index.html');
const PACKAGE_ROOT = path.join(repo, 'Package');

// Адрес сайта. Ссылки на скачивание делаем абсолютными: страницу открывают
// и с сервера, и с диска, и из опубликованной копии — относительный путь
// работал бы только в первом случае. При переезде на домен меняется здесь.
const SITE_URL = 'http://5.129.195.139';

// --- Разбор истории --------------------------------------------------------
//
// Тот же грубый разбор, что в лаунчере: нужны заголовки версий, названия
// разделов и пункты списка. Полноценный Markdown здесь не нужен.

function parseChangelog(markdown) {
  const releases = [];
  let current = null;
  let section = null;

  for (const raw of markdown.split(NL)) {
    const line = raw.trim();

    const head = line.match(/^##\s+(\d+\.\d+\.\d+)\s*[—–-]?\s*(.*)$/);

    if (head) {
      current = { version: head[1], date: head[2].trim(), summary: '', sections: [] };
      releases.push(current);
      section = null;
      continue;
    }

    if (line.startsWith('## ')) { current = null; section = null; continue; }
    if (!current) continue;

    if (line.startsWith('### ')) {
      section = { title: line.slice(4).trim(), items: [] };
      current.sections.push(section);
      continue;
    }

    if (line.startsWith('* ') || line.startsWith('- ')) {
      if (!section) { section = { title: '', items: [] }; current.sections.push(section); }
      section.items.push(line.slice(2).trim());
      continue;
    }

    // Продолжение пункта, разбитого по ширине в исходнике.
    if (line && section && section.items.length && !line.startsWith('---')) {
      section.items[section.items.length - 1] += ' ' + line;
      continue;
    }

    if (line && !section && !line.startsWith('---') && !current.summary) {
      current.summary = line;
    }
  }

  return releases;
}

// --- Разметка --------------------------------------------------------------

function escapeHtml(text) {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

/** Выделение из Markdown — жирный текст и код. */
function inline(text) {
  let out = escapeHtml(text);
  out = out.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
  out = out.replace(/`(.+?)`/g, '<code>$1</code>');
  return out;
}

/**
 * Цвет метки раздела несёт смысл, а не украшает: добавленное, исправленное и
 * изменённое читаются по-разному, и глаз находит нужное, не читая слов.
 */
function sectionKind(title) {
  const lower = title.toLowerCase();

  if (lower.startsWith('добав')) return 'add';
  if (lower.startsWith('исправ')) return 'fix';
  if (lower.startsWith('измен') || lower.startsWith('улучш')) return 'change';

  return 'plain';
}

function renderRelease(release, index) {
  const parts = [];

  parts.push('<article class="release' + (index === 0 ? ' release--latest' : '') + '">');

  parts.push('<div class="release__mark">');
  parts.push('<div class="release__version">' + escapeHtml(release.version) + '</div>');

  if (release.date) {
    parts.push('<div class="release__date">' + escapeHtml(release.date) + '</div>');
  }

  if (index === 0) parts.push('<div class="release__badge">текущая</div>');

  parts.push('</div>');

  parts.push('<div class="release__body">');

  if (release.summary) {
    parts.push('<p class="release__summary">' + inline(release.summary) + '</p>');
  }

  for (const section of release.sections) {
    parts.push('<section class="group">');

    if (section.title) {
      parts.push('<h3 class="group__title group__title--' + sectionKind(section.title) + '">' +
                 escapeHtml(section.title) + '</h3>');
    }

    parts.push('<ul class="group__list">');

    for (const item of section.items) {
      parts.push('<li>' + inline(item) + '</li>');
    }

    parts.push('</ul>');
    parts.push('</section>');
  }

  parts.push('</div>');
  parts.push('</article>');

  return parts.join(NL);
}

// --- Логотип ---------------------------------------------------------------

/**
 * Логотип весит полтора мегабайта в исходнике — на странице это лишний вес
 * ради картинки шириной в треть экрана. Уменьшаем один раз и держим рядом.
 */
function prepareLogo() {
  if (fs.existsSync(LOGO_WEB)) return;
  if (!fs.existsSync(LOGO)) return;

  const script = [
    'Add-Type -AssemblyName System.Drawing',
    "$src = [System.Drawing.Image]::FromFile('" + LOGO.replace(/\\/g, '/') + "')",
    '$width = 520',
    '$height = [int]($src.Height * $width / $src.Width)',
    '$out = New-Object System.Drawing.Bitmap($width, $height)',
    '$g = [System.Drawing.Graphics]::FromImage($out)',
    '$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic',
    '$g.DrawImage($src, 0, 0, $width, $height)',
    '$g.Dispose()',
    "$out.Save('" + LOGO_WEB.replace(/\\/g, '/') + "', [System.Drawing.Imaging.ImageFormat]::Png)",
    '$out.Dispose(); $src.Dispose()',
  ].join('; ');

  spawnSync('powershell', ['-NoProfile', '-Command', script], { encoding: 'utf8' });
}

function logoDataUri() {
  const file = fs.existsSync(LOGO_WEB) ? LOGO_WEB : LOGO;
  if (!fs.existsSync(file)) return '';

  return 'data:image/png;base64,' + fs.readFileSync(file).toString('base64');
}

// --- Файлы для скачивания --------------------------------------------------

/**
 * Размеры берём с диска, а не пишем руками: цифра рядом с кнопкой должна
 * совпадать с тем, что человек реально получит, а после каждой сборки она
 * меняется.
 */
function downloads(version) {
  const setup = path.join(PACKAGE_ROOT, 'Установка Приключения разбойника Жени ' + version + '.exe');
  const archive = path.join(PACKAGE_ROOT, 'Приключения разбойника Жени ' + version + '.zip');

  function megabytes(file) {
    if (!fs.existsSync(file)) return null;
    return Math.round(fs.statSync(file).size / 1024 / 1024);
  }

  return {
    setup: megabytes(setup),
    archive: megabytes(archive),
    setupUrl: SITE_URL + '/downloads/HighFlyingBird-Setup-latest.exe',
    archiveUrl: SITE_URL + '/downloads/HighFlyingBird-latest.zip',
  };
}

// --- Страница --------------------------------------------------------------

function buildPage(releases) {
  const latest = releases[0] || { version: '—', date: '' };
  const logo = logoDataUri();

  const files = downloads(latest.version);

  const total = releases.reduce(
    (sum, release) => sum + release.sections.reduce((n, s) => n + s.items.length, 0), 0);

  return `<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Alegreya:wght@500;700;800&family=PT+Sans:wght@400;700&family=JetBrains+Mono:wght@500;700&display=swap">
<title>Хроника разбойника Жени</title>
<style>
  /*
    Тема одна и она тёмная — как игра и как лаунчер. Это выбор, а не
    упущение: страница про игру, у которой тёмный интерфейс, и светлый
    вариант читался бы как документ о ней, а не как её часть.

    Раз тема одна, цвета обязаны быть заданы явно все до единого: фон
    страницы задаётся токеном, а не наследуется, иначе на светлом хосте
    тёмный текст ляжет на светлую подложку.
  */
  :root {
    --ground: #14111C;
    --surface: #1C1826;
    --surface-2: #221D2E;
    --ink: #EDE9F4;
    --ink-soft: #B4ACC2;
    --ink-faint: #7E7691;
    --line: #2E2740;

    --gold: #E8A93A;
    --gold-bright: #F5C663;
    --purple: #A96FD0;

    --add: #63BE72;
    --fix: #E0934F;
    --change: #6E9FE4;

    --shadow: 0 1px 2px rgba(0, 0, 0, .3), 0 10px 30px rgba(0, 0, 0, .35);
    --radius: 10px;

    /* Чтобы полосы прокрутки и поля были тёмными, а не белыми. */
    color-scheme: dark;
  }

  * { box-sizing: border-box; }

  body {
    margin: 0;
    background: var(--ground);
    color: var(--ink);
    font-family: "PT Sans", "Segoe UI", system-ui, sans-serif;
    font-size: 16px;
    line-height: 1.6;
    -webkit-font-smoothing: antialiased;
  }

  .page {
    max-width: 960px;
    margin: 0 auto;
    padding: 0 24px 96px;
  }

  /* --- Шапка --------------------------------------------------------- */

  .head {
    display: flex;
    align-items: center;
    gap: 32px;
    flex-wrap: wrap;
    padding: 48px 0 36px;
    border-bottom: 1px solid var(--line);
  }

  .head__logo {
    width: 260px;
    max-width: 100%;
    height: auto;
    display: block;
    filter: drop-shadow(0 6px 18px rgba(0, 0, 0, .35));
  }

  .head__text { flex: 1 1 300px; }

  .head__eyebrow {
    font-family: "JetBrains Mono", ui-monospace, monospace;
    font-size: 12px;
    font-weight: 700;
    text-transform: uppercase;
    color: var(--gold);
    margin: 0 0 8px;
  }

  .head__title {
    font-family: Alegreya, Georgia, serif;
    font-weight: 800;
    font-size: clamp(30px, 4.4vw, 44px);
    line-height: 1.1;
    margin: 0 0 10px;
    text-wrap: balance;
  }

  .head__lede {
    margin: 0;
    color: var(--ink-soft);
    max-width: 46ch;
  }

  /* --- Кнопка скачивания --------------------------------------------- */

  .get {
    flex: 0 0 auto;
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 8px;
  }

  /*
    Золото то же, что на кнопке запуска в лаунчере. Человек, скачавший
    игру здесь, через минуту увидит ту же кнопку у себя на экране — и
    должен узнать её, а не гадать, та ли это программа.
  */
  .get__button {
    display: inline-block;
    padding: 15px 30px 16px;
    border-radius: 5px;
    background: linear-gradient(180deg, var(--gold-bright) 0%, var(--gold) 55%, #B87F22 100%);
    color: #241808;
    font-family: "PT Sans", sans-serif;
    font-size: 17px;
    font-weight: 700;
    text-decoration: none;
    box-shadow: 0 2px 4px rgba(0, 0, 0, .3), 0 10px 24px rgba(232, 169, 58, .18);
    transition: transform .18s cubic-bezier(.16, 1, .3, 1), box-shadow .18s;
  }

  .get__button:hover {
    transform: translateY(-1px);
    box-shadow: 0 3px 6px rgba(0, 0, 0, .35), 0 14px 30px rgba(232, 169, 58, .26);
  }

  .get__button:focus-visible {
    outline: 2px solid var(--gold-bright);
    outline-offset: 3px;
  }

  .get__meta {
    margin: 0;
    font-size: 12.5px;
    color: var(--ink-faint);
  }

  .get__alt {
    margin: 0;
    font-size: 12.5px;
    color: var(--ink-faint);
  }

  .get__alt a {
    color: var(--ink-soft);
    text-decoration: underline;
    text-underline-offset: 2px;
  }

  .get__alt a:hover { color: var(--gold); }

  /* --- Сводка -------------------------------------------------------- */

  .facts {
    display: flex;
    gap: 12px;
    flex-wrap: wrap;
    padding: 26px 0 8px;
  }

  .fact {
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    padding: 14px 18px;
    min-width: 132px;
    box-shadow: var(--shadow);
  }

  .fact__label {
    font-size: 12px;
    color: var(--ink-faint);
    margin: 0 0 4px;
  }

  .fact__value {
    font-family: "JetBrains Mono", ui-monospace, monospace;
    font-size: 20px;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
    margin: 0;
    color: var(--ink);
  }

  .fact--now .fact__value { color: var(--gold); }

  /* --- Лента версий -------------------------------------------------- */

  .releases { padding-top: 44px; }

  .release {
    display: grid;
    grid-template-columns: 170px 1fr;
    gap: 32px;
    padding: 34px 0;
    border-top: 1px solid var(--line);
  }

  .release:first-child { border-top: none; }

  .release__mark { position: relative; }

  @media (min-width: 760px) {
    /*
      Номер версии едет вместе с чтением: у длинной записи легко потерять,
      к какой версии относится то, что читаешь сейчас.
    */
    .release__mark {
      position: sticky;
      top: 24px;
      align-self: start;
    }
  }

  .release__version {
    font-family: "JetBrains Mono", ui-monospace, monospace;
    font-size: 26px;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
    color: var(--ink);
    line-height: 1.2;
  }

  .release--latest .release__version { color: var(--gold); }

  .release__date {
    font-size: 13px;
    color: var(--ink-faint);
    margin-top: 4px;
  }

  .release__badge {
    display: inline-block;
    margin-top: 10px;
    padding: 3px 10px 4px;
    border-radius: 999px;
    background: color-mix(in srgb, var(--purple) 18%, transparent);
    border: 1px solid color-mix(in srgb, var(--purple) 45%, transparent);
    color: var(--purple);
    font-size: 11px;
    font-weight: 700;
  }

  .release__summary {
    font-family: Alegreya, Georgia, serif;
    font-size: 19px;
    line-height: 1.5;
    color: var(--ink);
    margin: 0 0 22px;
    text-wrap: pretty;
  }

  .group + .group { margin-top: 22px; }

  .group__title {
    font-family: "PT Sans", sans-serif;
    font-size: 13px;
    font-weight: 700;
    margin: 0 0 8px;
    display: inline-flex;
    align-items: center;
    gap: 8px;
    color: var(--ink-soft);
  }

  /* Точка-метка вместо значка: в этом размере значок читается хуже цвета. */
  .group__title::before {
    content: "";
    width: 8px;
    height: 8px;
    border-radius: 2px;
    background: var(--ink-faint);
  }

  .group__title--add::before { background: var(--add); }
  .group__title--fix::before { background: var(--fix); }
  .group__title--change::before { background: var(--change); }

  .group__title--add { color: var(--add); }
  .group__title--fix { color: var(--fix); }
  .group__title--change { color: var(--change); }

  .group__list {
    margin: 0;
    padding: 0;
    list-style: none;
    display: flex;
    flex-direction: column;
    gap: 9px;
  }

  .group__list li {
    position: relative;
    padding-left: 18px;
    color: var(--ink-soft);
    max-width: 64ch;
  }

  .group__list li::before {
    content: "";
    position: absolute;
    left: 2px;
    top: .68em;
    width: 5px;
    height: 5px;
    border-radius: 50%;
    background: color-mix(in srgb, var(--gold) 70%, transparent);
  }

  .group__list strong { color: var(--ink); font-weight: 700; }

  .group__list code {
    font-family: "JetBrains Mono", ui-monospace, monospace;
    font-size: .88em;
    background: var(--surface-2);
    border: 1px solid var(--line);
    border-radius: 4px;
    padding: 1px 5px;
  }

  /* --- Подвал -------------------------------------------------------- */

  .foot {
    margin-top: 52px;
    padding-top: 24px;
    border-top: 1px solid var(--line);
    color: var(--ink-faint);
    font-size: 13px;
    display: flex;
    justify-content: space-between;
    gap: 16px;
    flex-wrap: wrap;
  }

  .foot code {
    font-family: "JetBrains Mono", ui-monospace, monospace;
    font-size: 12px;
  }

  @media (max-width: 759px) {
    .release { grid-template-columns: 1fr; gap: 14px; }
    .head { gap: 22px; padding-top: 32px; }
    .head__logo { width: 200px; }
  }

  @media (prefers-reduced-motion: reduce) {
    * { animation: none !important; transition: none !important; }
  }
</style>

<div class="page">
  <header class="head">
    ${logo ? '<img class="head__logo" src="' + logo + '" alt="Приключения разбойника Жени">' : ''}
    <div class="head__text">
      <p class="head__eyebrow">Хроника изменений</p>
      <h1 class="head__title">Приключения разбойника Жени</h1>
      <p class="head__lede">Что менялось в игре от сборки к сборке. Записи идут
      от новых к старым — сверху то, что в игре прямо сейчас.</p>
    </div>

    <div class="get">
      <a class="get__button" href="${files.setupUrl}">Скачать игру</a>
      <p class="get__meta">Установщик для Windows${files.setup ? ', ' + files.setup + ' МБ' : ''}</p>
      <p class="get__alt">или <a href="${files.archiveUrl}">архивом</a>${files.archive ? ', ' + files.archive + ' МБ' : ''} — без установки</p>
    </div>
  </header>

  <div class="facts">
    <div class="fact fact--now">
      <p class="fact__label">Текущая версия</p>
      <p class="fact__value">${escapeHtml(latest.version)}</p>
    </div>
    <div class="fact">
      <p class="fact__label">Обновлена</p>
      <p class="fact__value">${escapeHtml(latest.date || '—')}</p>
    </div>
    <div class="fact">
      <p class="fact__label">Версий выпущено</p>
      <p class="fact__value">${releases.length}</p>
    </div>
    <div class="fact">
      <p class="fact__label">Изменений всего</p>
      <p class="fact__value">${total}</p>
    </div>
  </div>

  <main class="releases">
${releases.map(renderRelease).join(NL)}
  </main>

  <footer class="foot">
    <span>Собрано из <code>CHANGELOG.md</code> — того же файла, что читают игра и лаунчер.</span>
    <span>OXBOX</span>
  </footer>
</div>
`;
}

// --- Поехали ---------------------------------------------------------------

if (!fs.existsSync(CHANGELOG)) {
  console.error('Не найден ' + CHANGELOG);
  process.exit(1);
}

const releases = parseChangelog(fs.readFileSync(CHANGELOG, 'utf8'));

if (!releases.length) {
  console.error('В CHANGELOG.md не нашлось ни одной версии.');
  process.exit(1);
}

prepareLogo();

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, buildPage(releases));

const size = (fs.statSync(OUT).size / 1024).toFixed(0);

console.log('Страница собрана: ' + OUT);
console.log('Версий: ' + releases.length + ', размер: ' + size + ' КБ');
