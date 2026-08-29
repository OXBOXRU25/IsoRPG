using System.Linq;
using IsoRPG.Dev;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Эталонная комната на POLYGON Dungeons: один зал, доведённый до
    /// «нравится», прежде чем строить из этого сто таких.
    ///
    /// Зачем именно так. Соблазн — сразу пересобрать всю локацию на новом
    /// наборе; цена ошибки — вся локация. Одна комната отвечает на все
    /// вопросы, которые иначе выясняются на сотне: сходится ли клетка,
    /// правильно ли встают детали с угловым пивотом, не мелок ли наш герой
    /// в их коридорах, хватает ли света от факелов, читается ли потолок из
    /// изометрии или он закрывает всё, ради чего комната и строилась.
    ///
    /// Ставится за восточным краем земли, на своей подложке. В игре клавиша
    /// Home переносит внутрь.
    ///
    /// Детали ставятся ПО ЗАМЕРУ, а не по формуле: у Synty точка отсчёта в
    /// углу детали, и вычислять сдвиг руками — значит ошибиться на полклетки
    /// в каждой из них. Меряем нарисованные границы и двигаем так, чтобы
    /// середина детали пришлась в середину клетки; это работает и для стен,
    /// и для полов, и для любой детали, которую туда положат позже.
    /// </summary>
    public static class SyntyRoom
    {
        private const string HolderName = "SyntyRoom";

        /// <summary>Размер комнаты в клетках, считая стены.</summary>
        private const int Width = 9;
        private const int Depth = 7;

        /// <summary>Высота потолка в метрах.</summary>
        private const float CeilingHeight = 5f;

        /// <summary>
        /// Ставить ли свод.
        ///
        /// Выключен, пока своды не замерены. Первый заход поставил их по
        /// формуле «пол плюс пять метров» — и зал превратился в груду
        /// перекрытий, под которой не построилась даже навигация. У Synty у
        /// каждой детали своя точка отсчёта, и потолок это не «пол, поднятый
        /// повыше».
        /// </summary>
        private const bool WithCeiling = false;

        // ------------------------------------------------------------------

        [MenuItem("Tools/IsoRPG/Эталонная комната Synty: собрать", priority = 52)]
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

            float cell = SyntyLayout.Cell;
            int placed = 0;

            for (int row = 0; row < Depth; row++)
            {
                for (int col = 0; col < Width; col++)
                {
                    Vector3 at = origin + new Vector3(col * cell, 0f, -row * cell);

                    bool north = row == 0;
                    bool south = row == Depth - 1;
                    bool west = col == 0;
                    bool east = col == Width - 1;
                    bool edge = north || south || west || east;

                    // Пол под всем, включая стены: обрыв кладки в пустоту
                    // читается как недоделка, а не как подземелье.
                    Place(Pick(SyntyLayout.Floors), holder.transform, at, 0f);
                    placed++;

                    if (!edge)
                    {
                        if (WithCeiling)
                        {
                            Place(Pick(SyntyLayout.CeilingsVaulted), holder.transform,
                                  at + Vector3.up * CeilingHeight, 0f);
                            placed++;
                        }

                        // Две колонны в зале: они держат свод по смыслу и
                        // дают укрытие от стрел по игре.
                        if ((col == 2 || col == Width - 3) && row == Depth / 2)
                        {
                            Place(Pick(SyntyLayout.Pillars), holder.transform, at, 0f);
                            placed++;
                        }

                        continue;
                    }

                    // ---- стены по периметру ------------------------------

                    // Угол: у Synty угловой детали нет вовсе, стены стыкуются
                    // встык. Ставим две — по одной на каждую сторону.
                    bool corner = (north || south) && (west || east);

                    float angle = (north || south) ? 0f : 90f;

                    // Вход — посередине южной стены, двойной проём: в него
                    // проходят вдвоём и не застревают на повороте.
                    bool door = south && col == Width / 2;

                    // Окна в северной стене: сквозь них видно, что снаружи
                    // есть мир, и комната перестаёт быть коробкой.
                    bool window = north && (col == 2 || col == Width - 3);

                    string prefab =
                        door ? SyntyLayout.DoorwayDouble :
                        window ? Pick(SyntyLayout.Windows) :
                        Pick(SyntyLayout.Walls);

                    Place(prefab, holder.transform, at, angle);
                    placed++;

                    if (corner)
                    {
                        Place(Pick(SyntyLayout.Walls), holder.transform, at, 90f);
                        placed++;
                    }

                    // Факелы через две клетки по внутренней стороне: свет
                    // пятнами, а не ровной заливкой, — так подземелье и
                    // выглядит подземельем.
                    if (!corner && !door && col % 3 == 1)
                    {
                        Vector3 inward = north ? Vector3.back
                                       : south ? Vector3.forward
                                       : west ? Vector3.right : Vector3.left;

                        var torch = Place(SyntyLayout.Torch, holder.transform,
                                          at + inward * (cell * 0.42f) + Vector3.up * 2.6f,
                                          angle + (north || west ? 180f : 0f));

                        AddFire(torch);
                        placed++;
                    }
                }
            }

            Pad(holder, origin, cell);

            var entrance = origin + new Vector3((Width / 2) * cell, 0f, -(Depth - 1) * cell + cell);
            Jumper(entrance);

            Rebake();

            Selection.activeGameObject = holder;

            Debug.Log("[IsoRPG] Эталонная комната Synty собрана: " + Width + "×" + Depth +
                      " клеток по " + cell + " м, деталей " + placed +
                      ". В игре Home переносит внутрь.");
        }

        [MenuItem("Tools/IsoRPG/Эталонная комната Synty: убрать", priority = 53)]
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == HolderName);

            if (old != null) Object.DestroyImmediate(old);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Ставит деталь так, чтобы её середина пришлась в середину клетки.
        ///
        /// Через замер, а не через константу сдвига: у Synty пивот в углу
        /// детали, но у разных деталей по-разному (у ниши, например, в
        /// центре). Замер отвечает за каждую деталь отдельно и не врёт.
        /// </summary>
        private static GameObject Place(string path, Transform parent, Vector3 at, float angle)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (asset == null)
            {
                Debug.LogWarning("[IsoRPG] Нет детали " + path);
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, angle, 0f);

            var renderers = go.GetComponentsInChildren<Renderer>()
                              .Where(r => !(r is ParticleSystemRenderer))
                              .ToArray();

            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                Vector3 shift = at - bounds.center;
                go.transform.position += new Vector3(shift.x, 0f, shift.z);
            }

            return go;
        }

        /// <summary>
        /// Живой огонь у факела.
        ///
        /// Ровный свет читается как лампа. Настоящий факел дышит: мерцание
        /// в пределах пятнадцати процентов с разной фазой у каждого — и зал
        /// перестаёт быть музеем. Стоит это одного компонента.
        /// </summary>
        private static void AddFire(GameObject torch)
        {
            if (torch == null) return;

            var lightGo = new GameObject("Fire");
            lightGo.transform.SetParent(torch.transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.3f;

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color32(0xFF, 0xB0, 0x60, 0xFF);
            light.intensity = 2.4f;
            light.range = 9f;
            light.shadows = LightShadows.None;

            lightGo.AddComponent<FlickerLight>();
        }

        /// <summary>Подложка под комнатой: за краем земли её нет.</summary>
        private static void Pad(GameObject holder, Vector3 origin, float cell)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Pad";
            floor.transform.SetParent(holder.transform, true);

            floor.transform.position = new Vector3(origin.x + (Width - 1) * cell * 0.5f,
                                                   -0.3f,
                                                   origin.z - (Depth - 1) * cell * 0.5f);

            floor.transform.localScale = new Vector3(Width * cell + 10f, 0.4f, Depth * cell + 10f);

            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Materials/M_ShowcasePad.mat");

            if (material != null) floor.GetComponent<Renderer>().sharedMaterial = material;

            floor.GetComponent<Renderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Где ставим — за восточным краем земли.
        ///
        /// От края ЗЕМЛИ, а не от карты руин: разница в восемьдесят метров,
        /// и отсчёт не от того края уже однажды положил витрину поверх
        /// игровой площади.
        /// </summary>
        private static Vector3 Origin()
        {
            float edge = 115f;

            var ground = GameObject.Find("Ground");
            var renderer = ground != null ? ground.GetComponent<Renderer>() : null;

            if (renderer != null) edge = renderer.bounds.max.x;

            return new Vector3(edge + 40f, 0f, 30f);
        }

        private static void Jumper(Vector3 entrance)
        {
            var jumper = Object.FindFirstObjectByType<ShowcaseJumper>();

            if (jumper == null)
            {
                var carrier = new GameObject("SyntyRoomJumper");
                jumper = carrier.AddComponent<ShowcaseJumper>();
                jumper.Home = RuinsLayout.HallCentre;
            }

            jumper.Room = entrance;
            jumper.RoomTitle = "эталонная комната Synty";

            EditorUtility.SetDirty(jumper);
        }

        private static void Rebake()
        {
            var ground = GameObject.Find("Ground");
            var surface = ground != null ? ground.GetComponent<NavMeshSurface>() : null;

            if (surface == null)
            {
                Debug.LogWarning("[IsoRPG] Нет NavMeshSurface на Ground — по комнате не походить.");
                return;
            }

            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);
        }

        private static string Pick(string[] set) => set[Random.Range(0, set.Length)];
    }
}
