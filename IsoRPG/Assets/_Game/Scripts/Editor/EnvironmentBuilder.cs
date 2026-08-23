using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает окружение: руины посреди леса.
    ///
    /// Расставляется скриптом, а не руками, по той же причине, что и вся
    /// сцена: композицию видно в одном месте, её можно поправить числом и
    /// пересобрать одинаково в любой момент. Руками разложенный лес живёт
    /// в бинарном файле сцены, и через месяц никто не помнит, почему дерево
    /// стоит именно там.
    ///
    /// Раскладка держится на трёх кругах:
    ///   • центр — руины, там дерутся;
    ///   • средний круг — редкие голые деревья и камни, там ходят;
    ///   • дальний — плотный лес, он закрывает край карты.
    ///
    /// Голые деревья ближе к руинам, живые снаружи: так место читается как
    /// «здесь что-то случилось», без единой надписи.
    /// </summary>
    public static class EnvironmentBuilder
    {
        private const string DungeonFolder = "Assets/_Game/Art/KayKit/Dungeon";
        private const string NatureFolder = "Assets/_Game/Art/KayKit/Nature";

        /// <summary>
        /// Зерно случайности. Фиксированное: пересборка сцены должна давать
        /// тот же лес, иначе нельзя ни сравнить два кадра, ни вернуться к
        /// раскладке, которая понравилась.
        /// </summary>
        private const int Seed = 20260823;

        private const float RuinsRadius = 13f;
        private const float ForestInner = 21f;
        private const float ForestOuter = 37f;

        public static void Build(Transform parent)
        {
            var state = Random.state;
            Random.InitState(Seed);

            var root = new GameObject("Environment").transform;
            root.SetParent(parent, false);

            BuildRuins(root);
            BuildForest(root);
            BuildUndergrowth(root);

            Random.state = state;
        }

        // ------------------------------------------------------------------
        // Руины
        // ------------------------------------------------------------------

        private static void BuildRuins(Transform root)
        {
            var ruins = new GameObject("Ruins").transform;
            ruins.SetParent(root, false);

            // Стена меряется по факту, а не берётся из предположения о сетке:
            // тогда куски стыкуются встык при любом наборе.
            float step = MeasureWidth(DungeonFolder + "/wall.fbx");
            if (step < 0.5f) step = 4f;

            // Прямоугольник стен с разрывами. Разрывы важнее самих стен: они
            // дают проходы, укрытия от стрел и места, где можно оторваться
            // от погони. Сплошная коробка была бы просто забором.
            var walls = new (string model, float x, float z, float rotY)[]
            {
                ("wall_broken",  -2f, 3f,   0f),
                ("wall",         -1f, 3f,   0f),
                ("wall_doorway",  0f, 3f,   0f),
                ("wall",          1f, 3f,   0f),
                ("wall_broken",   2f, 3f,   0f),

                ("wall_corner",  -2.5f, 2.5f, 0f),
                ("wall_corner",   2.5f, 2.5f, 270f),

                ("wall",        -2.5f, 1f,  90f),
                ("wall_broken", -2.5f, 0f,  90f),
                ("wall_half",    2.5f, 1f,  90f),
                ("wall_broken",  2.5f, -1f, 90f),

                ("wall_broken", -1f, -2.5f, 180f),
                ("wall_half",    1f, -2.5f, 180f),
            };

            foreach (var (model, x, z, rotY) in walls)
            {
                var go = Place(DungeonFolder + "/" + model + ".fbx", ruins,
                               new Vector3(x * step, 0f, z * step), rotY, 1f);

                // Стены обязаны быть непрозрачными для луча: на них держится
                // проверка линии огня у лучника.
                AddSolidCollider(go);
            }

            // Колонны по углам площадки — вертикали, за которые цепляется глаз.
            var pillars = new (float x, float z)[]
            {
                (-1.6f, 1.6f), (1.6f, 1.6f), (-1.6f, -1.6f), (1.6f, -1.6f),
            };

            foreach (var (x, z) in pillars)
            {
                var go = Place(DungeonFolder + "/pillar.fbx", ruins,
                               new Vector3(x * step, 0f, z * step),
                               Random.Range(0f, 360f), 1f);
                AddSolidCollider(go);
            }

            // Обломки: без них руины выглядят как недостроенный дом, а не как
            // разрушенный.
            for (int i = 0; i < 9; i++)
            {
                string model = Random.value < 0.5f ? "rubble_half" : "rubble_large";
                Vector2 flat = Random.insideUnitCircle * RuinsRadius;

                Place(DungeonFolder + "/" + model + ".fbx", ruins,
                      new Vector3(flat.x, 0f, flat.y), Random.Range(0f, 360f),
                      Random.Range(0.85f, 1.15f));
            }

            // Пара факелов у входа: единственный источник тепла в кадре.
            Place(DungeonFolder + "/torch_lit.fbx", ruins,
                  new Vector3(-0.45f * step, 0f, 3f * step), 0f, 1f);
            Place(DungeonFolder + "/torch_lit.fbx", ruins,
                  new Vector3(0.45f * step, 0f, 3f * step), 0f, 1f);
        }

        // ------------------------------------------------------------------
        // Лес
        // ------------------------------------------------------------------

        private static void BuildForest(Transform root)
        {
            var forest = new GameObject("Forest").transform;
            forest.SetParent(root, false);

            var living = CollectModels(NatureFolder, "Tree_", "Tree_Bare_");
            var bare = CollectModels(NatureFolder, "Tree_Bare_", null);

            if (living.Count == 0 && bare.Count == 0)
            {
                Debug.LogWarning("[IsoRPG] Не найдены модели деревьев — лес не собран.");
                return;
            }

            // Плотное кольцо по краю: оно закрывает границу земли, за которой
            // ничего нет. Без него карта заканчивается обрывом в пустоту.
            for (int i = 0; i < 190; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(ForestInner, ForestOuter);

                var pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                // Чем дальше от руин, тем больше живых деревьев.
                float bareChance = Mathf.InverseLerp(ForestOuter, ForestInner, radius);
                var pool = (bare.Count > 0 && Random.value < bareChance * 0.7f) ? bare : living;

                var go = Place(pool[Random.Range(0, pool.Count)], forest, pos,
                               Random.Range(0f, 360f), Random.Range(0.85f, 1.3f));

                AddTrunkCollider(go);
            }

            // Одиночные деревья в средней зоне: они разбивают пустое поле и
            // дают лучнику что обходить.
            for (int i = 0; i < 22; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(RuinsRadius + 2f, ForestInner);

                var pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                var pool = bare.Count > 0 ? bare : living;

                var go = Place(pool[Random.Range(0, pool.Count)], forest, pos,
                               Random.Range(0f, 360f), Random.Range(0.8f, 1.1f));

                AddTrunkCollider(go);
            }
        }

        // ------------------------------------------------------------------
        // Подлесок: кусты, трава, камни
        // ------------------------------------------------------------------

        private static void BuildUndergrowth(Transform root)
        {
            var undergrowth = new GameObject("Undergrowth").transform;
            undergrowth.SetParent(root, false);

            var bushes = CollectModels(NatureFolder, "Bush_", null);
            var grass = CollectModels(NatureFolder, "Grass_", "Singlesided");
            var rocks = CollectModels(NatureFolder, "Rock_", null);

            // Кусты и трава коллайдеров НЕ получают: сквозь них надо ходить.
            // Иначе поле превращается в лабиринт, а навигация — в кашу.
            Scatter(bushes, undergrowth, 70, 6f, ForestOuter - 3f, 0.8f, 1.3f, false);
            Scatter(grass, undergrowth, 260, 4f, ForestInner + 4f, 0.9f, 1.6f, false);
            Scatter(rocks, undergrowth, 40, 8f, ForestOuter - 4f, 0.7f, 1.4f, false);
        }

        private static void Scatter(List<string> pool, Transform parent, int count,
                                    float minRadius, float maxRadius,
                                    float minScale, float maxScale, bool solid)
        {
            if (pool.Count == 0) return;

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(minRadius, maxRadius);

                var pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                var go = Place(pool[Random.Range(0, pool.Count)], parent, pos,
                               Random.Range(0f, 360f), Random.Range(minScale, maxScale));

                if (solid) AddSolidCollider(go);
            }
        }

        // ------------------------------------------------------------------
        // Помощники
        // ------------------------------------------------------------------

        private static List<string> CollectModels(string folder, string prefix, string exclude)
        {
            var result = new List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);

                if (!name.StartsWith(prefix)) continue;
                if (exclude != null && name.Contains(exclude)) continue;

                result.Add(path);
            }

            return result;
        }

        private static GameObject Place(string path, Transform parent, Vector3 position,
                                        float rotY, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                Debug.LogWarning("[IsoRPG] Не найдена модель " + path);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            go.transform.localScale = Vector3.one * scale;

            // Всё окружение статично: Unity объединит его при сборке и не
            // будет пересчитывать освещение каждый кадр.
            //
            // Флага навигации здесь больше нет: в новых версиях Unity
            // источники для навигационной сетки выбираются самой поверхностью
            // при выпечке, а не пометкой на объекте. Наша поверхность собирает
            // по видимым мешам, поэтому окружение попадёт в неё в любом случае.
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic |
                                                       StaticEditorFlags.OccluderStatic |
                                                       StaticEditorFlags.OccludeeStatic);

            return go;
        }

        /// <summary>Коллайдер по форме меша — для стен и колонн.</summary>
        private static void AddSolidCollider(GameObject go)
        {
            if (go == null) return;

            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                if (filter.GetComponent<Collider>() != null) continue;

                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
        }

        /// <summary>
        /// Узкая коробка по стволу — для деревьев.
        ///
        /// Коллайдер по кроне перекрыл бы проход на всю её ширину: игрок
        /// упирался бы в воздух под ветками, а лучник считал бы листву
        /// непрозрачной для стрел.
        /// </summary>
        private static void AddTrunkCollider(GameObject go)
        {
            if (go == null) return;

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);
            box.size = new Vector3(0.55f, bounds.size.y, 0.55f);
        }

        private static float MeasureWidth(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return 0f;

            var renderers = prefab.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0f;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            return bounds.size.x;
        }
    }
}
