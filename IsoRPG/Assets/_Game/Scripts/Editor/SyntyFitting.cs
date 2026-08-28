using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Примерочная: две одинаковые комнаты бок о бок — наша из KayKit и их
    /// из POLYGON Dungeons, — чтобы решить глазами, стоит ли переезжать.
    ///
    /// Именно ДВЕ. Первая версия ставила только комнату Synty, и польза от
    /// неё оказалась нулевой: сравнивать было не с чем, а вдобавок она
    /// встала посреди наших же руин, и наборы перемешались так, что стык
    /// стало не найти. Один набор в кадре отвечает на вопрос «красиво ли»,
    /// а нужен ответ на «лучше ли нашего» — а на него отвечает только пара.
    ///
    /// Всё складывается в один объект "SyntyFitting": убирается соседним
    /// пунктом меню, следов в сцене не остаётся.
    /// </summary>
    public static class SyntyFitting
    {
        private const string Synty = "Assets/PolygonDungeon/Prefabs";
        private const string KayKit = "Assets/_Game/Art/KayKit/Dungeon";
        private const string OurPrefabs = "Assets/_Game/Prefabs";
        private const string HolderName = "SyntyFitting";

        /// <summary>Сколько клеток в стороне комнаты.</summary>
        private const int Side = 3;

        /// <summary>
        /// Где ставим — к югу от руин, снаружи них.
        ///
        /// Руины занимают X от -62 до 62 и Z от -32 до 32; предыдущая версия
        /// стояла в точке (-16, 0, -8), то есть в самой их середине, и
        /// примерочная перемешалась с настоящим подземельем. Считаем от
        /// южного края карты, а не от записанного числа: изменится карта —
        /// примерочная уедет вместе с ней и снова окажется снаружи.
        /// </summary>
        private static Vector3 Origin =>
            new Vector3(RuinsLayout.HallCentre.x,
                        0f,
                        RuinsLayout.CellToWorld(0, RuinsLayout.Map.Length - 1).z - 16f);

        [MenuItem("Tools/IsoRPG/Примерочная Synty", priority = 40)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            Clear();

            var holder = new GameObject(HolderName);
            holder.transform.position = Origin;

            // Размер клетки меряем по стене каждого набора, а не задаём.
            //
            // Наборы живут в разных единицах: Synty импортируется с
            // globalScale 100, KayKit — с 0.771, потому что его ужимали под
            // рост героя. Записанное сюда число разошлось бы с правдой при
            // первой же перенастройке импорта.
            float syntyCell = MeasureWidth(Synty + "/Environments/Walls/SM_Env_Wall_01.prefab");
            float kayCell = MeasureWidth(KayKit + "/wall.fbx");

            if (syntyCell <= 0f)
            {
                Debug.LogError("[IsoRPG] Не нашёл SM_Env_Wall_01 — набор Synty не импортирован.");
                Object.DestroyImmediate(holder);
                return;
            }

            Debug.Log("[IsoRPG] Стена KayKit " + kayCell.ToString("0.00") +
                      ", стена Synty " + syntyCell.ToString("0.00") +
                      ". Их деталь " + (kayCell > 0f ? (syntyCell / kayCell).ToString("0.00") : "?") +
                      " от нашей. Единица означала бы, что раскладку руин можно " +
                      "перенести один в один; иначе шаг придётся пересчитывать.");

            // Наша слева, их справа. Промежуток в полторы клетки: комнаты
            // должны читаться как две, но помещаться в один кадр — иначе
            // сравнивать снова придётся по памяти, а это мы уже проходили.
            float gap = Mathf.Max(kayCell, syntyCell) * 1.5f;
            float ourWidth = Side * kayCell;

            BuildRoom(holder.transform, Vector3.zero, kayCell, false, "НАШ  KayKit");

            BuildRoom(holder.transform, new Vector3(ourWidth + gap, 0f, 0f),
                      syntyCell, true, "ИХ  Synty");

            Selection.activeGameObject = holder;

            Debug.Log("[IsoRPG] Примерочная собрана к югу от руин, в " + Origin +
                      ". Слева наша комната, справа Synty.");
        }

        [MenuItem("Tools/IsoRPG/Убрать примерочную Synty", priority = 41)]
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == HolderName);

            if (old != null) Object.DestroyImmediate(old);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Одна комната: пол, две стены углом, колонна, бочка и наш скелет
        /// внутри.
        ///
        /// Скелет — обязательная часть, а не украшение. Вопрос ведь не в том,
        /// красив ли набор сам по себе, а в том, не выглядят ли НАШИ
        /// персонажи в нём чужими и не мелкие ли они в их коридорах.
        /// Одинаковый в обеих комнатах, чтобы сравнение было честным.
        /// </summary>
        private static void BuildRoom(Transform parent, Vector3 at, float cell,
                                      bool synty, string label)
        {
            var room = new GameObject(label);
            room.transform.SetParent(parent, false);
            room.transform.localPosition = at;

            for (int x = 0; x < Side; x++)
                for (int z = 0; z < Side; z++)
                    Place(synty
                            ? Synty + "/Environments/Floors/SM_Env_Tiles_0" + (1 + (x + z) % 6) + ".prefab"
                            : KayKit + "/floor_tile_large.fbx",
                          room.transform, new Vector3(x * cell, 0f, z * cell), 0f);

            // Стены двумя сторонами, дальней и левой: при изометрии ближние
            // закрыли бы саму комнату, и смотреть было бы не на что.
            for (int i = 0; i < Side; i++)
            {
                Place(synty
                        ? Synty + "/Environments/Walls/SM_Env_Wall_0" + (1 + i % 7) + ".prefab"
                        : KayKit + "/wall.fbx",
                      room.transform, new Vector3(i * cell, 0f, Side * cell), 180f);

                Place(synty
                        ? Synty + "/Environments/Walls/SM_Env_Wall_0" + (1 + (i + 2) % 7) + ".prefab"
                        : KayKit + "/wall.fbx",
                      room.transform, new Vector3(-cell * 0.5f, 0f, i * cell), 90f);
            }

            Place(synty
                    ? Synty + "/Environments/Pillars/SM_Env_Pillar_Broken_01.prefab"
                    : KayKit + "/column.fbx",
                  room.transform, new Vector3(0f, 0f, 0f), 0f);

            Place(synty
                    ? Synty + "/Props/SM_Prop_Barrel_01.prefab"
                    : KayKit + "/barrel_large.fbx",
                  room.transform, new Vector3(cell * 2.2f, 0f, cell * 0.4f), 20f);

            Place(OurPrefabs + "/Skeleton_Warrior.prefab", room.transform,
                  new Vector3(cell * 1.2f, 0f, cell * 1.2f), 200f);

            AddLabel(room.transform, label, new Vector3(cell * 1.2f, 4f, cell * 1.2f));
        }

        /// <summary>
        /// Подпись над комнатой.
        ///
        /// Без неё через минуту не вспомнить, какая из двух чья, — и вся
        /// затея снова превращается в сравнение по памяти.
        /// </summary>
        private static void AddLabel(Transform parent, string text, Vector3 local)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;

            // Разворачиваем к камере: она у нас стоит под фиксированным
            // углом, поэтому надпись достаточно повернуть один раз.
            go.transform.localRotation = Quaternion.Euler(35f, 50f, 0f);

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;

            // Мелко и низко. Прошлый размер (0.14) вешал над комнатой
            // вывеску в человеческий рост: подпись читалась издали, но
            // закрывала ровно то, ради чего примерочную и собирали.
            mesh.characterSize = 0.05f;
            mesh.fontSize = 48;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color32(0xFF, 0xD9, 0x6A, 0xFF);
        }

        private static void Place(string path, Transform parent, Vector3 local, float angle)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (asset == null)
            {
                Debug.LogWarning("[IsoRPG] Нет детали " + path);
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        /// <summary>
        /// Ширина детали в мировых единицах — по отрисованным границам.
        ///
        /// Через Renderer, а не через коллайдер: у декоративных деталей
        /// коллайдера может не быть вовсе, а нарисованные границы есть всегда.
        /// </summary>
        private static float MeasureWidth(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return 0f;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            float width = 0f;

            var renderers = instance.GetComponentsInChildren<Renderer>();

            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                width = Mathf.Max(bounds.size.x, bounds.size.z);
            }

            Object.DestroyImmediate(instance);
            return width;
        }
    }
}
