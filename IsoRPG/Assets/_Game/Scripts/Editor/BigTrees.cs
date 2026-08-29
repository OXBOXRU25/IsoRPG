using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Крупные деревья: великаны Зачарованного леса и рослые луговые.
    ///
    /// Отдельно от посева луга намеренно. Луг — это ковёр, который сеется
    /// плотностью на сотню метров и пересевается целиком при каждой правке
    /// травы. Дерево в восемьдесят метров — не ковёр, а ориентир: их
    /// единицы, каждое видно с любой точки карты, и место каждому выбирается
    /// по правилам, которых у травы нет вовсе.
    ///
    /// Три требования заказчика зашиты в код, а не оставлены на глазок:
    /// дерево не висит в воздухе, не растёт из водоёма и не стоит в чужом
    /// дереве. Каждое проверяется замером после посадки — журнал печатает
    /// числа, а не «готово».
    /// </summary>
    public static class BigTrees
    {
        private const string Holder = "Деревья";
        private const string MeadowHolder = "Луг Synty";

        /// <summary>Поле посева — то же, что у луга: 160 × 160 м.</summary>
        private const float Field = 160f;

        /// <summary>Чистый пятачок вокруг героя.</summary>
        private const float HomeKeep = 14f;

        private sealed class Kind
        {
            public string Biome;
            public string Name;
            public int Count;
            public float MinScale, MaxScale;

            /// <summary>Предельная крутизна места, градусы.</summary>
            public float MaxSteep;

            /// <summary>Доля кроны, которую занимает комель с корнями.</summary>
            public float TrunkShare;

            /// <summary>
            /// Насколько закопать дерево — ДОЛЯ его высоты.
            ///
            /// Числа сняты щупом «tree-norm» с демо-сцен самого набора, где
            /// эти же деревья расставлены автором: берёза утоплена на 1.05 м
            /// при высоте 14.3 (38 экземпляров), луговое на 1.49 при 15.2,
            /// великаны на 2.4–3.1 м. Своя посадка «нижней точкой на грунт»
            /// давала ноль — отсюда и висящие в воздухе корни.
            /// </summary>
            public float SinkShare;

            /// <summary>Держаться не ближе этой доли от суммы крон.</summary>
            public float Spacing;

            /// <summary>Ставить не ближе этого от центра карты, метры.</summary>
            public float MinFromHome;

            /// <summary>
            /// Крона висит ВЫСОКО над остальным лесом.
            ///
            /// У великана она начинается на шестидесяти метрах, и мерить по
            /// ней расстояние до соседей неверно: под таким деревом мелкие
            /// растут как раз и должны, это и есть лес. Первый заход с
            /// меркой по кроне не поставил ни одного великана — свободного
            /// круга радиусом в двадцать четыре метра на поле просто нет.
            /// Для таких меряем по стволу, а крона пусть накрывает.
            /// </summary>
            public bool Overhang;
        }

        /// <summary>
        /// Что сажаем и в какой дозе.
        ///
        /// Великанов по одному на вид, и это не осторожность, а счёт: крона
        /// 59 м при поле посева 160 м — треть поля одним деревом. Двух таких
        /// рядом мир не вмещает, они встанут стеной.
        /// </summary>
        private static readonly Kind[] Table =
        {
            new Kind
            {
                Biome = "PNB_Enchanted_Forest", Name = "SM_Env_Tree_Giant_01",
                Count = 1, MinScale = 0.72f, MaxScale = 0.82f,
                MaxSteep = 13f, TrunkShare = 0.30f, SinkShare = 0.035f,
                Spacing = 1.10f, MinFromHome = 38f, Overhang = true,
            },
            new Kind
            {
                Biome = "PNB_Enchanted_Forest", Name = "SM_Env_Tree_Giant_02",
                Count = 1, MinScale = 0.72f, MaxScale = 0.82f,
                MaxSteep = 13f, TrunkShare = 0.30f, SinkShare = 0.039f,
                Spacing = 1.10f, MinFromHome = 38f, Overhang = true,
            },
            new Kind
            {
                Biome = "PNB_Enchanted_Forest", Name = "SM_Env_Tree_Large_01",
                Count = 3, MinScale = 0.85f, MaxScale = 1.10f,
                MaxSteep = 14f, TrunkShare = 0.16f, SinkShare = 0.055f,
                Spacing = 0.80f, MinFromHome = 26f,
            },
            new Kind
            {
                Biome = "PNB_Enchanted_Forest", Name = "SM_Env_Tree_Large_02",
                Count = 3, MinScale = 0.85f, MaxScale = 1.10f,
                MaxSteep = 14f, TrunkShare = 0.16f, SinkShare = 0.055f,
                Spacing = 0.80f, MinFromHome = 26f,
            },
            new Kind
            {
                Biome = "PNB_Meadow_Forest", Name = "SM_Env_Tree_Meadow_01",
                Count = 16, MinScale = 0.80f, MaxScale = 1.20f,
                MaxSteep = 20f, TrunkShare = 0.14f, SinkShare = 0.102f,
                Spacing = 0.62f, MinFromHome = 16f,
            },
            new Kind
            {
                Biome = "PNB_Meadow_Forest", Name = "SM_Env_Tree_Birch_01",
                Count = 24, MinScale = 0.85f, MaxScale = 1.25f,
                MaxSteep = 24f, TrunkShare = 0.14f, SinkShare = 0.099f,
                Spacing = 0.55f, MinFromHome = 15f,
            },
        };

        /// <summary>Занятое место: середина, поперечник кроны, радиус комля.</summary>
        private readonly struct Spot
        {
            public readonly Vector2 P;
            public readonly float Width;
            public readonly float Trunk;

            public Spot(Vector2 p, float width, float trunk)
            {
                P = p; Width = width; Trunk = trunk;
            }
        }

        [MenuItem("Tools/IsoRPG/Мир: крупные деревья", priority = 30)]
        public static void Plant()
        {
            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — сажать не на чем.");
                return;
            }

            Clear();

            var holder = new GameObject(Holder);
            var placed = new List<Spot>();
            var giants = new List<Vector2>();

            // Чужие деревья луга — тоже занятое место.
            //
            // Заказчик просил, чтобы новое дерево не встало в старое. Трава и
            // цветы при этом помехой не считаются: под кроной им и место.
            var meadowTrees = MeadowTrees();

            Random.InitState(20260830);

            int total = 0;
            float worstGap = 0f, nearestPond = float.MaxValue;

            foreach (var k in Table)
            {
                string path = "Assets/PolygonNatureBiomes/" + k.Biome +
                              "/Prefabs/" + k.Name + ".prefab";

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет префаба " + path);
                    continue;
                }

                int done = 0;

                // Попыток с запасом: у великана условий больше всего, и на
                // ровном месте вдали от воды и троп он находится не с первого
                // броска. Ограничение нужно, чтобы задание не зациклилось,
                // если места не осталось вовсе.
                for (int attempt = 0; attempt < 4000 && done < k.Count; attempt++)
                {
                    float x = Random.Range(-Field * 0.5f, Field * 0.5f);
                    float z = Random.Range(-Field * 0.5f, Field * 0.5f);
                    var at = new Vector2(x, z);

                    if (at.magnitude < Mathf.Max(HomeKeep, k.MinFromHome)) continue;

                    float u = Mathf.Clamp01((x - terrain.transform.position.x) /
                                            terrain.terrainData.size.x);
                    float v = Mathf.Clamp01((z - terrain.transform.position.z) /
                                            terrain.terrainData.size.z);

                    // Крутизна: дерево растёт вверх, и на косогоре комель с
                    // одной стороны неизбежно повисает. Проще не ставить.
                    if (terrain.terrainData.GetSteepness(u, v) > k.MaxSteep) continue;

                    // Тропа: ствол посреди дороги — то же, что куст посреди
                    // дороги, только заметнее.
                    if (SyntyMeadow.OnPath(terrain, at, 4f)) continue;

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);

                    float ground = terrain.SampleHeight(new Vector3(x, 0f, z)) +
                                   terrain.transform.position.y;

                    go.transform.position = new Vector3(x, ground, z);
                    go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    go.transform.localScale = Vector3.one * Random.Range(k.MinScale, k.MaxScale);

                    var box = Box(go);

                    if (box.size == Vector3.zero)
                    {
                        Object.DestroyImmediate(go);
                        continue;
                    }

                    float width = Mathf.Max(box.size.x, box.size.z);
                    float trunk = Mathf.Max(width * k.TrunkShare * 0.5f, 0.4f);

                    // Водоём обходим ПО КРОНЕ, а не по стволу.
                    //
                    // Ствол в трёх метрах от воды выглядит нормально, а крона
                    // в двадцать метров при этом висит над всем прудом и
                    // накрывает его тенью. Считаем от края глади до края
                    // кроны, с запасом в три метра.
                    bool atWater = false;

                    // Великану меряем берег по стволу: крона на шестидесяти
                    // метрах над водой не мешает никому, а вот комель в пруду
                    // — ровно то, чего просили не делать.
                    float shore = k.Overhang ? trunk + 10f : width * 0.5f + 3f;

                    foreach (var pond in SyntyWater.Ponds)
                    {
                        float gap = Vector2.Distance(at, pond.Centre) - pond.Radius;

                        if (gap < shore) { atWater = true; break; }
                    }

                    if (atWater)
                    {
                        Object.DestroyImmediate(go);
                        continue;
                    }

                    // Своих и чужих деревьев касаемся кронами, но не входим
                    // друг в друга: доля Spacing и есть допустимое касание.
                    bool crowded = false;

                    // Чем меряем себя: обычное дерево — кроной, великан —
                    // стволом. Под великаном лес растёт, в обычное дерево
                    // упирается крона.
                    float mine = k.Overhang ? trunk * 2f : width;

                    foreach (var s in placed.Concat(meadowTrees))
                    {
                        float need = (s.Width + mine) * 0.5f * k.Spacing;

                        if (Vector2.Distance(s.P, at) < need) { crowded = true; break; }
                    }

                    // Великаны — ориентиры, и стоять они должны в разных
                    // концах: два рядом читаются как один куст на горизонте.
                    if (!crowded && k.Overhang)
                        foreach (var g in giants)
                            if (Vector2.Distance(g, at) < 70f) { crowded = true; break; }

                    if (crowded)
                    {
                        Object.DestroyImmediate(go);
                        continue;
                    }

                    // ПОСАДКА ПО НИЖНЕЙ ТОЧКЕ МОДЕЛИ, а не по опорной точке
                    // префаба. У этих деревьев опорная точка стоит выше
                    // комля — поставишь её на грунт, и дерево повиснет в
                    // воздухе на высоту корней. Меряем габариты уже
                    // повёрнутого и масштабированного объекта и опускаем на
                    // разницу, потом топим комель.
                    float steep = terrain.terrainData.GetSteepness(u, v);
                    float slopeSink = trunk * Mathf.Tan(steep * Mathf.Deg2Rad) * 0.6f;

                    // Глубина посадки: авторская доля высоты, но НЕ МЕНЬШЕ
                    // высоты корневого раструба. У великана корни расходятся
                    // вширь на несколько метров вверх от кончиков, и
                    // авторские три метра их не закрывают — а именно они и
                    // висели в воздухе на кадре у заказчика.
                    float flare = FlareTop(go, box);

                    float sink = Mathf.Max(box.size.y * k.SinkShare, flare) +
                                 Mathf.Min(slopeSink, trunk * 0.5f);

                    // Потолок: дерево должно стоять в земле, а не тонуть.
                    sink = Mathf.Min(sink, box.size.y * 0.22f);

                    go.transform.position += new Vector3(0f, ground - box.min.y - sink, 0f);

                    if (k.Overhang)
                        Debug.Log("[IsoRPG]   " + k.Name + ": высота " +
                                  box.size.y.ToString("0.0") + " м, раструб корней " +
                                  flare.ToString("0.0") + " м, закопано на " +
                                  sink.ToString("0.0") + " м.");

                    placed.Add(new Spot(at, width, trunk));
                    if (k.Overhang) giants.Add(at);
                    done++;
                    total++;

                    foreach (var pond in SyntyWater.Ponds)
                    {
                        float edge = Vector2.Distance(at, pond.Centre) - pond.Radius -
                                     width * 0.5f;

                        if (edge < nearestPond) nearestPond = edge;
                    }
                }

                Debug.Log("[IsoRPG] Деревья: " + k.Name + " — " + done + " из " + k.Count);
            }

            int swallowed = ClearUnderTrunks(placed);

            // ЩУП: перечитываем то, что получилось, вместо доверия журналу.
            //
            // Журнал печатает тот же код, который делал работу, и подтверждает
            // лишь что он дошёл до строки. Висящее дерево так не поймать.
            // Меряем ГЛУБИНУ ПОСАДКИ, а не зазор под нижней точкой.
            //
            // Прежний щуп считал зазор до низа модели и уверенно печатал
            // «0.00» — низ и правда лежал на грунте. Только низ у этих
            // деревьев — кончики корней, и весь раструб при этом висел в
            // воздухе. Проверка была исправна, а мерила не то.
            float shallowest = float.MaxValue;

            foreach (Transform t in holder.transform)
            {
                var box = Box(t.gameObject);
                float ground = terrain.SampleHeight(t.position) + terrain.transform.position.y;
                float deep = ground - box.min.y;

                if (deep < shallowest) shallowest = deep;
                if (-deep > worstGap) worstGap = -deep;
            }

            Debug.Log("[IsoRPG] Самое мелко сидящее дерево закопано на " +
                      (shallowest == float.MaxValue ? 0f : shallowest).ToString("0.00") + " м.");

            Debug.Log("[IsoRPG] Крупных деревьев посажено " + total +
                      ", из-под стволов убрано растений " + swallowed +
                      ". Наибольший зазор под комлем " + worstGap.ToString("0.00") +
                      " м, ближайшая крона к воде " +
                      (nearestPond == float.MaxValue ? 0f : nearestPond).ToString("0.0") + " м.");

            if (worstGap > 0.05f)
                Debug.LogWarning("[IsoRPG] ДЕРЕВО ВИСИТ: зазор " + worstGap.ToString("0.00") +
                                 " м — посадка по нижней точке не сработала.");

            EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>
        /// Снять посаженное: два великана вблизи и мир общим планом.
        ///
        /// Отдельным заданием, потому что снимать надо ПОСЛЕ посадки и по её
        /// результату: где встали великаны, заранее не известно никому.
        /// </summary>
        public static void Shots()
        {
            var holder = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                               .FirstOrDefault(g => g.name == Holder);

            if (holder == null)
            {
                Debug.LogError("[IsoRPG] Деревьев в сцене нет — снимать нечего.");
                return;
            }

            var tall = holder.transform.Cast<Transform>()
                             .Select(t => (t, h: Box(t.gameObject).size.y))
                             .OrderByDescending(p => p.h)
                             .Take(2)
                             .ToArray();

            int n = 0;

            foreach (var (t, h) in tall)
            {
                n++;

                // Смотрим снизу вверх с полутора высот: сверху восемьдесят
                // метров читаются как куст, ради роста всё и затевалось.
                SceneEye.Shot("tree-giant-" + n,
                              t.position + new Vector3(0f, h * 0.42f, 0f),
                              h * 1.75f, 10f, 35f);
            }

            SceneEye.Shot("trees-wide", new Vector3(0f, 8f, 0f), 150f, 16f, 35f);

            Debug.Log("[IsoRPG] Кадры деревьев сняты: tree-giant-1, tree-giant-2, trees-wide.");
        }

        /// <summary>Снести прежнюю посадку: задание должно быть повторяемым.</summary>
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .Where(g => g.name == Holder).ToArray();

            foreach (var o in old) Object.DestroyImmediate(o);
        }

        /// <summary>Деревья, уже стоящие на лугу, с их поперечником.</summary>
        private static List<Spot> MeadowTrees()
        {
            var list = new List<Spot>();

            var meadow = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                               .FirstOrDefault(g => g.name == MeadowHolder);

            if (meadow == null) return list;

            foreach (Transform t in meadow.transform)
            {
                if (t.name.IndexOf("Tree", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var box = Box(t.gameObject);
                float w = Mathf.Max(box.size.x, box.size.z);

                list.Add(new Spot(new Vector2(t.position.x, t.position.z), w, w * 0.1f));
            }

            return list;
        }

        /// <summary>
        /// Убрать растения, оказавшиеся внутри ствола.
        ///
        /// Трава под кроной — это лес. Трава, растущая СКВОЗЬ ствол, — это
        /// ошибка, которую видно с любого ракурса.
        /// </summary>
        private static int ClearUnderTrunks(List<Spot> trees)
        {
            var meadow = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                               .FirstOrDefault(g => g.name == MeadowHolder);

            if (meadow == null) return 0;

            var doomed = new List<GameObject>();

            foreach (Transform t in meadow.transform)
            {
                var at = new Vector2(t.position.x, t.position.z);

                foreach (var tree in trees)
                {
                    if (Vector2.Distance(at, tree.P) < tree.Trunk * 1.15f)
                    {
                        doomed.Add(t.gameObject);
                        break;
                    }
                }
            }

            foreach (var d in doomed) Object.DestroyImmediate(d);

            return doomed.Count;
        }

        /// <summary>
        /// Высота корневого раструба над самой нижней точкой модели.
        ///
        /// У этих деревьев корни расходятся от ствола вширь и вниз. Их
        /// кончики и есть нижняя точка модели, поэтому посадка «нижней
        /// точкой на грунт» оставляет ВЕСЬ раструб на поверхности — дерево
        /// стоит на цыпочках, как на паучьих лапах. Закапывать надо по то
        /// место, где корни сходятся в ствол.
        ///
        /// Ищем его по геометрии: режем нижнюю половину дерева на слои и
        /// смотрим, на какой высоте поперечник перестаёт превышать ствол.
        /// Число выводится из самой модели, поэтому работает и для тех
        /// видов, которых в демо-сценах автора нет вовсе.
        /// </summary>
        private static float FlareTop(GameObject go, Bounds box)
        {
            const int Slices = 40;

            float height = box.size.y;
            if (height < 0.5f) return 0f;

            // Смотрим только нижнюю половину: выше начинается крона, и её
            // ширина к корням отношения не имеет.
            float span = height * 0.5f;
            var widest = new float[Slices];

            var axis = new Vector2(box.center.x, box.center.z);
            int seen = 0;

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                Vector3[] verts;

                // Меш без разрешения на чтение отдаёт пустой список — не
                // ошибка, просто этим путём его не измерить.
                try { verts = mesh.vertices; }
                catch { continue; }

                var toWorld = mf.transform.localToWorldMatrix;

                foreach (var v in verts)
                {
                    var w = toWorld.MultiplyPoint3x4(v);
                    float up = w.y - box.min.y;

                    if (up < 0f || up > span) continue;

                    int slice = Mathf.Clamp((int)(up / span * Slices), 0, Slices - 1);
                    float r = Vector2.Distance(new Vector2(w.x, w.z), axis);

                    if (r > widest[slice]) widest[slice] = r;
                    seen++;
                }
            }

            if (seen < 50) return 0f;

            // Ствол: самый узкий слой в верхней трети рассмотренного куска —
            // там раструб уже кончился, а крона ещё не началась.
            float trunkR = float.MaxValue;

            for (int i = Slices * 2 / 3; i < Slices; i++)
                if (widest[i] > 0.01f && widest[i] < trunkR) trunkR = widest[i];

            if (trunkR == float.MaxValue || trunkR < 0.01f) return 0f;

            // Верх раструба: самый высокий слой, который всё ещё заметно
            // шире ствола.
            for (int i = Slices - 1; i >= 0; i--)
            {
                if (widest[i] > trunkR * 1.8f)
                    return (i + 1f) / Slices * span;
            }

            return 0f;
        }

        /// <summary>Габариты по всем видимым частям объекта.</summary>
        private static Bounds Box(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);

            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

            var box = rs[0].bounds;
            foreach (var r in rs) box.Encapsulate(r.bounds);

            return box;
        }
    }
}
