// Чинит шейдер воды Synty под нашу версию URP.
//
// Болезнь. `SyntyStudios/WaterShader` объявляет `_CameraDepthTexture_TexelSize`
// сам, семь раз — по разу на проход. В URP той версии, под которую набор
// писался, этой переменной в библиотеке не было. В нашей есть, и компилятор
// честно ругается: `redefinition of '_CameraDepthTexture_TexelSize'`.
//
// Как это выглядит в игре. НЕ розовым — шейдер с ошибкой компиляции не
// подменяется аварийным материалом, а просто не рисует ничего. Вода
// исчезает, оставляя сухое русло с камнями и живыми брызгами поверх.
// Павлон 03.09.2026: «у этой речки брызги есть, а воды в канаве нет».
// Именно поэтому прошлая сессия заливала реку нашим материалом: она
// обходила эту ошибку, не назвав её.
//
// Лечение. URP объявляет ту же переменную в `DeclareDepthTexture.hlsl` под
// стражем `UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED`. Оборачиваем авторское
// объявление тем же стражем: библиотека включена — берём её переменную,
// не включена — объявляем сами. Верно в обоих случаях, а значит и в тех
// трёх проходах шейдера, где ошибки не было.
//
// Почему скриптом, а не руками. Файл лежит в папке набора, а наборы Synty
// несут общее ядро `PNB_Core` внутри каждого биома: импорт соседнего биома
// перезапишет шейдер и унесёт правку. Скрипт идемпотентен — гонять после
// любого импорта, проверка занимает секунду.

import { readFileSync, writeFileSync } from "node:fs";

const SHADER =
  "D:/GAME Ai/IsoRPG/Assets/PolygonNatureBiomes/PNB_Core/Shaders/SyntyStudios_WaterShader.shader";

const DECL = "uniform float4 _CameraDepthTexture_TexelSize;";
const GUARD = "UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED";

const text = readFileSync(SHADER, "utf8");

// Идемпотентность проверяем по СТРАЖУ, а не по имени переменной: имя есть в
// файле и до правки, и после неё, поэтому проверка по нему всегда говорила бы
// «уже сделано».
if (text.includes(GUARD)) {
  console.log("Шейдер воды уже пропатчен — страж на месте, ничего не меняю.");
  process.exit(0);
}

const before = text.split(DECL).length - 1;

if (before === 0) {
  console.error("Не нашёл объявления в шейдере — набор изменился, править надо заново.");
  process.exit(1);
}

const patched = text.replaceAll(
  DECL,
  `#ifndef ${GUARD}\n\t\t\t${DECL}\n\t\t\t#endif`
);

writeFileSync(SHADER, patched, "utf8");

console.log(`Шейдер воды пропатчен: объявлений обёрнуто ${before}.`);
