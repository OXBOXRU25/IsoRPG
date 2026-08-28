using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Луг по числам автора: трава, цветы, подсолнухи, камни, фруктовые деревья.
    ///
    /// <b>Все числа сняты со сцены Demo лугового биома Synty</b> щупом
    /// PnbAnalyze, ничего не выдумано. Террейн у автора 400x400 м; плотности
    /// пересчитываются на нашу площадку.
    ///
    /// <b>Важная поправка к моему прежнему предположению:</b> у автора
    /// слоёв подлеска на террейне НЕТ ВООБЩЕ — вся трава стоит префабами.
    /// Я собирался сеять её картой плотности террейна и был неправ.
    ///
    /// Каждый вид несёт свой размерный ряд и свою посадку в землю — они у
    /// автора разные и это не случайность: высокая трава утоплена на метр с
    /// лишним, цветы почти не утоплены, плоские цветы наоборот приподняты
    /// над землёй.
    /// </summary>
    public static class SyntyMeadow
    {
        private const string Holder = "Луг Synty";
        private const string Biome = "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs";

        /// <summary>Игровая площадка, метров. Та же, по которой печётся навигация.</summary>
        private const float Field = 160f;

        /// <summary>Вид растения с авторскими числами.</summary>
        private readonly struct Kind
        {
            public readonly string Name;
            public readonly float Per100;    // штук на 100 м²
            public readonly float MinScale, MaxScale;
            public readonly float Sink;      // насколько низ уходит под землю, м

            public Kind(string name, float per100, float min, float max, float sink)
            {
                Name = name; Per100 = per100; MinScale = min; MaxScale = max; Sink = sink;
            }
        }

        // Таблица снята с Demo лугового биома. Порядок — по убыванию плотности.
        private static readonly Kind[] Table =
        {
            new Kind("SM_Env_Grass_Tall_Clump_04",  1.21f, 0.60f, 2.92f, 0.58f),
            new Kind("SM_Env_Grass_Tall_Clump_05",  1.10f, 0.63f, 1.47f, 1.27f),
            new Kind("SM_Env_Grass_Med_Clump_02",   0.74f, 0.61f, 1.32f, 0.22f),
            new Kind("SM_Env_Grass_Med_Clump_03",   0.73f, 0.60f, 1.34f, 0.25f),
            new Kind("SM_Env_Grass_Short_Clump_03", 0.65f, 0.76f, 1.28f, 0.27f),
            new Kind("SM_Env_Grass_Tall_Clump_03",  0.33f, 0.61f, 3.16f, 0.41f),
            new Kind("SM_Env_Grass_Tall_Clump_02",  0.25f, 0.72f, 2.24f, 0.39f),
            new Kind("SM_Env_Wildflowers_03",       0.22f, 0.58f, 1.30f, 0.10f),
            new Kind("SM_Env_Wildflowers_02",       0.19f, 0.61f, 1.28f, 0.11f),
            new Kind("SM_Env_Tree_Fruit_01",        0.24f, 0.65f, 2.22f, 0.96f),
            new Kind("SM_Env_Tree_Fruit_02",        0.14f, 0.70f, 1.41f, 1.00f),
            new Kind("SM_Env_Tree_Fruit_03",        0.13f, 0.67f, 1.61f, 0.68f),
            new Kind("SM_Env_Sunflower_01",         0.09f, 0.61f, 1.26f, 0.06f),
            new Kind("SM_Env_Wildflowers_01",       0.08f, 0.70f, 1.05f, 0.12f),
            new Kind("SM_Env_Grass_Bush_01",        0.08f, 0.80f, 1.70f, 0.58f),
            new Kind("SM_Env_Rock_02",              0.07f, 0.15f, 1.22f, 0.89f),
            new Kind("SM_Env_Rock_01",              0.06f, 0.15f, 1.59f, 0.71f),
            new Kind("SM_Env_Rock_03",              0.06f, 0.10f, 1.28f, 1.02f),
            // SM_Env_Flowers_Flat_01 убран из посева: без стеблей на траве
            // читается кувшинками, а не цветами — так и увидел заказчик.
            // У автора они приподняты НАД землёй (утоплены на -0.36), что
            // подтверждает: это для воды. Вернуть, когда появится пруд.
        };

        public static void Sow()
        {
            Clear();

            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — сеять не на чем.");
                return;
            }

            var holder = new GameObject(Holder);
            float hundreds = Field * Field / 100f;

            Random.InitState(4207);

            int total = 0, kinds = 0;

            foreach (var k in Table)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Biome + "/" + k.Name + ".prefab");

                if (prefab == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет префаба " + k.Name);
                    continue;
                }

                int count = Mathf.RoundToInt(k.Per100 * hundreds);
                int placed = 0;

                for (int i = 0; i < count; i++)
                {
                    float x = Random.Range(-Field * 0.5f, Field * 0.5f);
                    float z = Random.Range(-Field * 0.5f, Field * 0.5f);

                    // Пятачок у начала координат оставляем чистым: там стоит
                    // герой, и куст в лицо на старте — не композиция.
                    if (new Vector2(x, z).magnitude < 6f) { i--; continue; }

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);

                    float ground = terrain.SampleHeight(new Vector3(x, 0f, z)) +
                                   terrain.transform.position.y;

                    go.transform.position = new Vector3(x, ground, z);
                    go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    go.transform.localScale = Vector3.one * Random.Range(k.MinScale, k.MaxScale);

                    Seat(go, ground, k.Sink);
                    Strip(go);

                    placed++;
                }

                total += placed;
                kinds++;

                Debug.Log("[IsoRPG] Луг: " + k.Name + " — " + placed + " шт.");
            }

            int objects = holder.GetComponentsInChildren<Transform>(true).Length;

            Debug.Log("[IsoRPG] Луг Synty посеян: видов " + kinds +
                      ", растений " + total +
                      ", объектов в сцене " + objects +
                      " (у префабов свои LOD-узлы).");

            EditorSceneManager.MarkAllScenesDirty();
        }

        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == Holder);

            if (old == null) return;

            Object.DestroyImmediate(old);
            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Прежний луг снят, сцена помечена грязной.");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Посадить по нижней грани нарисованного и утопить на авторскую
        /// величину. По точке отсчёта префаба сажать нельзя: у покупных
        /// наборов она где угодно, и один папоротник у меня уже висел в небе.
        /// </summary>
        private static void Seat(GameObject go, float ground, float sink)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;

            var box = rs[0].bounds;
            foreach (var r in rs) box.Encapsulate(r.bounds);

            go.transform.position += new Vector3(0f, ground - box.min.y - sink, 0f);
        }

        /// <summary>
        /// Снять коллайдеры с травы и цветов.
        ///
        /// По траве ходят. Коллайдер на каждом кустике — это полторы тысячи
        /// препятствий, о которые спотыкается и герой, и монстры, и при этом
        /// на навигационной сетке их не видно. Камням и деревьям коллайдеры
        /// оставляем: в них упираться и надо.
        /// </summary>
        private static void Strip(GameObject go)
        {
            string n = go.name.ToLowerInvariant();

            bool solid = n.Contains("rock") || n.Contains("tree");
            if (solid) return;

            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
        }
    }
}
