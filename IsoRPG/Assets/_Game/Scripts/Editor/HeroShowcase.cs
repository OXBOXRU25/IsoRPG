using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Витрина кандидатов в герои: ряд персонажей в отдельной сцене.
    ///
    /// Смысл — показать РАНЬШЕ, чем встраивать. Дважды подряд я тратил по
    /// полтора часа на интеграцию модели (материалы в конвейер, риг, аватар,
    /// аниматор, сборка), и оба раза заказчик смотрел десять секунд и говорил
    /// «убирай». Вопрос «нравится ли» решается кадром, вопрос «заработает ли» —
    /// часами; отвечать надо в таком порядке.
    ///
    /// Сцена отдельная и намеренно: боевую арену проба не трогает вовсе,
    /// откатывать нечего.
    ///
    /// Камера и свет — наши, игровые. Смотреть модель под её собственным
    /// студийным светом бессмысленно: у нас вид сверху под 35 градусами и
    /// ровный дневной свет, и решает то, как персонаж читается именно так.
    /// </summary>
    public static class HeroShowcase
    {
        private const string ScenePath = "Assets/_Game/Scenes/HeroShowcase.unity";

        private const string Presets =
            "Assets/PolygonFantasyHeroCharacters/Prefabs/Characters_Presets";

        // Углы камеры ровно как в игре.
        private const float CameraPitch = 35f;
        private const float CameraYaw = 50f;

        /// <summary>Сколько персонажей показываем за раз.</summary>
        private const int Count = 8;

        /// <summary>
        /// Витрина разбойников: явные кандидаты из наших наборов.
        ///
        /// Отдельно от общей витрины, потому что вопрос другой. Там — «кто из
        /// ста двадцати», здесь — «есть ли вообще разбойник». Имена у Synty
        /// размечены жёстко, поэтому кандидатов находим поиском, а не глазами.
        /// Один из них назван Rouge вместо Rogue: опечатка автора, и по
        /// правильному слову он не ищется.
        /// </summary>
        /// <summary>
        /// Один персонаж крупно, с трёх сторон.
        ///
        /// Имя берётся из файла-подсказки рядом со сценой: так его можно
        /// сменить, не трогая код и не пересобирая проект.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Витрина: один крупно", priority = 46)]
        public static void One()
        {
            if (EditorApplication.isPlaying) return;

            string name = "SM_Chr_Male_Rouge_01";
            const string hint = "Assets/_Game/Scenes/one-hero.txt";

            if (System.IO.File.Exists(hint))
            {
                var text = System.IO.File.ReadAllText(hint).Trim();
                if (!string.IsNullOrEmpty(text)) name = text;
            }

            var guid = AssetDatabase.FindAssets(name + " t:Prefab").FirstOrDefault();

            if (guid == null)
            {
                Debug.LogWarning("[IsoRPG] Не найден префаб " + name);
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(guid));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Ground();
            Sun();

            // Три копии: спереди, в три четверти и со спины. Персонажа в игре
            // видно в основном сзади и сбоку, а выбирают его обычно по виду
            // спереди — показываем все три сразу.
            var angles = new[] { 0f, 140f, 180f };
            var labels = new[] { "спина", "три четверти", "лицо" };

            for (int i = 0; i < angles.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3((i - 1) * 1.1f, 0f, 0f);
                go.transform.rotation = Quaternion.Euler(0f, CameraYaw + angles[i], 0f);
                go.name = labels[i];
            }

            Camera(1.35f);

            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/OneHero.unity");

            Debug.Log("[IsoRPG] Крупный показ: " + name +
                      ". Слева направо: спина, три четверти, лицо.");
        }

        [MenuItem("Tools/IsoRPG/Витрина разбойников", priority = 45)]
        public static void Rogues()
        {
            if (EditorApplication.isPlaying) return;

            var names = new[]
            {
                "SM_Chr_Rogue_Female_01",
                "SM_Chr_Male_Rouge_01",
                "SM_Chr_Hero_Knight_Male_01",
                "SM_Chr_Commoner_Male_01",
            };

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Ground();
            Sun();

            float spacing = 1.5f;
            float startX = -(names.Length - 1) * spacing * 0.5f;
            int placed = 0;

            for (int i = 0; i < names.Length; i++)
            {
                var guid = AssetDatabase.FindAssets(names[i] + " t:Prefab").FirstOrDefault();

                if (guid == null)
                {
                    Debug.LogWarning("[IsoRPG] Не найден " + names[i]);
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3(startX + i * spacing, 0f, 0f);
                go.transform.rotation = Quaternion.Euler(0f, CameraYaw + 180f, 0f);
                go.name = (i + 1) + ". " + names[i];

                placed++;
            }

            Camera(2.6f);

            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/RogueShowcase.unity");

            Debug.Log("[IsoRPG] Витрина разбойников: " + placed + " из " + names.Length +
                      ". Слева направо: " + string.Join(", ", names));
        }

        /// <summary>
        /// Витрина стартовых персонажей Sidekick.
        ///
        /// Набор ещё выбирается, поэтому смотрим его в пустой сцене под
        /// нашим светом, а не в арене: в боевую сцену то, что не решено,
        /// не тащим. Здесь сразу видно главное — не розовые ли материалы
        /// в URP 17 и как эти персонажи читаются рядом с нашими.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Витрина Sidekick", priority = 46)]
        public static void Sidekicks()
        {
            if (EditorApplication.isPlaying) return;

            var names = new[]
            {
                "Starter_01",
                "Starter_02",
                "Starter_03",
                "Starter_04",
            };

            SidekickMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Ground();
            Sun();

            float spacing = 1.5f;
            float startX = -(names.Length - 1) * spacing * 0.5f;
            int placed = 0;

            for (int i = 0; i < names.Length; i++)
            {
                var guid = AssetDatabase.FindAssets(names[i] + " t:Prefab").FirstOrDefault();

                if (guid == null)
                {
                    Debug.LogWarning("[IsoRPG] Не найден " + names[i]);
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3(startX + i * spacing, 0f, 0f);
                go.transform.rotation = Quaternion.Euler(0f, CameraYaw + 180f, 0f);
                go.name = (i + 1) + ". " + names[i];

                placed++;
            }

            Camera(2.6f);

            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/SidekickShowcase.unity");

            Debug.Log("[IsoRPG] Витрина Sidekick: " + placed + " из " + names.Length +
                      ". Слева направо: " + string.Join(", ", names));
        }

        /// <summary>
        /// Чинит розовые материалы Sidekick.
        ///
        /// В стартовом наборе нет ни одного шейдера: материалы ссылаются на
        /// шейдер из инструмента Sidekick, который качается отдельно с
        /// GitHub. Без него Unity подставляет свой аварийный — отсюда
        /// сплошная малиновая заливка. Дело НЕ в URP 17, как выглядит на
        /// первый взгляд.
        ///
        /// Смотреть набор ради этого через загрузку стороннего пакета
        /// незачем: у Synty вся раскраска лежит в одной цветовой карте,
        /// и обычный URP/Lit с этой картой показывает персонажа верно.
        /// Если набор возьмём — поставим родной шейдер и эта починка
        /// сама перестанет срабатывать: она трогает только материалы с
        /// потерянным шейдером.
        /// </summary>
        private static void SidekickMaterials()
        {
            const string Root = "Assets/Synty/SidekickCharacters";

            if (!System.IO.Directory.Exists(Root)) return;

            var lit = Shader.Find("Universal Render Pipeline/Lit");

            if (lit == null)
            {
                Debug.LogWarning("[IsoRPG] Нет шейдера URP/Lit — чинить материалы нечем.");
                return;
            }

            int repaired = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (mat == null) continue;

                // Потерянный шейдер Unity подменяет аварийным. Целые
                // материалы не трогаем.
                if (mat.shader != null && mat.shader.name != "Hidden/InternalErrorShader") continue;

                mat.shader = lit;

                // Цветовая карта лежит в соседней папке Textures.
                string folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                string parent = folder.Substring(0, folder.LastIndexOf('/'));

                var texGuid = AssetDatabase
                    .FindAssets("ColorMap t:Texture2D", new[] { parent })
                    .FirstOrDefault();

                if (texGuid != null)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        AssetDatabase.GUIDToAssetPath(texGuid));

                    if (tex != null) mat.SetTexture("_BaseMap", tex);
                }

                EditorUtility.SetDirty(mat);
                repaired++;
            }

            if (repaired > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[IsoRPG] Материалов Sidekick переведено на URP/Lit: " + repaired);
            }
        }

        [MenuItem("Tools/IsoRPG/Витрина героев: собрать", priority = 39)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play сцена не сохранится.");
                return;
            }

            var all = AssetDatabase.FindAssets("t:Prefab", new[] { Presets })
                                   .Select(AssetDatabase.GUIDToAssetPath)
                                   .OrderBy(p => p)
                                   .ToArray();

            if (all.Length == 0)
            {
                Debug.LogWarning("[IsoRPG] Пресетов не найдено в " + Presets);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Ground();
            Sun();

            // Берём равномерно по всему списку, а не первые восемь: пресеты
            // отсортированы по номеру, и подряд идущие похожи друг на друга.
            int step = Mathf.Max(1, all.Length / Count);
            float spacing = 1.6f;
            float startX = -(Count - 1) * spacing * 0.5f;
            int placed = 0;

            for (int i = 0; i < Count; i++)
            {
                string path = all[Mathf.Min(i * step, all.Length - 1)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = new Vector3(startX + i * spacing, 0f, 0f);

                // Разворачиваем лицом к камере: иначе половина ряда окажется
                // спиной, и судить будет не о чем.
                go.transform.rotation = Quaternion.Euler(0f, CameraYaw + 180f, 0f);

                placed++;
            }

            // Капсула прежнего роста — линейка. Без неё непонятно, крупнее
            // персонаж нашего или мельче.
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "Наш рост, 1.9 м";
            capsule.transform.position = new Vector3(startX - spacing * 1.4f, 0.95f, 0f);
            capsule.transform.localScale = new Vector3(0.7f, 0.95f, 0.7f);

            var shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader != null)
            {
                var m = new Material(shader);
                m.color = new Color(0.8f, 0.3f, 0.25f);
                capsule.GetComponent<Renderer>().sharedMaterial = m;
            }

            Camera(placed * spacing * 0.62f);

            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[IsoRPG] Витрина героев собрана: " + placed + " из " + all.Length +
                      " пресетов, слева капсула нашего роста. Снимок: " +
                      "Tools/IsoRPG/Глаз: снять открытую сцену.");
        }

        private static void Ground()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;

            var m = new Material(shader);
            m.color = new Color(0.38f, 0.42f, 0.30f);
            m.SetFloat("_Smoothness", 0.05f);
            ground.GetComponent<Renderer>().sharedMaterial = m;
        }

        private static void Sun()
        {
            var go = new GameObject("Sun", typeof(Light));
            var light = go.GetComponent<Light>();

            // Те же числа, что в игре после настройки по эталону набора леса.
            light.type = LightType.Directional;
            light.intensity = 2.4f;
            light.color = new Color(1.000f, 0.945f, 0.855f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;

            go.transform.rotation = Quaternion.Euler(52f, 145f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.55f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.80f, 0.74f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.31f, 0.26f);
            RenderSettings.ambientIntensity = 1.15f;
            RenderSettings.fog = false;
        }

        private static void Camera(float size)
        {
            var go = new GameObject("Main Camera", typeof(UnityEngine.Camera));
            go.tag = "MainCamera";

            var cam = go.GetComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(2.2f, size);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.68f, 0.82f);

            go.transform.rotation = Quaternion.Euler(CameraPitch, CameraYaw, 0f);
            go.transform.position = new Vector3(0f, 1f, 0f) - go.transform.forward * 20f;
        }
    }
}
