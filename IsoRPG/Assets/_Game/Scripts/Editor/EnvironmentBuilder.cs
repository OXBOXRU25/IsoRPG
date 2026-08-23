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

        /// <summary>
        /// Двор вокруг руин: сюда лес не заходит. Без расчищенной полосы
        /// деревья лезут прямо в стены, и постройка теряется в кустах.
        /// </summary>
        private const float ClearingRadius = 26f;

        private const float ForestInner = 27f;
        private const float ForestOuter = 38f;

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

            float cell = MeasureWidth(DungeonFolder + "/wall.fbx");
            if (cell < 0.5f) cell = 4f;

            var map = RuinsLayout.Map;
            int rows = map.Length;
            int cols = 0;
            foreach (var line in map) cols = Mathf.Max(cols, line.Length);

            // Центрируем план вокруг начала координат: игрок появляется там,
            // и руины должны быть вокруг него, а не сбоку.
            float offsetX = -(cols - 1) * cell * 0.5f;
            float offsetZ = (rows - 1) * cell * 0.5f;

            for (int z = 0; z < rows; z++)
            {
                string line = map[z];

                for (int x = 0; x < line.Length; x++)
                {
                    char c = line[x];
                    if (c == ' ') continue;

                    var at = new Vector3(offsetX + x * cell, 0f, offsetZ - z * cell);

                    // Пол под всем, включая стены: обрыв кладки в пустоту
                    // читается как недоделка, а не как разрушение.
                    if (RuinsLayout.HasFloor(c))
                        Place(DungeonFolder + "/" + RuinsLayout.FloorFor(c) + ".fbx",
                              ruins, at, Random.Range(0, 4) * 90f, 1f);

                    if (RuinsLayout.IsWallChar(c))
                    {
                        PlaceWall(ruins, map, x, z, at, cell);
                        continue;
                    }

                    string prop = RuinsLayout.PropFor(c);
                    if (prop == null) continue;

                    var placed = Place(DungeonFolder + "/" + prop + ".fbx", ruins, at,
                                       c == 'o' ? 0f : PropAngle(map, x, z), 1f);

                    // Колонны и штабеля перекрывают выстрел, бочка по пояс —
                    // нет: иначе лучник замолкал бы за каждым ящиком.
                    if (RuinsLayout.IsSolidProp(c)) AddSolidCollider(placed);
                }
            }

            Decorate(ruins, map, cell, offsetX, offsetZ);
        }

        /// <summary>
        /// Расставляет мелочь: свечи вдоль стен и хлам на полу.
        ///
        /// Не рисуется в карте намеренно. Свеча у стены — это не решение
        /// планировщика, а правило: «вдоль стен, через одну, изнутри». Такие
        /// вещи должны появляться сами, иначе карта превращается в мозаику из
        /// сотни символов и перестаёт читаться — а читаемость и была смыслом
        /// карты.
        ///
        /// Мелочь важнее, чем кажется: пустой каменный пол выглядит
        /// недоделанным при любой планировке, а десяток свечей и пара бутылок
        /// превращают помещение в место, где кто-то был.
        /// </summary>
        private static void Decorate(Transform parent, string[] map, float cell,
                                     float offsetX, float offsetZ)
        {
            var candles = new[] { "candle_lit", "candle_thin_lit", "candle_triple", "candle_melted" };
            var litter = new[] { "bottle_A_green", "bottle_B_brown", "bottle_C_green",
                                 "coin_stack_small", "box_small", "rubble_half" };

            for (int z = 0; z < map.Length; z++)
            {
                string line = map[z];

                for (int x = 0; x < line.Length; x++)
                {
                    // Ставим только на пустой каменный пол: под мебелью и на
                    // земле мелочь смотрится мусором, а не деталью.
                    if (line[x] != '.') continue;

                    var at = new Vector3(offsetX + x * cell, 0f, offsetZ - z * cell);

                    float wallAngle;
                    if (NextToWall(map, x, z, out wallAngle))
                    {
                        // Вдоль стены — свечи. Через одну: сплошной ряд
                        // читается как иллюминация, а не как заброшенное место.
                        if ((x + z) % 2 == 0 && Random.value < 0.65f)
                        {
                            Vector3 shift = Quaternion.Euler(0f, wallAngle, 0f) * Vector3.forward;

                            var candle = Place(
                                DungeonFolder + "/" + candles[Random.Range(0, candles.Length)] + ".fbx",
                                parent, at - shift * cell * 0.34f, Random.Range(0, 4) * 90f, 1f);

                            // Светит не каждая: свет — самое дорогое, что
                            // есть в кадре, и три десятка источников ради
                            // свечек не окупаются. Хватает трети, остальные
                            // читаются отражённым светом соседей.
                            // Радиус больше, яркость меньше: свеча должна
                            // подсвечивать угол, а не выжигать круг на полу.
                            if (Random.value < 0.35f) AddFireLight(candle, 4.5f, 0.5f, 0.55f);
                        }

                        continue;
                    }

                    // В глубине комнаты — редкий хлам.
                    if (Random.value < 0.07f)
                    {
                        Vector2 jitter = Random.insideUnitCircle * cell * 0.28f;

                        Place(DungeonFolder + "/" + litter[Random.Range(0, litter.Length)] + ".fbx",
                              parent, at + new Vector3(jitter.x, 0f, jitter.y),
                              Random.Range(0f, 360f), 1f);
                    }
                }
            }
        }

        /// <summary>
        /// Стоит ли клетка у стены и куда эта стена смотрит.
        /// </summary>
        private static bool NextToWall(string[] map, int x, int z, out float angle)
        {
            angle = 0f;

            if (IsWallAt(map, x, z - 1)) { angle = 0f; return true; }
            if (IsWallAt(map, x, z + 1)) { angle = 180f; return true; }
            if (IsWallAt(map, x - 1, z)) { angle = 270f; return true; }
            if (IsWallAt(map, x + 1, z)) { angle = 90f; return true; }

            return false;
        }

        /// <summary>
        /// Ставит секцию стены, подбирая вид по соседям.
        ///
        /// Угол, обычная секция или трещина — это не украшение: одинаковые
        /// куски по всему периметру читаются как обои, а торчащие торцами
        /// углы выдают сборку вслепую.
        /// </summary>
        private static void PlaceWall(Transform parent, string[] map, int x, int z,
                                      Vector3 at, float cell)
        {
            char c = map[z][x];
            string model = RuinsLayout.WallFor(c);
            float angle = WallAngle(map, x, z);

            bool corner = IsCorner(map, x, z);

            if (corner && c == '#')
            {
                model = RuinsLayout.CornerModel;
                angle = CornerAngle(map, x, z);
            }
            else if (c == '#')
            {
                // Проёмы и окна не ломаем: через них ходят и смотрят.
                if (RuinsLayout.ShouldBreak(x, z)) model = RuinsLayout.BrokenModel;
                else if (RuinsLayout.ShouldCrack(x, z)) model = RuinsLayout.CrackedModel;
            }

            var go = Place(DungeonFolder + "/" + model + ".fbx", parent, at, angle, 1f);
            AddSolidCollider(go);

            if (!RuinsLayout.HasTorch(c)) return;

            // Факел вешаем на стену, а не ставим на пол: напольный посреди
            // зала читается как столб непонятного назначения.
            var torch = Place(DungeonFolder + "/" + RuinsLayout.TorchModel + ".fbx", parent,
                              at + Vector3.up * cell * 0.42f, angle, 1f);

            AddFireLight(torch, 8f, 2.1f, 0.25f);
        }

        /// <summary>
        /// Стена смотрит внутрь помещения. Считается по соседям: где открытая
        /// клетка — туда и лицо.
        /// </summary>
        private static float WallAngle(string[] map, int x, int z)
        {
            if (IsOpenAt(map, x, z + 1)) return 0f;
            if (IsOpenAt(map, x, z - 1)) return 180f;
            if (IsOpenAt(map, x + 1, z)) return 90f;
            if (IsOpenAt(map, x - 1, z)) return 270f;

            return 0f;
        }

        private static bool IsCorner(string[] map, int x, int z)
        {
            bool up = IsWallAt(map, x, z - 1);
            bool down = IsWallAt(map, x, z + 1);
            bool left = IsWallAt(map, x - 1, z);
            bool right = IsWallAt(map, x + 1, z);

            // Угол — это когда стена продолжается по двум перпендикулярным
            // направлениям, а по остальным обрывается.
            return (up || down) && (left || right);
        }

        private static float CornerAngle(string[] map, int x, int z)
        {
            bool up = IsWallAt(map, x, z - 1);
            bool left = IsWallAt(map, x - 1, z);
            bool right = IsWallAt(map, x + 1, z);

            if (up && right) return 0f;
            if (up && left) return 270f;
            if (left) return 180f;

            return 90f;
        }

        private static float PropAngle(string[] map, int x, int z)
        {
            if (IsWallAt(map, x, z - 1)) return 0f;
            if (IsWallAt(map, x, z + 1)) return 180f;
            if (IsWallAt(map, x - 1, z)) return 90f;
            if (IsWallAt(map, x + 1, z)) return 270f;

            return Random.Range(0, 4) * 90f;
        }

        private static bool IsOpenAt(string[] map, int x, int z)
        {
            if (z < 0 || z >= map.Length) return false;
            if (x < 0 || x >= map[z].Length) return false;

            return RuinsLayout.IsOpen(map[z][x]);
        }

        private static bool IsWallAt(string[] map, int x, int z)
        {
            if (z < 0 || z >= map.Length) return false;
            if (x < 0 || x >= map[z].Length) return false;

            return RuinsLayout.IsWallChar(map[z][x]);
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

            // Лес растёт рощами, а не ровным слоем. Равномерный разброс —
            // самое узнаваемое «сделано генератором»: в природе деревья
            // цепляются друг за друга, оставляя прогалины между группами.
            // Прогалины важнее самих деревьев: по ним ходят и через них видно.
            for (int grove = 0; grove < 26; grove++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(ForestInner, ForestOuter);

                var center = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                // Ближе к руинам рощи мельче и суше — переход, а не стена леса.
                float toEdge = Mathf.InverseLerp(ForestInner, ForestOuter, radius);
                int count = Mathf.RoundToInt(Mathf.Lerp(4f, 11f, toEdge));
                float spread = Mathf.Lerp(3.5f, 6f, toEdge);
                float bareChance = Mathf.Lerp(0.75f, 0.1f, toEdge);

                for (int i = 0; i < count; i++)
                {
                    Vector2 flat = Random.insideUnitCircle * spread;
                    var pos = center + new Vector3(flat.x, 0f, flat.y);

                    if (pos.magnitude < ClearingRadius) continue;

                    var pool = (bare.Count > 0 && Random.value < bareChance) ? bare : living;

                    var go = Place(pool[Random.Range(0, pool.Count)], forest, pos,
                                   Random.Range(0f, 360f), Random.Range(0.85f, 1.3f));

                    AddTrunkCollider(go);
                }
            }

            // Одиночные сухие деревья во дворе: они разбивают пустоту и дают
            // лучнику что обходить, но не превращают площадку в чащу.
            var solitary = bare.Count > 0 ? bare : living;

            for (int i = 0; i < 9; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(ClearingRadius * 0.72f, ClearingRadius - 1f);

                var pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                var go = Place(solitary[Random.Range(0, solitary.Count)], forest, pos,
                               Random.Range(0f, 360f), Random.Range(0.8f, 1.05f));

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
            // Кусты и камни только снаружи двора: внутри руин им не место,
            // там пол. Трава заходит на границу — обжитый край выглядит
            // естественнее ровно обрезанного.
            Scatter(bushes, undergrowth, 90, ClearingRadius - 2f, ForestOuter - 2f, 0.8f, 1.3f, false);
            Scatter(grass, undergrowth, 220, ClearingRadius - 6f, ForestOuter - 4f, 0.9f, 1.6f, false);
            Scatter(rocks, undergrowth, 45, ClearingRadius - 3f, ForestOuter - 3f, 0.7f, 1.4f, false);
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

        /// <summary>
        /// Живой огонь: точечный источник с лёгким дрожанием.
        ///
        /// Ровно горящий факел выглядит лампочкой. Дрожание делает мизерная
        /// анимация яркости — эффект стоит копейки, а замечается сразу,
        /// потому что глаз ищет движение.
        /// </summary>
        private static void AddFireLight(GameObject go, float range, float intensity, float height)
        {
            if (go == null) return;

            var holder = new GameObject("Fire");
            holder.transform.SetParent(go.transform, false);

            // Огонь горит на верхушке, а не у основания. Источник у самого
            // пола даёт кляксу прямо под предметом: свет упирается в пол
            // раньше, чем успевает разойтись, и вместо свечения получается
            // пятно от фонарика.
            holder.transform.localPosition = Vector3.up * height;

            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.36f);
            light.range = range;
            light.intensity = intensity;

            // Тени от свечей не считаем: их десятки, и каждая обошлась бы
            // дороже всего остального освещения вместе взятого.
            light.shadows = LightShadows.None;

            holder.AddComponent<IsoRPG.Combat.FlickeringLight>();
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
