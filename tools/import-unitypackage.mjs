#!/usr/bin/env node
/**
 * Раскладка .unitypackage прямо в проект, без участия редактора.
 *
 * Зачем не через Unity: пакетов полтора десятка, каждый ставится
 * диалогом с полутора тысячами галочек, и всё это время редактор занят.
 * Формат при этом простейший — tar.gz, внутри по папке на ассет:
 *
 *     <guid>/asset        сам файл (у папок его нет)
 *     <guid>/asset.meta   метафайл, в нём тот же guid
 *     <guid>/pathname     куда класть, от корня проекта
 *
 * Раскладываем по pathname, .meta кладём рядом — GUID сохраняются, значит
 * префабы и материалы находят друг друга ровно так же, как после импорта
 * из меню.
 *
 *   node tools/import-unitypackage.mjs <файл.unitypackage> [ещё файлы...]
 *        --project <путь>   корень Unity-проекта (по умолчанию ./IsoRPG)
 *        --tmp <путь>       где распаковывать (по умолчанию D:/_unpack)
 *        --list             только показать, что внутри, ничего не класть
 *        --skip-existing    не трогать уже лежащие файлы
 */

import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const args = process.argv.slice(2);
const packages = [];
let project = 'D:/GAME Ai/IsoRPG';
let tmpRoot = 'D:/_unpack';
let listOnly = false;
let skipExisting = false;
let only = null;

for (let i = 0; i < args.length; i++) {
  const a = args[i];
  if (a === '--project') project = args[++i];
  else if (a === '--tmp') tmpRoot = args[++i];
  else if (a === '--list') listOnly = true;
  else if (a === '--skip-existing') skipExisting = true;
  else if (a === '--only') only = args[++i].toLowerCase();
  else packages.push(a);
}

if (packages.length === 0) {
  console.error('Укажи хотя бы один .unitypackage');
  process.exit(1);
}

const mb = (bytes) => (bytes / 1024 / 1024).toFixed(1) + ' МБ';

for (const pkg of packages) {
  if (!fs.existsSync(pkg)) {
    console.error('НЕТ ФАЙЛА: ' + pkg);
    continue;
  }

  const name = path.basename(pkg).replace(/\.unitypackage$/i, '');
  console.log('\n=== ' + name + ' (' + mb(fs.statSync(pkg).size) + ') ===');

  // Короткий путь распаковки специально: внутри наборов встречаются имена
  // под 150 символов, и на длинном временном пути Windows упирается в
  // предел в 260 — распаковка обрывается на середине без внятной ошибки.
  const tmp = path.join(tmpRoot, 'p' + Date.now().toString(36));
  fs.mkdirSync(tmp, { recursive: true });

  try {
    execFileSync('tar', ['--force-local', '-xzf', pkg, '-C', tmp], { stdio: 'inherit' });

    const entries = fs.readdirSync(tmp, { withFileTypes: true })
                      .filter((e) => e.isDirectory());

    let files = 0, dirs = 0, skipped = 0, bytes = 0, skippedManifest = 0;
    const roots = new Map();

    for (const entry of entries) {
      const dir = path.join(tmp, entry.name);
      const pathnameFile = path.join(dir, 'pathname');
      if (!fs.existsSync(pathnameFile)) continue;

      // В pathname бывает вторая строка (старый путь при переименовании) —
      // берём только первую.
      const rel = fs.readFileSync(pathnameFile, 'utf8').split('\n')[0].trim();
      if (!rel) continue;

      // --only: вернуть из пакета лишь часть файлов. Нужно, когда наш
      // же инструмент испортил материалы: исходники лежат в пакете, и
      // достать оттуда одни .mat дешевле, чем перекладывать весь набор.
      if (only && !rel.toLowerCase().includes(only)) continue;

      // Манифест пакетов НЕ трогаем никогда.
      //
      // 01.09.2026 переимпорт набора лошадей положил свой Packages/manifest.json
      // поверх нашего — и проект разом лишился URP и Input System: сотни
      // ошибок компиляции про «UnityEngine.Rendering.Universal не существует».
      // Выглядит это как «набор сломал проект», а на самом деле список
      // пакетов проекта заменён списком из чужого архива. Файл текстовый и
      // возвращается из git (`git checkout -- IsoRPG/Packages/manifest.json`),
      // но после него Unity ещё переустанавливает пакеты — это минуты.
      if (/^Packages\/(manifest|packages-lock)\.json$/i.test(rel)) {
        skippedManifest++;
        continue;
      }

      const top = rel.split('/').slice(0, 2).join('/');
      roots.set(top, (roots.get(top) || 0) + 1);

      if (listOnly) continue;

      const dest = path.join(project, rel);
      const assetFile = path.join(dir, 'asset');
      const metaFile = path.join(dir, 'asset.meta');

      if (fs.existsSync(assetFile)) {
        if (skipExisting && fs.existsSync(dest)) { skipped++; continue; }
        fs.mkdirSync(path.dirname(dest), { recursive: true });
        fs.copyFileSync(assetFile, dest);
        bytes += fs.statSync(assetFile).size;
        files++;
      } else {
        // Записи без asset — это папки. Их .meta нужен: без него Unity
        // заведёт свой GUID, и ссылки внутри набора на папку разъедутся.
        fs.mkdirSync(dest, { recursive: true });
        dirs++;
      }

      if (fs.existsSync(metaFile)) {
        fs.copyFileSync(metaFile, dest + '.meta');
      }
    }

    if (listOnly) {
      console.log('Содержимое по корневым папкам:');
      for (const [root, n] of [...roots].sort((a, b) => b[1] - a[1])) {
        console.log('  ' + String(n).padStart(6) + '  ' + root);
      }
    } else {
      console.log('Разложено: файлов ' + files + ', папок ' + dirs +
                  (skipped ? ', пропущено ' + skipped : '') +
                  (skippedManifest ? ', манифест пакетов не тронут' : '') +
                  ', объём ' + mb(bytes));
      console.log('Корни: ' + [...roots.keys()].join(', '));
    }
  } catch (err) {
    console.error('СБОЙ на ' + name + ': ' + err.message);
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}

console.log('\nГотово. Переключись в Unity — он подхватит новые файлы по фокусу.');
