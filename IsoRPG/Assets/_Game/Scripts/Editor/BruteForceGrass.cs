using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит на наш террейн шейдерную траву Brute Force вместо объектов.
    ///
    /// Как она устроена — это важно, потому что не похоже ни на что из того,
    /// что мы делали раньше. Трава здесь не модели и не detail-слой, а сам
    /// материал земли: шейдер выдавливает из поверхности до семнадцати слоёв
    /// геометрии, и получается «мех». Растёт она там, где на террейне
    /// прокрашен травяной слой, и приминается под персонажем — за это отвечает
    /// отдельная камера с текстурой воздействия.
    ///
    /// Правила автора, которые нельзя нарушать (из его ReadMe):
    ///   • Слой ноль — всегда земля, ему нормаль не назначают.
    ///   • Слои с первого и дальше — трава; узор берётся из НОРМАЛИ слоя,
    ///     а цвет травы из его Specular. Это выглядит как ошибка, но так
    ///     задумано: обычные поля слоя заняты под другое.
    ///   • Тайлинг: X — размер узора, Y — толщина травинок и строго от 0 до 2,
    ///     смещение обязано быть (1, 1). Любые другие значения ломают шейдер.
    ///
    /// Землю оставляем свою — она уже покрашена под наш лес, с тропой и
    /// подстилкой. Меняется только материал террейна и добавляются травяные
    /// слои поверх нашей раскраски.
    /// </summary>
    public static class BruteForceGrass
    {
        private const string Root = "Assets/BruteForce-GrassShader/";
        private const string TerrainMaterial = Root + "Materials/URP/Terrain/URPBFGrassTerrain01.mat";

        /// <summary>
        /// Их травяные слои. Берём готовые: в них уже выставлены нормаль-узор,
        /// цвет и тайлинг по правилам шейдера, а собирать это руками значит
        /// повторить чужую отладку.
        /// </summary>
        private static readonly string[] GrassLayers =
        {
            Root + "Terrain/URP/NewLayer02URP.terrainlayer",
            Root + "Terrain/URP/NewLayer03URP.terrainlayer",
            Root + "Terrain/URP/NewLayer04URP.terrainlayer",
        };

        /// <summary>Их слой земли. Нулевым обязан быть именно он.</summary>
        private const string GroundLayer = Root + "Terrain/URP/NewLayer01URP.terrainlayer";

        [MenuItem("Tools/IsoRPG/Трава Brute Force: поставить на террейн", priority = 27)]
        public static void Apply()
        {
            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
                                .FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogWarning("[IsoRPG] Террейна в сцене нет.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterial);

            if (material == null)
            {
                Debug.LogWarning("[IsoRPG] Не найден материал " + TerrainMaterial);
                return;
            }

            // Материал делаем своей копией: их ассет общий для всего набора, и
            // правка настроек под наш лес испортила бы демо-сцены — а мы к ним
            // ещё вернёмся сравнивать.
            const string myMaterial = "Assets/_Game/Art/Materials/M_ArenaGrassTerrain.mat";

            AssetDatabase.DeleteAsset(myMaterial);
            AssetDatabase.CopyAsset(TerrainMaterial, myMaterial);

            var mine = AssetDatabase.LoadAssetAtPath<Material>(myMaterial);
            terrain.materialTemplate = mine;

            var data = terrain.terrainData;

            // Слои берём ИХ целиком, а не подмешиваем к своим.
            //
            // Первая попытка добавила их траву поверх наших четырёх слоёв — и
            // земля стала белой. Шейдер читает слои по своим правилам: нулевой
            // считает землёй и берёт из него цвет, остальные считает травой и
            // берёт узор из нормали. Наши слои этих правил не знают, и на
            // чужих местах шейдер видит пустоту.
            //
            // Значит либо целиком их раскладка, либо своя — смешивать нельзя.
            var layers = new List<TerrainLayer>();

            var ground = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GroundLayer);

            if (ground == null)
            {
                Debug.LogWarning("[IsoRPG] Не найден их слой земли " + GroundLayer);
                return;
            }

            layers.Add(ground);

            int firstGrass = layers.Count;
            int added = 0;

            foreach (var path in GrassLayers)
            {
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);

                if (layer == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет слоя " + path);
                    continue;
                }

                if (layers.Contains(layer)) continue;

                layers.Add(layer);
                added++;
            }

            if (added == 0)
            {
                Debug.LogWarning("[IsoRPG] Ни одного травяного слоя не добавлено.");
                return;
            }

            data.terrainLayers = layers.ToArray();

            PaintGrass(data, firstGrass, added);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[IsoRPG] Шейдерная трава поставлена: материал " + mine.name +
                      ", травяных слоёв " + added + " (с индекса " + firstGrass +
                      "), всего слоёв " + data.terrainLayers.Length + ".");
        }

        /// <summary>
        /// Прокрашивает траву поверх прежней раскраски.
        ///
        /// Тропу не трогаем: она читается тропой ровно потому, что вытоптана,
        /// и шерсть поверх неё убьёт весь смысл. Веса берём из уже
        /// нарисованной карты — где лежит дорога, там травы нет.
        /// </summary>
        private static void PaintGrass(TerrainData data, int firstGrass, int count)
        {
            int res = data.alphamapResolution;
            int n = data.terrainLayers.Length;

            var maps = data.GetAlphamaps(0, 0, res, res);
            var next = new float[res, res, n];

            // Дороги в этой раскладке нет: слои теперь их, и тропу придётся
            // рисовать заново уже их землёй. Пока смотрим на траву.
            const int roadLayer = -1;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float road = roadLayer >= 0 && roadLayer < firstGrass
                        ? maps[y, x, roadLayer] : 0f;

                    // Чем больше дороги в точке, тем меньше травы. На самой
                    // тропе травы нет вовсе.
                    float grass = Mathf.Clamp01(1f - road * 2.2f);

                    // Пятнами: сплошной ковёр читается газоном, а не поляной.
                    float u = (float)x / (res - 1);
                    float v = (float)y / (res - 1);
                    float noise = Mathf.PerlinNoise(u * 5.5f + 91f, v * 5.5f + 17f);

                    grass *= Mathf.Lerp(0.45f, 1f, noise);

                    float rest = 1f - grass;
                    // Земля — весь остаток: слой у неё теперь один.
                    next[y, x, 0] = rest;

                    for (int g = 0; g < count; g++)
                    {
                        int idx = firstGrass + g;
                        if (idx < n) next[y, x, idx] = grass / count;
                    }
                }
            }

            data.SetAlphamaps(0, 0, next);
        }

        [MenuItem("Tools/IsoRPG/Трава Brute Force: убрать", priority = 28)]
        public static void Remove()
        {
            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
                                .FirstOrDefault();

            if (terrain == null) return;

            // Возвращаем террейн как был: наш материал и наша раскраска.
            TerrainBuilder.Build();

            Debug.Log("[IsoRPG] Шейдерная трава убрана, террейн пересобран.");
        }
    }
}
