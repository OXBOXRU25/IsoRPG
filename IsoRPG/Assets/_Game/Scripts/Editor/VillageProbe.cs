using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Пробная деревня из ГОТОВЫХ построек Synty.
    ///
    /// Тут развернулись на сто восемьдесят градусов, и стоит записать
    /// почему. Сперва я собирал зал из кирпичей — стена, стена, проём — и
    /// получилась коробка, которую сам заказчик назвал страшной. А в демо
    /// набора лежит `Preset_Buildings_Group`: тридцать три постройки уже
    /// собраны целиком — дома, кузница, церковь, мельница, хижины, с
    /// крышами, трубами, навесами и балконами. Их мы вытащили в свои
    /// префабы и теперь ставим домами, а не кирпичами.
    ///
    /// Композиция — улица, а не сетка. Дома вдоль дороги фасадами внутрь,
    /// площадь с фонтаном в конце, деревья и лавки в промежутках. Ровная
    /// сетка читается как склад; улица читается как место, где живут, — и
    /// это ровно та разница, из-за которой их промо выглядит городом, а моя
    /// первая попытка выглядела коробкой.
    ///
    /// Стоит НА нашей карте, к северу от руин: до неё можно дойти ногами,
    /// и видно её в нашей камере, с нашим светом и рядом с нашим героем.
    /// Ради этого всё и затевалось — набор надо смотреть в игре, а не в
    /// чужой демо-сцене.
    /// </summary>
    public static class VillageProbe
    {
        private const string HolderName = "VillageProbe";
        private const string Prefabs = "Assets/_Game/Prefabs/Synty";

        /// <summary>Середина деревни на нашей карте.</summary>
        private static readonly Vector3 Centre = new Vector3(0f, 0f, 72f);

        /// <summary>Ширина улицы между рядами домов.</summary>
        private const float Street = 16f;

        /// <summary>Шаг между домами вдоль улицы.</summary>
        private const float Step = 14f;

        /// <summary>
        /// Северный ряд — зажиточный: церковь, кузница, крупные дома.
        /// Южный — простой: хижины и сараи. Улица от этого читается как
        /// улица, а не как каталог: на ней есть «богатая сторона» и
        /// «бедная», то есть история.
        /// </summary>
        private static readonly string[] North =
        {
            "Preset_Church_01_A", "Preset_House_01_A", "Preset_Blacksmith_01",
            "Preset_House_04", "Preset_House_09_C",
        };

        private static readonly string[] South =
        {
            "Preset_Hut_01", "Preset_House_02_C", "Preset_Hut_02",
            "Preset_House_05", "Preset_Shelter_01",
        };

        /// <summary>Чем заполняем промежутки между домами.</summary>
        private static readonly string[] Between =
        {
            "SM_Env_Tree_Large_01", "SM_Env_Tree_Round_04", "SM_Env_Tree_Thin_02",
            "SM_Prop_Bench_Seat_01", "SM_Prop_Bench_Seat_02",
        };

        // ------------------------------------------------------------------

        [MenuItem("Tools/IsoRPG/Пробная деревня: собрать", priority = 57)]
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
            int placed = 0;

            // Дома фасадами на улицу: северный ряд смотрит на юг, южный — на
            // север. Угол подбирается по факту после первого взгляда: у
            // разных наборов «лицо» модели смотрит в разные стороны, и
            // угадывать это бессмысленно.
            for (int i = 0; i < North.Length; i++)
            {
                float x = (i - (North.Length - 1) * 0.5f) * Step;

                placed += Put(North[i], holder.transform,
                              Centre + new Vector3(x, 0f, Street * 0.5f), 180f);
            }

            for (int i = 0; i < South.Length; i++)
            {
                float x = (i - (South.Length - 1) * 0.5f) * Step;

                placed += Put(South[i], holder.transform,
                              Centre + new Vector3(x, 0f, -Street * 0.5f), 0f);
            }

            // Фонтан в торце улицы: точка, к которой сходится взгляд.
            placed += Put("SM_Env_Fountain_01", holder.transform,
                          Centre + new Vector3((North.Length * 0.5f + 0.5f) * Step, 0f, 0f), 0f);

            // Мелочь в промежутках. Не в ряд: деревья и лавки, стоящие по
            // линейке, читаются как забор.
            var random = new System.Random(7);

            for (int i = 0; i < 14; i++)
            {
                string what = Between[random.Next(Between.Length)];

                float x = (float)(random.NextDouble() - 0.5) * Step * North.Length;
                float z = (random.Next(2) == 0 ? 1f : -1f) * (Street * 0.5f + 6f + (float)random.NextDouble() * 5f);

                placed += Put(what, holder.transform, Centre + new Vector3(x, 0f, z),
                              random.Next(360));
            }

            Rebake();

            Selection.activeGameObject = holder;

            Debug.Log("[IsoRPG] Пробная деревня собрана: деталей " + placed +
                      ", середина " + Centre + ". Идти от зала на север.");
        }

        [MenuItem("Tools/IsoRPG/Пробная деревня: убрать", priority = 58)]
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == HolderName);

            if (old != null) Object.DestroyImmediate(old);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Ставит постройку, сажая её на землю по нарисованным границам.
        ///
        /// У вытащенных из чужой сцены построек точка отсчёта где угодно —
        /// они собирались под свой рельеф. Без посадки половина домов
        /// оказалась бы вкопанной по окна, а половина висела бы в воздухе.
        /// </summary>
        private static int Put(string name, Transform parent, Vector3 at, float angle)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + name + ".prefab");

            if (asset == null)
            {
                Debug.LogWarning("[IsoRPG] Нет постройки " + name);
                return 0;
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

                // По низу на землю, по горизонтали — серединой в точку.
                go.transform.position += new Vector3(at.x - bounds.center.x,
                                                     -bounds.min.y,
                                                     at.z - bounds.center.z);
            }

            return 1;
        }

        private static void Rebake()
        {
            var ground = GameObject.Find("Ground");

            if (ground == null)
            {
                Debug.LogWarning("[IsoRPG] Нет объекта Ground — навигацию не перепёк.");
                return;
            }

            // Компонент добавляем, если его нет.
            //
            // Раньше здесь стоял отказ «нет NavMeshSurface — по деревне не
            // походить», и это оказалось капризом: сцена собиралась разными
            // сборщиками в разное время, и компонент мог не сохраниться.
            // Добавить его стоит одной строки, а без навигации проверка
            // локации теряет смысл — по ней надо ХОДИТЬ.
            NavBake.Rebake();
        }
    }
}
