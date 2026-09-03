// Тянет описания умений с wowhead и складывает их в data/classes.json.
//
// Описания берём готовыми, а не пишем своими словами: игрок сверяет их с
// тем, что видит в тултипе игры, и пересказ там сразу заметен. Источник
// один — карточка заклинания на wowhead, первый ранг умения.
//
// Иконки и порядок умений задаются здесь же, в LIST: это наша часть,
// wowhead про наши рисунки ничего не знает.
//
// Запуск:
//   node tools/fetch-abilities.mjs

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..');
const OUT = path.join(repo, 'data', 'classes.json');

const ROGUE = [
  { id: 1784, icon: 'Skill_stealth' },
  { id: 1752, icon: 'Skill_sinisterstrike' },
  { id: 2098, icon: 'Skill_eviscerate' },
  { id: 1804, icon: 'Skill_picklock' },
  { id: 921, icon: 'Skill_pickpocket' },
  { id: 5277, icon: 'Skill_evasion' },
  { id: 6770, icon: 'Skill_sap' },
  { id: 2983, icon: 'Skill_sprint' },
  { id: 703, icon: 'Skill_garrote' },
  { id: 8647, icon: 'Skill_exposearmor' },
  { id: 1943, icon: 'Skill_rupture' },
  { id: 2842, icon: 'Skill_poisons' },
  { id: 1856, icon: 'Skill_vanish' },
  { id: 1725, icon: 'Skill_distract' },
  { id: 408, icon: 'Skill_kidneyshot' },
  { id: 1842, icon: 'Skill_disarmtrap' },
  { id: 2094, icon: 'Skill_blind' },
  { id: 6510, icon: 'Skill_blindingpowder' },
];

const NL = String.fromCharCode(10);

function strip(html) {
  return html
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(/<br\s*\/?>/gi, NL)
    .replace(/<\/(div|td|tr|table|span)>/gi, NL)
    .replace(/<[^>]+>/g, '')
    .replace(/&nbsp;/g, ' ')
    .replace(/&quot;/g, '"')
    .replace(/&amp;/g, '&');
}

/**
 * Шапка тултипа: стоимость, дистанция, время применения, восстановление.
 *
 * В отдаваемом html они лежат ячейками таблицы без разделителей, поэтому
 * вытаскиваем по образцам, а не по разметке — так одинаково работает и для
 * боевых приёмов, и для умений вроде взлома замка, где ячеек другой набор.
 */
function header(text) {
  const find = (re) => { const m = text.match(re); return m ? m[0].trim() : ''; };

  return {
    cost: find(/Энергия: \d+/),
    range: find(/Дистанция ближнего боя|Радиус действия: [\d.,]+ м/),
    cast: find(/Мгновенное действие|Применение: [\d.,]+ сек/),
    cooldown: find(/Восстановление: [\d.,]+ (?:сек|мин)/),
  };
}

async function tooltip(id) {
  const res = await fetch(`https://nether.wowhead.com/classic/ru/tooltip/spell/${id}`);
  if (!res.ok) throw new Error('wowhead ответил ' + res.status + ' на ' + id);

  return res.json();
}

/**
 * Склейка шапки: имя умения, к которому без пробела приклеены ячейки
 * таблицы. Отличать её надо аккуратно — описание часто начинается с того же
 * слова («Ослепление цели, вследствие чего...»), и грубая проверка по
 * началу строки выбрасывала само описание.
 */
function isHeaderBlob(line, name) {
  if (!name || !line.startsWith(name)) return false;

  return /^(Энергия|Дистанция|Радиус действия|Мгновенное действие|Применение|Восстановление|Требуется)/
    .test(line.slice(name.length));
}

function parse(json) {
  const html = (json.tooltip || '').replace(/<!--[\s\S]*?-->/g, '');
  const flat = strip(html);

  const lines = flat.split(NL).map((s) => s.trim()).filter(Boolean);

  // Первая строка — название, дальше идут ячейки шапки, требования и текст.
  const name = json.name || lines[0] || '';

  const reqs = lines.filter((s) => /^Требуется/.test(s));

  // Инструменты и ингредиенты стоят отдельной строкой без двоеточия —
  // одно-два слова перед описанием. Их и отделяем, иначе они выглядят
  // как первая фраза описания: «Отмычки Позволяет открывать...».
  // Первой строкой приходит вся шапка одной склейкой — она начинается с
  // названия умения. Отбрасываем её вместе с ячейками, которые уже разобраны
  // по полям, иначе «Рваная ранаЭнергия: 25» уедет в описание.
  const body = lines.filter((s) => (
    !/^Требуется/.test(s)
    && !isHeaderBlob(s, name)
    && !/^(Энергия|Дистанция|Радиус действия|Мгновенное действие|Применение|Восстановление)/.test(s)
  ));

  // Инструменты и ингредиенты приходят заголовком и значением на разных
  // строках: «Инструменты:» и следом «Отмычки». Склеиваем, иначе они
  // читаются как первая фраза описания.
  let tools = '';
  if (/^(Инструменты|Ингредиенты|Реагенты):$/.test(body[0] || '')) {
    tools = body.shift() + ' ' + (body.shift() || '');
  } else if (body.length > 1 && body[0].split(' ').length <= 3 && !/[.!:]$/.test(body[0])) {
    tools = body.shift();
  }

  const level = (reqs.find((s) => /\d+-й ур/.test(s)) || '').match(/\d+/);

  return {
    name,
    level: level ? Number(level[0]) : 0,
    ...header(flat),
    reqs: reqs.filter((s) => !/\d+-й ур/.test(s) && !/Разбойник/.test(s)),
    tools,
    desc: body,
  };
}

const abilities = [];

for (const entry of ROGUE) {
  const data = parse(await tooltip(entry.id));
  abilities.push({ icon: entry.icon, id: entry.id, ...data });
  console.log(data.level + '  ' + data.name);
}

const classes = [{
  id: 'rogue',
  name: 'Разбойник',
  lede: 'Бьёт из тени, живёт энергией и серией приёмов. Урон копится ударами по одному и тратится завершающим приёмом — чем длиннее серия, тем больнее финал.',
  abilities: abilities.sort((a, b) => a.level - b.level),
}];

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, JSON.stringify(classes, null, 2));

console.log('Умений: ' + abilities.length + ' → ' + OUT);
