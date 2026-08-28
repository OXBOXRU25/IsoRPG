using System.Linq;
using IsoRPG.Dev;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Пробное подземелье: чужая планировка, поставленная рядом с миром,
    /// чтобы посмотреть её ногами.
    ///
    /// Карта нарисована Павлом в отдельном разговоре по нашему же формату —
    /// и это ровно то, ради чего формат заводился: планировка правится
    /// глазом, без единой строчки кода. Ставится тем же строителем, что и
    /// наши руины (<see cref="EnvironmentBuilder.BuildMap"/>), поэтому здесь
    /// нет ни своего словаря символов, ни своих правил: стены, углы, факелы
    /// и мебель подставляются одинаково.
    ///
    /// Стоит за северным краем земли, на своём месте: 40 на 28 клеток это
    /// 160 на 112 метров, столько свободного места внутри карты нет — руины
    /// занимают середину, вокруг лес.
    ///
    /// Убирается соседним пунктом меню, следов не оставляет.
    /// </summary>
    public static class DungeonProbe
    {
        private const string HolderName = "DungeonProbe";

        /// <summary>
        /// План: два больших зала наверху, жилые комнаты в середине, три
        /// малых помещения внизу. Все внутренние стены двойные, проёмы 2×2.
        /// </summary>
        private static readonly string[] Map =
        {
            " ,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,, ",
            " ,###T##WW##T###WW###T#########T######, ",
            " ,#c.....................##SS......SS#, ",
            " ,#.......x..............##K.......cS#, ",
            " ,#....o........o........##..k.....S.T, ",
            " ,T..........l...........##..........#, ",
            " ,#....o........o......x.##SS.....b.S#, ",
            " ,#......................##..........#, ",
            " ,#.........kk...........##S.......SS#, ",
            " ,##########DD#################DD#####, ",
            " ,######T###DD######T##########DD#T###, ",
            " ,#........k....k........##.........B#, ",
            " ,T...o...o...o...o...o..DD..m.C.....T, ",
            " ,W.......x......x.......DD..C......BW, ",
            " ,W.........ll...........WW....b.....W, ",
            " ,#...k..............k...DD..h......B#, ",
            " ,T...o...o...o...o...o..DD..l.......T, ",
            " ,#..........x...........##....h....B#, ",
            " ,###DD#####T#####DD##################, ",
            " ,###DD#T#########DD##T##########T###, ",
            " ,#kk......##......x.......##S.K....c#, ",
            " ,T.l..h...DD..o........o..DDKK....S.#, ",
            " ,#..h.....DD..o..k..k..o..DDS.....K.T, ",
            " ,#b.......##.....x........##SS..b..S#, ",
            " ,###WW#######T###DD###T########WW####, ",
            " ,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,, ",
            " ,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,, ",
            " ,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,, ",
        };

        /// <summary>Отступ от северного края земли.</summary>
        private const float Gap = 40f;

        // ------------------------------------------------------------------

        [MenuItem("Tools/IsoRPG/Пробное подземелье: собрать", priority = 49)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            Clear();

            var holder = new GameObject(HolderName);

            Vector3 origin = Origin();
            holder.transform.position = Vector3.zero;

            EnvironmentBuilder.BuildMap(holder.transform, Map, origin, "Plan");

            // Вход — южный край плана, оттуда игрок и приходит.
            //
            // Считаем от самой карты, а не числом: она ещё будет меняться, и
            // записанная руками точка входа окажется внутри стены на первой
            // же правке.
            float cell = 4f;
            int rows = Map.Length;
            int cols = Map.Max(line => line.Length);

            // Внутрь плана, а не перед ним: за южным краем земли нет вовсе,
            // навигация там не строится, и перенос молча не сработал бы —
            // клавиша нажимается, герой стоит.
            var entrance = origin + new Vector3(0f, 0f, -(rows - 1) * cell * 0.5f + cell);

            Jumper(entrance);
            Rebake();

            Selection.activeGameObject = holder;

            Debug.Log("[IsoRPG] Пробное подземелье собрано: " + cols + "×" + rows +
                      " клеток (" + (cols * cell) + "×" + (rows * cell) + " м), центр " +
                      origin + ". В игре клавиша End переносит ко входу, F10 — обратно в зал.");
        }

        [MenuItem("Tools/IsoRPG/Пробное подземелье: убрать", priority = 50)]
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == HolderName);

            if (old != null) Object.DestroyImmediate(old);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Где ставим — за северным краем ЗЕМЛИ, а не карты руин.
        ///
        /// Разница дорогая: карта руин кончается на Z ≈ 32, а земля под ней
        /// тянется до 115. Отступ, отсчитанный от руин, положил бы подземелье
        /// прямо на траву, и оно замерцало бы, споря с землёй за пиксели —
        /// на витрине этот урок уже стоил вечера.
        /// </summary>
        private static Vector3 Origin()
        {
            float edge = RuinsLayout.CellToWorld(0, 0).z;

            var ground = GameObject.Find("Ground");
            var renderer = ground != null ? ground.GetComponent<Renderer>() : null;

            if (renderer != null) edge = renderer.bounds.max.z;
            else Debug.LogWarning("[IsoRPG] Не нашёл Ground — отступ считаю от карты руин.");

            float depth = Map.Length * 4f;

            return new Vector3(0f, 0f, edge + Gap + depth * 0.5f);
        }

        /// <summary>
        /// Прописывает вход в перенос по клавишам.
        ///
        /// Если витрина в сцене — дополняем её носитель, чтобы в сцене не
        /// оказалось двух компонентов, слушающих одни и те же клавиши: они
        /// сработали бы оба, и герой уехал бы в последнее из двух мест.
        /// </summary>
        private static void Jumper(Vector3 entrance)
        {
            var jumper = Object.FindFirstObjectByType<ShowcaseJumper>();

            if (jumper == null)
            {
                var carrier = new GameObject("DungeonProbeJumper");
                jumper = carrier.AddComponent<ShowcaseJumper>();
                jumper.Home = RuinsLayout.HallCentre;
            }

            jumper.Extra = entrance;
            jumper.ExtraTitle = "пробное подземелье";

            EditorUtility.SetDirty(jumper);
        }

        /// <summary>
        /// Перепекает навигацию: по подземелью надо ходить, а стены обязаны
        /// быть стенами — иначе проверка планировки ничего не проверяет.
        /// </summary>
        private static void Rebake()
        {
            var ground = GameObject.Find("Ground");

            if (ground == null)
            {
                Debug.LogWarning("[IsoRPG] Нет объекта Ground — навигацию не перепёк.");
                return;
            }

            var surface = ground.GetComponent<NavMeshSurface>();

            if (surface == null)
            {
                Debug.LogWarning("[IsoRPG] На Ground нет NavMeshSurface — собери песочницу заново.");
                return;
            }

            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);

            Debug.Log("[IsoRPG] Навигация перепечена вместе с подземельем.");
        }
    }
}
