// Изгиб травы по рельефу: правка шейдера растительности Synty.
//
// Куст травы у нас плоский и широкий — высота метр, поперечник четыре с
// половиной. На склоне под ним перепад земли до двух метров, и жёсткая
// плита лечь на такую поверхность не может: утопишь по центру — утонет
// целиком, посадишь по краю — задерётся. Лечится только изгибом геометрии.
//
// Шейдер получает карту высот глобальной текстурой (её ставит компонент
// TerrainConform) и опускает каждую вершину туда, где под ней земля.
// Смещение считается ОТНОСИТЕЛЬНО точки отсчёта объекта: куст повторяет
// форму склона, но не уезжает с места и не растягивается.
//
// Правка идёт во ВСЕ проходы. Если изогнуть только видимую геометрию,
// тени и глубина останутся от прямой — трава начнёт отбрасывать тень
// мимо себя.
//
// Запуск: node tools/patch-vegetation-shader.mjs

import { readFileSync, writeFileSync, copyFileSync, existsSync } from "node:fs";

const file =
  "D:/GAME Ai/IsoRPG/Assets/PolygonNatureBiomes/PNB_Core/Shaders/SyntyStudios_VegitationShader.shader";

const backup = file + ".before-conform";

let t = readFileSync(file, "utf8");

if (t.includes("_PNBHeightMap")) {
  console.log("Шейдер уже правлен — ничего не делаю.");
  process.exit(0);
}

// Копия до правки: шейдер лежит в игнорируемой папке набора, git его не
// вернёт, и откатывать придётся руками.
if (!existsSync(backup)) {
  copyFileSync(file, backup);
  console.log("Сделана копия:", backup.split("/").pop());
}

const nl = t.includes("\r\n") ? "\r\n" : "\n";
const fix = (s) => s.split("\n").join(nl);

// --- 1. Свойство материала ---------------------------------------------------
const propAnchor = `		_AlphaClip("AlphaClip", Float) = 0.5`;
if (!t.includes(propAnchor)) throw new Error("ЯКОРЬ СВОЙСТВ НЕ НАЙДЕН");

t = t.replace(
  propAnchor,
  propAnchor +
    fix(`
		[Header(Ground)]_ConformStrength("Прижатие к рельефу (0 выкл, 1 полностью)", Range(0, 1)) = 0`)
);
console.log("  свойство _ConformStrength: добавлено");

// --- 2. Объявления в каждом проходе -----------------------------------------
const cbuf = "CBUFFER_START(UnityPerMaterial)";
const cbufCount = t.split(cbuf).length - 1;
if (cbufCount === 0) throw new Error("CBUFFER НЕ НАЙДЕН");

t = t.split(cbuf).join(
  fix(`TEXTURE2D(_PNBHeightMap);
			SAMPLER(sampler_PNBHeightMap);
			float4 _PNBTerrainPos;
			float4 _PNBTerrainSize;

			CBUFFER_START(UnityPerMaterial)
			float _ConformStrength;`)
);
console.log(`  объявления в проходах: ${cbufCount}`);

// --- 3. Смещение вершин ------------------------------------------------------
// Блок одинаков во всех проходах, поэтому меняем разом.
const vtx = `				#ifdef ASE_ABSOLUTE_VERTEX_POS
					v.positionOS.xyz = vertexValue;
				#else
					v.positionOS.xyz += vertexValue;
				#endif`;

const vtxCrlf = fix(vtx);
const vtxCount = t.split(vtxCrlf).length - 1;
if (vtxCount === 0) throw new Error("БЛОК СМЕЩЕНИЯ ВЕРШИН НЕ НАЙДЕН");

const conform = fix(`
				// Изгиб по рельефу.
				//
				// Вершину опускаем на разницу высот земли под ней и земли под
				// точкой отсчёта объекта. Так куст повторяет склон, оставаясь
				// на своём месте: сам по себе он не едет и не растягивается.
				//
				// Проверка на размер участка обязательна: пока компонент не
				// отдал карту, размер нулевой, и без проверки мы поделили бы
				// на ноль и разбросали траву по всей сцене.
				if (_ConformStrength > 0.001 && _PNBTerrainSize.x > 0.001)
				{
					float3 conformWorld = TransformObjectToWorld(v.positionOS.xyz);
					float3 conformOrigin = TransformObjectToWorld(float3(0, 0, 0));

					float2 conformUvV = (conformWorld.xz - _PNBTerrainPos.xz) / _PNBTerrainSize.xz;
					float2 conformUvO = (conformOrigin.xz - _PNBTerrainPos.xz) / _PNBTerrainSize.xz;

					float conformHv = SAMPLE_TEXTURE2D_LOD(_PNBHeightMap, sampler_PNBHeightMap, conformUvV, 0).r;
					float conformHo = SAMPLE_TEXTURE2D_LOD(_PNBHeightMap, sampler_PNBHeightMap, conformUvO, 0).r;

					conformWorld.y += (conformHv - conformHo) * _PNBTerrainSize.y * _ConformStrength;

					v.positionOS.xyz = TransformWorldToObject(conformWorld);
				}`);

t = t.split(vtxCrlf).join(vtxCrlf + conform);
console.log(`  изгиб вставлен в проходов: ${vtxCount}`);

writeFileSync(file, t, "utf8");
console.log("Шейдер записан.");
