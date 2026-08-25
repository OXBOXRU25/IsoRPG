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
const FAVICON = path.join(here, 'favicon.png');
const OUT = path.join(repo, 'site', 'index.html');
const PACKAGE_ROOT = path.join(repo, 'Package');

// Адрес сайта. Ссылки на скачивание делаем абсолютными: страницу открывают
// и с сервера, и с диска, и из опубликованной копии — относительный путь
// работал бы только в первом случае. При переезде на домен меняется здесь.
const SITE_URL = 'https://mygame.oxboxdigital.ru';

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

  if (index === 0) parts.push('<div class="release__badge" data-i18n="current">текущая</div>');

  parts.push('</div>');

  parts.push('<div class="release__body">');

  if (release.summary) {
    parts.push('<p class="release__summary">' + inline(release.summary) + '</p>');
  }

  for (const section of release.sections) {
    parts.push('<section class="group">');

    if (section.title) {
      parts.push('<h3 class="group__title group__title--' + sectionKind(section.title) + '"' +
                 ' data-i18n="section-' + sectionKind(section.title) + '">' +
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


/**
 * Значок вкладки из логотипа игры.
 *
 * Логотип широкий, с надписью во всю ширину; в квадрате 64 на 64 от неё
 * останутся нечитаемые полосы. Поэтому берём центральный квадрат — там
 * орёл, который и опознаётся в ряду вкладок.
 *
 * Готовим один раз и держим рядом: пересчитывать при каждой сборке
 * страницы нечего, картинка не меняется.
 */
function prepareFavicon() {
  if (fs.existsSync(FAVICON)) return;
  if (!fs.existsSync(LOGO)) return;

  const script = [
    "Add-Type -AssemblyName System.Drawing",
    "$src = [System.Drawing.Image]::FromFile('" + LOGO.replace(/\\/g, "/") + "')",

    // Голова орла крупным планом.
    //
    // Центральный квадрат по высоте забирает вместе с головой размах
    // крыльев: в значке 32 на 32 они превращаются в светлые полосы по
    // бокам, и опознать в этом птицу нельзя. Берём только голову с клювом —
    // силуэт, который читается даже в ряду вкладок.
    //
    // Доли, а не пиксели: логотип могут перерисовать в другом размере.
    "$side = [int]($src.Height * 0.26)",
    "$x = [int]($src.Width * 0.399)",
    "$y = [int]($src.Height * 0.30)",

    "$out = New-Object System.Drawing.Bitmap(64, 64)",
    "$g = [System.Drawing.Graphics]::FromImage($out)",
    "$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic",
    "$rect = New-Object System.Drawing.Rectangle(0, 0, 64, 64)",
    "$from = New-Object System.Drawing.Rectangle($x, $y, $side, $side)",
    "$g.DrawImage($src, $rect, $from, [System.Drawing.GraphicsUnit]::Pixel)",
    "$g.Dispose()",
    "$out.Save('" + FAVICON.replace(/\\/g, "/") + "', [System.Drawing.Imaging.ImageFormat]::Png)",
    "$out.Dispose(); $src.Dispose()",
  ].join("; ");

  spawnSync("powershell", ["-NoProfile", "-Command", script], { encoding: "utf8" });
}

function buttonSkin() {
  const file = path.join(here, 'button-web.png');
  if (!fs.existsSync(file)) return '';

  return 'data:image/png;base64,' + fs.readFileSync(file).toString('base64');
}

function favicon() {
  if (!fs.existsSync(FAVICON)) return "";

  return "data:image/png;base64," + fs.readFileSync(FAVICON).toString("base64");
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
  const setup = path.join(PACKAGE_ROOT, 'Установка Adventures of Zhenya ' + version + '.exe');
  const archive = path.join(PACKAGE_ROOT, 'Adventures of Zhenya ' + version + '.zip');

  function megabytes(file) {
    if (!fs.existsSync(file)) return null;
    return Math.round(fs.statSync(file).size / 1024 / 1024);
  }

  return {
    setup: megabytes(setup),
    archive: megabytes(archive),
    setupUrl: SITE_URL + '/downloads/AdventuresOfZhenya-Setup-latest.exe',
    archiveUrl: SITE_URL + '/downloads/AdventuresOfZhenya-latest.zip',
  };
}

// --- Страница --------------------------------------------------------------

function buildPage(releases) {
  const latest = releases[0] || { version: '—', date: '' };
  const logo = logoDataUri();

  const files = downloads(latest.version);

  const total = releases.reduce(
    (sum, release) => sum + release.sections.reduce((n, s) => n + s.items.length, 0), 0);

  return `<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">

<!--
  Значок вкладки — орёл с логотипа игры.

  Вшит в страницу, а не лежит файлом: страницу открывают и с сервера, и
  с диска, и отдельной копией — отдельный файл значка при этом теряется,
  и во вкладке появляется чужая заглушка браузера.
-->
<link rel="icon" type="image/png" href="${favicon()}">

<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Alegreya:wght@500;700;800&family=PT+Sans:wght@400;700&family=JetBrains+Mono:wght@500;700&display=swap">
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
    max-width: 1120px;
    margin: 0 auto;
    padding: 0 24px 96px;
  }

  /* --- Живой фон первого экрана ---------------------------------------- */

  /*
    Тот же ролик, что играет в меню игры. Смысл не в украшении: человек,
    открывший страницу, должен за секунду понять, во что игра, — а страница
    состоит из списка изменений, по которому этого не видно.

    Файлом рядом, а не вшитый: полмегабайта в base64 распухают до семисот
    килобайт и грузятся до первого показа страницы, задерживая её целиком.
  */
  /*
    Прибит к окну, а не к странице.

    При обычном фоне ролик уезжает вверх вместе с содержимым и через экран
    прокрутки исчезает совсем. Прибитый остаётся на месте, а страница едет
    поверх — тот же приём, что у заставки в игре: мир на месте, движется
    только то, что читают.
  */
  .hero {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    height: 100vh;
    overflow: hidden;
    z-index: 0;

    /* Фон не должен перехватывать нажатия у того, что лежит на нём. */
    pointer-events: none;
  }

  .hero__video {
    width: 100%;
    height: 100%;
    object-fit: cover;

    /* Приглушаем совсем немного: ролик должен быть виден, иначе он
       превращается в тёмное пятно и непонятно, зачем он там. */
    opacity: 0.85;
  }

  /*
    Затемнение поверх ролика.

    Снизу оно доводится до цвета страницы — иначе видео обрывается ровной
    линией поперёк экрана, и это читается как ошибка вёрстки, а не как приём.
  */
  .hero__shade {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    /*
      Затемнение держим лёгким сверху и плотным снизу.

      Сверху текста мало и картинку видно; книзу она уходит в цвет
      страницы, чтобы ролик не обрывался линией. Заодно под самим текстом
      добавлена мягкая тень — так читаемость держится без общего мрака.
    */
    /*
      Верх открыт, низ уходит в цвет страницы.

      Ролик показывается только в верхней трети окна: ниже начинается
      текст, и под ним фон обязан стать сплошным, иначе строки лягут
      прямо на ветки деревьев.
    */
    background:
      linear-gradient(180deg,
        rgba(20, 17, 28, .10) 0%,
        rgba(20, 17, 28, .34) 34%,
        rgba(20, 17, 28, .82) 58%,
        var(--ground) 76%);
  }

  /*
    Полотно под всем, что ниже шапки.

    Без него страница едет поверх неподвижного ролика прозрачной, и текст
    списка версий читается по кустам и надгробиям. Полотно закрывает фон
    ровно там, где начинается чтение, и растушёвано сверху — чтобы граница
    не читалась линией.
  */
  .sheet {
    position: relative;
    z-index: 1;
    background: var(--ground);

    /*
      Растушёвка вверх — чтобы граница полотна не читалась линией.

      Тень поднимается на полсотни пикселей выше самого полотна, а рисуется
      оно позже шапки и потому накрывало ей текст: нижняя строка подписи
      уходила в темноту. Поэтому шапка поднята на слой выше — тень ложится
      под неё, а не на неё.
    */
    box-shadow: 0 -50px 70px 50px var(--ground);
  }



  /*
    Содержимое — поверх фона.

    Переключатель языка исключён намеренно: он позиционируется абсолютно,
    и это правило перебивало его собственный position — кнопка уезжала в
    поток к левому краю, а выпадающий список оставался привязан к странице
    и открывался справа, за полэкрана от кнопки.
  */
  .page > *:not(.hero):not(.lang):not(.head) { position: relative; z-index: 1; }

  /*
    Шапка — слоем выше остального.

    Полотно под списком версий растушёвано вверх тенью, и на одном слое с
    шапкой эта тень ложилась прямо на подпись: нижняя строка уходила в
    темноту. Селектор здесь такой же длины, как общий, и стоит после него —
    иначе он проигрывает по специфичности и не действует вовсе.
  */
  .page > .head:not(.hero):not(.lang) { position: relative; z-index: 3; }
  .lang { z-index: 20; }

  /* --- Выбор языка ---------------------------------------------------- */

  /*
    Свой список, а не <select>: браузерный рисуется системой, и на тёмной
    странице он выпадает белым прямоугольником с чужими шрифтами. Здесь
    важнее, что он выглядит частью страницы, чем что он привычный, —
    вариантов всего три и промахнуться негде.
  */
  .lang {
    position: absolute;
    top: 18px;
    right: 0;
    z-index: 20;
    font-family: "PT Sans", sans-serif;
  }

  .lang__button {
    display: flex;
    align-items: center;
    gap: 8px;
    min-height: 38px;
    padding: 0 12px 0 14px;
    border: 1px solid var(--line);
    border-radius: 8px;
    background: var(--surface);
    color: var(--ink-soft);
    font-family: inherit;
    font-size: 14px;
    font-weight: 700;
    letter-spacing: normal;
    cursor: pointer;
    transition: border-color .18s, color .18s;
  }

  .lang__button:hover,
  .lang__button[aria-expanded="true"] {
    border-color: var(--gold);
    color: var(--ink);
  }

  .lang__button:focus-visible {
    outline: 2px solid var(--gold-bright);
    outline-offset: 2px;
  }

  /* Стрелка поворачивается — это и есть обещание, что список раскроется. */
  .lang__arrow {
    width: 9px;
    height: 9px;
    border-right: 2px solid currentColor;
    border-bottom: 2px solid currentColor;
    transform: translateY(-2px) rotate(45deg);
    transition: transform .18s cubic-bezier(.16, 1, .3, 1);
  }

  .lang__button[aria-expanded="true"] .lang__arrow {
    transform: translateY(1px) rotate(225deg);
  }

  .lang__menu {
    position: absolute;
    top: calc(100% + 6px);
    right: 0;
    min-width: 148px;
    margin: 0;
    padding: 5px;
    list-style: none;
    border: 1px solid var(--line);
    border-radius: 8px;
    background: var(--surface);
    box-shadow: var(--shadow);

    /* Закрытое состояние: не display:none, чтобы раскрытие было плавным. */
    opacity: 0;
    visibility: hidden;
    transform: translateY(-6px);
    transition: opacity .18s, transform .18s cubic-bezier(.16, 1, .3, 1), visibility .18s;
  }

  .lang[data-open="true"] .lang__menu {
    opacity: 1;
    visibility: visible;
    transform: translateY(0);
  }

  .lang__option {
    display: flex;
    align-items: center;
    gap: 10px;
    width: 100%;
    min-height: 42px;
    padding: 0 12px;
    border: 0;
    border-radius: 6px;
    background: none;
    color: var(--ink-soft);
    font-family: inherit;
    font-size: 14px;
    text-align: left;
    cursor: pointer;
    transition: background-color .15s, color .15s;
  }

  .lang__option:hover { background: rgba(232, 169, 58, .1); color: var(--ink); }

  .lang__option[aria-selected="true"] {
    color: var(--gold-bright);
    font-weight: 700;
  }

  .lang__code {
    min-width: 26px;
    font-weight: 700;
  }

  /* --- Шапка --------------------------------------------------------- */

  .page { position: relative; }

  .head {
    display: flex;
    align-items: center;
    gap: 32px;
    flex-wrap: wrap;
    padding: 48px 0 36px;
    border-bottom: 1px solid var(--line);
  }

  .head__logo {
    width: 330px;
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

  .head__title,
  .head__lede,
  .head__eyebrow {
    text-shadow: 0 2px 14px rgba(10, 8, 14, .95), 0 1px 4px rgba(10, 8, 14, 1);
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

    /*
      Светлее, чем остальной приглушённый текст.
      
      Подпись лежит поверх видео, а не поверх ровной подложки: там, где за
      ней тёмные ветки, приглушённый оттенок проваливается совсем. Тень
      этого не спасает — она добавляет контраст к фону, а не к самому
      цвету букв.
    */
    color: var(--ink);
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

    /*
      Кнопка из игры — та же самая картинка, что на экране запуска.

      Числа в border-image-slice — это отступы в пикселях ИСХОДНОЙ картинки:
      сколько отрезать сверху, справа, снизу и слева, чтобы вырезать углы.
      Правый и левый срезы крупные: там металлические наконечники с
      заклёпками, их растягивать нельзя. Ключевое слово fill оставляет
      середину картинки как заливку, иначе кнопка была бы пустой внутри.
    */
    border-style: solid;
    border-width: 14px 38px;
    border-image: url("${buttonSkin()}") 50 120 fill / 14px 38px / 0 stretch;
    background: none;

    padding: 8px 14px 10px;
    color: #241808;
    font-family: "PT Sans", sans-serif;
    font-size: 17px;
    font-weight: 700;
    text-decoration: none;
    /* Тень вокруг рисованной кнопки не нужна: у неё есть собственный
       контур, и внешняя тень читается как ореол. Оставляем свечение,
       которое только подсвечивает её на тёмном фоне. */
    filter: drop-shadow(0 6px 18px rgba(232, 169, 58, .22));
    transition: transform .18s cubic-bezier(.16, 1, .3, 1), filter .18s;
  }

  .get__button:hover {
    transform: translateY(-1px);
    filter: drop-shadow(0 8px 24px rgba(232, 169, 58, .34)) brightness(1.06);
  }

  .get__button:active {
    transform: translateY(1px);
    filter: drop-shadow(0 3px 10px rgba(232, 169, 58, .2)) brightness(0.94);
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

    /* По центру: карточки — итог первого экрана, а он симметричный.
       Прижатые влево, они спорят с центрированной шапкой над ними. */
    justify-content: center;

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
    .page { padding: 0 18px 64px; }

    .release { grid-template-columns: 1fr; gap: 14px; }
    /*
      Шапка на телефоне — одной колонкой по центру.

      На широком экране логотип стоит слева, а текст справа, и левый край
      текста держит взгляд. На узком колонка одна, и прижатый влево текст
      под центрированным логотипом читается как съехавший: у блока два
      разных центра. Либо всё слева, либо всё по центру — здесь по центру,
      потому что логотип симметричный и задаёт ось.
    */
    .head {
      gap: 18px;
      padding-top: 4px;
      flex-direction: column;
      align-items: center;
      text-align: center;
    }

    .head__logo { width: 253px; }
    .head__text { flex: 0 1 auto; }

    /* Переключатель языка — над логотипом, а не поверх него.
       Он лежит вне шапки, поэтому центрируется собственным маргином,
       а не выравниванием флекса. */
    /* relative, а не static: выпадающий список позиционируется
       относительно кнопки, и без опоры он уезжает к краю страницы. */
    .lang {
      position: relative;
      top: auto;
      right: auto;
      width: fit-content;
      margin: 20px auto 16px;
    }

    .lang__menu { left: 50%; right: auto; transform: translate(-50%, -6px); }
    .lang[data-open="true"] .lang__menu { transform: translate(-50%, 0); }

    /*
      Факты в две ровные колонки.

      Гибкая строка переносила их по содержимому, и карточки выходили
      разной ширины: «Текущая версия» узкая, «Обновлена» во весь экран.
      Читается это как сбитая вёрстка, хотя каждая карточка сама по себе
      правильная. Сетка выравнивает их по одной мерке.
    */
    .facts {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 10px;
    }

    .fact { min-width: 0; padding: 12px 14px; }

    /*
      Кегль значений уменьшен, чтобы дата влезала в половину строки.

      Сначала я отдал дате всю ширину — и получил «Текущую версию» одну
      в первой строке с пустотой рядом: растянутая карточка выталкивает
      соседку. Пятнадцатью пикселями «24 августа 2026» помещается, и все
      четыре карточки ложатся ровным квадратом.
    */
    .fact__value { font-size: 15px; }

    /* Кнопка во всю ширину: на телефоне промахнуться мимо неё нельзя. */
    /* Содержимое блока скачивания тоже по центру: в основе у него
       align-items: flex-start, и без этого кнопка с подписями остаётся
       прижатой влево под центрированным логотипом. */
    .get {
      align-self: stretch;
      width: 100%;
      align-items: center;
      text-align: center;
    }
    .get__button { display: block; text-align: center; }

    /* Подвал в столбик — в строку две надписи не помещаются. */
    .foot { flex-direction: column; gap: 8px; }
  }

  /*
    Совсем узкие экраны: факты в один столбец.

    Порог 340, а не 400: на обычных 375 две карточки помещаются свободно,
    а в один столбец четыре штуки растягивали страницу вдвое — до ленты
    версий приходилось прокручивать пустоту.
  */
  @media (max-width: 340px) {
    .facts { grid-template-columns: 1fr; }
    .fact:nth-child(2) { grid-column: auto; }
    .head__logo { width: 210px; }
  }

  @media (prefers-reduced-motion: reduce) {
    * { animation: none !important; transition: none !important; }

    /* Ролик прячем целиком: под ним остаётся неподвижный кадр-постер. */
    .hero__video { display: none; }
    .hero { background: url("menu-bg.jpg") center / cover no-repeat; }
  }
</style>

<div class="page">
  <div class="hero" aria-hidden="true">
    <video class="hero__video" autoplay muted loop playsinline preload="metadata"
           poster="menu-bg.jpg">
      <source src="menu-bg.mp4" type="video/mp4">
    </video>
    <div class="hero__shade"></div>
  </div>

  <div class="lang" data-open="false">
    <button class="lang__button" type="button" aria-haspopup="listbox" aria-expanded="false">
      <span class="lang__code" data-lang-current>RU</span>
      <span class="lang__arrow" aria-hidden="true"></span>
    </button>

    <ul class="lang__menu" role="listbox" aria-label="Язык страницы">
      <li><button class="lang__option" type="button" role="option" data-lang="ru" aria-selected="true"><span class="lang__code">RU</span> Русский</button></li>
      <li><button class="lang__option" type="button" role="option" data-lang="en" aria-selected="false"><span class="lang__code">EN</span> English</button></li>
      <li><button class="lang__option" type="button" role="option" data-lang="uk" aria-selected="false"><span class="lang__code">UA</span> Українська</button></li>
    </ul>
  </div>

  <header class="head">
    ${logo ? '<img class="head__logo" src="' + logo + '" alt="Приключения разбойника Жени">' : ''}
    <div class="head__text">
      <p class="head__eyebrow" data-i18n="eyebrow">Хроника изменений</p>
      <h1 class="head__title" data-i18n="game">Приключения разбойника Жени</h1>
      <p class="head__lede" data-i18n="lede">Что менялось в игре от сборки к сборке. Записи идут
      от новых к старым — сверху то, что в игре прямо сейчас.</p>
    </div>

    <div class="get">
      <a class="get__button" href="${files.setupUrl}" data-i18n="download">Скачать игру</a>
      <p class="get__meta"><span data-i18n="installer">Установщик для Windows</span>${files.setup ? ', ' + files.setup + ' <span data-i18n="mb">МБ</span>' : ''}</p>
      <p class="get__alt"><span data-i18n="or">или</span> <a href="${files.archiveUrl}" data-i18n="as-archive">архивом</a>${files.archive ? ', ' + files.archive + ' <span data-i18n="mb">МБ</span>' : ''} <span data-i18n="no-install">— без установки</span></p>
    </div>
  </header>

  <div class="sheet">
  <div class="facts">
    <div class="fact fact--now">
      <p class="fact__label" data-i18n="fact-version">Текущая версия</p>
      <p class="fact__value">${escapeHtml(latest.version)}</p>
    </div>
    <div class="fact fact--date">
      <p class="fact__label" data-i18n="fact-updated">Обновлена</p>
      <p class="fact__value">${escapeHtml(latest.date || '—')}</p>
    </div>
    <div class="fact">
      <p class="fact__label" data-i18n="fact-releases">Версий выпущено</p>
      <p class="fact__value">${releases.length}</p>
    </div>
    <div class="fact">
      <p class="fact__label" data-i18n="fact-changes">Изменений всего</p>
      <p class="fact__value">${total}</p>
    </div>
  </div>

  <main class="releases">
${releases.map(renderRelease).join(NL)}
  </main>

  </div>

  <footer class="foot">
    <span data-i18n="foot">Собрано из <code>CHANGELOG.md</code> — того же файла, что читают игра и лаунчер.</span>
    <span>OXBOX</span>
  </footer>
</div>

<script>
/*
  Перевод интерфейса страницы.

  Тексты самих патчей остаются на языке, на котором написаны, — они приходят
  из CHANGELOG.md, и переводить их пришлось бы заново при каждом выпуске.
  Переключатель отвечает за обвязку: заголовки, кнопки, подписи.
*/
(function () {
  var DICT = {
    en: {
      title: 'Chronicle of Zhenya the Rogue',
      game: 'The Adventures of Zhenya the Rogue',
      eyebrow: 'Change log',
      lede: 'What changed in the game from build to build. Newest entries first — the top one is what is in the game right now.',
      download: 'Download the game',
      installer: 'Windows installer',
      mb: 'MB',
      or: 'or',
      'as-archive': 'as an archive',
      'no-install': '— no installation',
      'fact-version': 'Current version',
      'fact-updated': 'Updated',
      'fact-releases': 'Releases',
      'fact-changes': 'Changes in total',
      current: 'current',
      'section-add': 'Added',
      'section-fix': 'Fixed',
      'section-change': 'Changed',
      foot: 'Built from <code>CHANGELOG.md</code> — the same file the game and the launcher read.'
    },
    uk: {
      title: 'Хроніка розбійника Жені',
      game: 'Пригоди розбійника Жені',
      eyebrow: 'Хроніка змін',
      lede: 'Що змінювалося у грі від збірки до збірки. Записи йдуть від нових до старих — згори те, що у грі просто зараз.',
      download: 'Завантажити гру',
      installer: 'Інсталятор для Windows',
      mb: 'МБ',
      or: 'або',
      'as-archive': 'архівом',
      'no-install': '— без встановлення',
      'fact-version': 'Поточна версія',
      'fact-updated': 'Оновлено',
      'fact-releases': 'Версій випущено',
      'fact-changes': 'Змін усього',
      current: 'поточна',
      'section-add': 'Додано',
      'section-fix': 'Виправлено',
      'section-change': 'Змінено',
      foot: 'Зібрано з <code>CHANGELOG.md</code> — того самого файлу, що читають гра й лаунчер.'
    }
  };

  var root = document.querySelector('.lang');
  if (!root) return;

  var button = root.querySelector('.lang__button');
  var label = root.querySelector('[data-lang-current]');
  var options = root.querySelectorAll('.lang__option');
  var marks = document.querySelectorAll('[data-i18n]');

  // Русский оригинал запоминаем при загрузке: обратно к нему нужно уметь
  // вернуться, а второго словаря на русский заводить незачем.
  var original = {};
  for (var i = 0; i < marks.length; i++) {
    original[marks[i].getAttribute('data-i18n')] = marks[i].innerHTML;
  }

  var CODES = { ru: 'RU', en: 'EN', uk: 'UA' };

  /*
    Даты переводим отдельно: они приходят из CHANGELOG.md обычным текстом
    и в словарь не попадают — месяцев двенадцать, а дат в истории десятки.
  */
  var MONTHS = {
    'января': ['January', 'січня'], 'февраля': ['February', 'лютого'],
    'марта': ['March', 'березня'], 'апреля': ['April', 'квітня'],
    'мая': ['May', 'травня'], 'июня': ['June', 'червня'],
    'июля': ['July', 'липня'], 'августа': ['August', 'серпня'],
    'сентября': ['September', 'вересня'], 'октября': ['October', 'жовтня'],
    'ноября': ['November', 'листопада'], 'декабря': ['December', 'грудня']
  };

  var dates = document.querySelectorAll('.release__date, .fact--date .fact__value');
  var dateOriginal = [];

  for (var d = 0; d < dates.length; d++) dateOriginal.push(dates[d].textContent);

  function applyDates(code) {
    var column = code === 'en' ? 0 : 1;

    for (var i = 0; i < dates.length; i++) {
      var text = dateOriginal[i];

      if (code !== 'ru') {
        for (var month in MONTHS) {
          if (text.indexOf(month) >= 0) {
            text = text.replace(month, MONTHS[month][column]);
            break;
          }
        }
      }

      dates[i].textContent = text;
    }
  }

  function apply(code) {
    var dict = DICT[code];

    for (var i = 0; i < marks.length; i++) {
      var key = marks[i].getAttribute('data-i18n');
      var value = dict && dict[key] ? dict[key] : original[key];

      if (value !== undefined) marks[i].innerHTML = value;
    }

    applyDates(code);

    if (dict && dict.title) document.title = dict.title;
    else document.title = 'Хроника разбойника Жени';

    document.documentElement.lang = code;
    label.textContent = CODES[code] || 'RU';

    for (var k = 0; k < options.length; k++) {
      options[k].setAttribute('aria-selected',
        options[k].getAttribute('data-lang') === code ? 'true' : 'false');
    }

    try { localStorage.setItem('site-lang', code); } catch (error) { /* приватный режим */ }
  }

  function open(state) {
    root.setAttribute('data-open', state ? 'true' : 'false');
    button.setAttribute('aria-expanded', state ? 'true' : 'false');
  }

  button.addEventListener('click', function (event) {
    event.stopPropagation();
    open(root.getAttribute('data-open') !== 'true');
  });

  for (var j = 0; j < options.length; j++) {
    options[j].addEventListener('click', function () {
      apply(this.getAttribute('data-lang'));
      open(false);
      button.focus();
    });
  }

  // Клик мимо и Esc закрывают список: открытый список, который нельзя
  // закрыть иначе как выбором, — ловушка.
  document.addEventListener('click', function () { open(false); });

  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') open(false);
  });

  var saved = null;
  try { saved = localStorage.getItem('site-lang'); } catch (error) { /* приватный режим */ }

  if (saved && (saved === 'ru' || saved === 'en' || saved === 'uk')) apply(saved);
})();
</script>
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
prepareFavicon();

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, buildPage(releases));

const size = (fs.statSync(OUT).size / 1024).toFixed(0);

console.log('Страница собрана: ' + OUT);
console.log('Версий: ' + releases.length + ', размер: ' + size + ' КБ');
