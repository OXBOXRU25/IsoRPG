using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Зверинец: расставляет новых существ рядом с местом появления героя,
    /// чтобы их можно было рассмотреть в игре и решить, брать ли.
    ///
    /// Это витрина, а не встраивание. Существа стоят со СВОИМИ анимациями
    /// из набора — дышат, оглядываются, — но драться не умеют и в бой не
    /// вступают: ни здоровья, ни добычи, ни выбора целью у них нет. Так и
    /// задумано. Полное встраивание монстра это контроллер под наши
    /// параметры, строка в таблице песочницы, портрет и таблица добычи —
    /// работа на несколько часов, и делать её до того, как ты сказал
    /// «берём», значит рисковать выкинуть её целиком.
    ///
    /// Всё в объекте "CreatureShowcase", убирается соседним пунктом меню.
    /// </summary>
    public static class CreatureShowcase
    {
        private const string Wolves = "Assets/Polygonal Wolf/Prefabs";
        private const string Ghouls = "Assets/BitGem/Ghoul-Crew-Hand-Painted-Series";
        private const string HolderName = "CreatureShowcase";

        /// <summary>
        /// Кто и под какой подписью. Порядок — по тому, как их удобно
        /// смотреть: сперва три волка одной породы, потом пятеро упырей.
        /// </summary>
        private static readonly (string path, string label)[] Cast =
        {
            (Wolves + "/Polygonal Wolf Brown.prefab",              "Волк бурый"),
            (Wolves + "/Polygonal Wolf Black.prefab",              "Волк чёрный"),
            (Wolves + "/Polygonal Wolf White.prefab",              "Волк белый"),

            (Ghouls + "/Ghoul/Prefabs/ghoul.prefab",                       "Упырь"),
            (Ghouls + "/Ghoul-Scavenger/Prefabs/ghoul_scavenger.prefab",   "Упырь-падальщик"),
            (Ghouls + "/Ghoul-Festering/Prefabs/ghoul_festering.prefab",   "Упырь гниющий"),
            (Ghouls + "/Ghoul-Grotesque/Prefabs/ghoul_grotesque.prefab",   "Упырь гротескный"),
            (Ghouls + "/Ghoul-Boss/Prefabs/ghoul_boss.prefab",             "Упырь-вожак"),
        };

        [MenuItem("Tools/IsoRPG/Зверинец: показать новых существ", priority = 42)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            Clear();

            var cells = FindOpenCells(Cast.Length);

            if (cells.Count < Cast.Length)
            {
                Debug.LogWarning("[IsoRPG] Свободных клеток рядом с залом нашлось " +
                                 cells.Count + " из " + Cast.Length +
                                 " — часть существ встанет теснее.");
            }

            var holder = new GameObject(HolderName);

            int placed = 0, missing = 0;

            for (int i = 0; i < Cast.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Cast[i].path);

                if (asset == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет существа " + Cast[i].path +
                                     " — набор не импортирован?");
                    missing++;
                    continue;
                }

                Vector3 at = cells.Count > 0
                    ? cells[Mathf.Min(i, cells.Count - 1)]
                    : RuinsLayout.HallCentre + new Vector3(i * 2.5f, 0f, 0f);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder.transform);
                go.transform.position = at;

                // Разворачиваем к югу — герой приходит оттуда, и существо
                // должно встретить его лицом, а не хвостом.
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                AddLabel(holder.transform, Cast[i].label, at + new Vector3(0f, 2.6f, 0f));
                placed++;
            }

            Selection.activeGameObject = holder;

            Debug.Log("[IsoRPG] Зверинец: поставлено " + placed +
                      (missing > 0 ? ", не найдено " + missing : "") +
                      ". Смотреть у зала, рядом с местом появления героя.");
        }

        [MenuItem("Tools/IsoRPG/Зверинец: убрать", priority = 43)]
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == HolderName);

            if (old != null) Object.DestroyImmediate(old);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Свободные клетки вокруг зала, по расширяющемуся кольцу.
        ///
        /// По карте, а не по прямой линии от героя: прямая почти наверняка
        /// упёрлась бы в стену, и половина зверинца оказалась бы внутри
        /// кладки. Карта уже знает, где пол, а где камень, — грех не
        /// спросить.
        ///
        /// Кольцами от середины зала: так существа встают кучно и рядом с
        /// местом появления, а не выстраиваются в цепочку через всё
        /// подземелье.
        /// </summary>
        private static List<Vector3> FindOpenCells(int count)
        {
            var found = new List<Vector3>();

            const int hallCol = 10;
            const int hallRow = 5;

            for (int ring = 1; ring <= 8 && found.Count < count; ring++)
            {
                for (int dc = -ring; dc <= ring && found.Count < count; dc++)
                {
                    for (int dr = -ring; dr <= ring && found.Count < count; dr++)
                    {
                        // Только внешнее кольцо: внутренние уже перебрали.
                        if (Mathf.Abs(dc) != ring && Mathf.Abs(dr) != ring) continue;

                        int col = hallCol + dc;
                        int row = hallRow + dr;

                        if (row < 0 || row >= RuinsLayout.Map.Length) continue;

                        string line = RuinsLayout.Map[row];
                        if (col < 0 || col >= line.Length) continue;

                        if (!RuinsLayout.IsOpen(line[col])) continue;

                        // Клетку под самим героем оставляем свободной, иначе
                        // он появится внутри волка.
                        if (dc == 2 && dr == 0) continue;

                        found.Add(RuinsLayout.CellToWorld(col, row));
                    }
                }
            }

            return found;
        }

        /// <summary>Подпись над существом, чтобы не гадать, кто из них кто.</summary>
        private static void AddLabel(Transform parent, string text, Vector3 at)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(parent, false);
            go.transform.position = at;

            // Камера у нас под неизменным углом, поэтому надпись достаточно
            // развернуть один раз, а не крутить каждый кадр.
            go.transform.rotation = Quaternion.Euler(35f, 50f, 0f);

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;

            // 0.08 при кегле 48 — примерно четверть мировой единицы на букву,
            // то есть около двадцати пикселей на нашем зуме. При 0.22 и 64,
            // с которых я начал, надписи выходили выше самих существ и
            // закрывали ровно то, ради чего их ставили.
            mesh.characterSize = 0.08f;
            mesh.fontSize = 48;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color32(0xFF, 0xD9, 0x6A, 0xFF);
        }
    }
}
