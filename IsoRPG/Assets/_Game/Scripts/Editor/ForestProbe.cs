using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит два леса в один кадр: наш нынешний и реалистичный TriForge.
    ///
    /// Зачем отдельная сцена, а не «поставим и посмотрим в песочнице». Смена
    /// леса необратима: снести 180 деревьев легко, вернуть их на те же места
    /// нельзя — раскладка лежит в бинарном файле сцены. Значит решение
    /// принимается ДО сноса, и принимается по кадру, а не по описанию.
    ///
    /// Два набора нарисованы на разных языках, и это надо увидеть рядом:
    ///   • Synty и PNB — плоские палитровые текстуры, гранёные силуэты,
    ///     цвет несёт всю работу, теней внутри кроны почти нет;
    ///   • TriForge — фотографическая кора и листва, карты нормалей,
    ///     объём внутри кроны, приглушённая натурная гамма.
    ///
    /// Между рядами стоит наш герой. Он тут не для красоты: стилевой
    /// разрыв виден именно на стыке персонажа с фоном, а не между двумя
    /// деревьями. И заодно он даёт масштаб — наборы разных авторов
    /// расходятся по росту в разы, и это тоже решается замером, а не глазом.
    /// </summary>
    public static class ForestProbe
    {
        private const string ScenePath = "Assets/_Game/Scenes/ForestProbe.unity";

        // Камера ровно как в игре: углы сняты с Albion и живут в StyleCompare.
        private const float CameraPitch = 35f;
        private const float CameraYaw = 50f;

        // Модель нашего героя — та же, что в Player.prefab. Набор KayKit из
        // проекта убран, и брать оттуда «кого-нибудь для масштаба» нельзя:
        // проба обязана показывать то, что реально стоит в игре.
        private const string Hero =
            "Assets/PolygonElvenRealm/Prefabs/Characters/SM_Chr_Commoner_Male_01.prefab";

        /// <summary>
        /// Наш нынешний лес — те самые деревья, что стоят в песочнице.
        /// Берём наши обёртки из _Game/Prefabs, а не оригиналы набора:
        /// в сцене стоят именно они, вместе со своими материалами.
        /// </summary>
        private static readonly (string path, string label)[] Ours =
        {
            ("Assets/_Game/Prefabs/Synty/SM_Env_Tree_Large_01.prefab", "Synty Большое"),
            ("Assets/_Game/Prefabs/Synty/SM_Env_Tree_Round_04.prefab", "Synty Круглое"),
            ("Assets/_Game/Prefabs/Synty/SM_Env_Tree_Thin_03.prefab",  "Synty Тонкое"),
            ("Assets/_Game/Prefabs/Synty/SM_Env_Tree_Thin_02.prefab",  "Synty Тонкое 2"),
        };

        /// <summary>Реалистичный набор — кандидат на замену.</summary>
        private static readonly (string path, string label)[] Theirs =
        {
            ("Assets/TriForge Assets/Fantasy Forest Environment/Prefabs/Trees/P_FFE_Tree_1.Prefab",    "FFE Дерево 1"),
            ("Assets/TriForge Assets/Fantasy Forest Environment/Prefabs/Trees/P_FFE_Tree_2.Prefab",    "FFE Дерево 2"),
            ("Assets/TriForge Assets/Fantasy Forest Environment/Prefabs/Trees/P_FFE_Spruce_B1.Prefab", "FFE Ель B1"),
            ("Assets/TriForge Assets/Fantasy Forest Environment/Prefabs/Trees/P_FFE_Birch_3.Prefab",   "FFE Берёза 3"),
        };

        [MenuItem("Tools/IsoRPG/Лес: проба TriForge рядом с нашим", priority = 56)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play сцена не сохраняется на диск.", "Понятно");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateGround();
            CreateLight();
            BeautifulSky.Apply();

            var report = new List<string>();

            // Наши — ближний ряд, кандидат — дальний. Так на одном кадре
            // видно и силуэты обоих, и то, как они читаются на расстоянии.
            float tallest = 0f;
            tallest = Mathf.Max(tallest, PlaceRow(Ours,   -9f, "Наш лес",  report));
            tallest = Mathf.Max(tallest, PlaceRow(Theirs,  9f, "TriForge", report));

            // По рыцарю к каждому ряду. Один в середине поля не отвечает на
            // главный вопрос — он ни на чьём фоне не стоит.
            PlaceHero(new Vector3(-3f, 0f, -5f), "Герой у нашего леса",  report);
            PlaceHero(new Vector3(-3f, 0f,  5f), "Герой у TriForge",     report);

            // Рамка кадра считается по самому высокому дереву, а не берётся
            // числом: наборы расходятся по росту, и жёсткий Size обрезал бы
            // кроны ровно у того набора, который выше.
            CreateCamera(Mathf.Max(10f, tallest * 0.85f));

            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[IsoRPG] Проба леса собрана. Ближний ряд — наш, дальний — TriForge.\n" +
                      string.Join("\n", report) +
                      "\n\nСнимок: Tools/IsoRPG/Глаз: снять открытую сцену.");
        }

        /// <summary>
        /// Ряд деревьев вдоль оси X. Возвращает высоту самого высокого —
        /// она нужна камере.
        /// </summary>
        private static float PlaceRow((string path, string label)[] set, float z,
                                      string groupName, List<string> report)
        {
            var group = new GameObject(groupName);
            group.transform.position = Vector3.zero;

            float step = 7f;
            float startX = -(set.Length - 1) * step * 0.5f;
            float tallest = 0f;

            for (int i = 0; i < set.Length; i++)
            {
                var (path, label) = set[i];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    report.Add(groupName + " · " + label + " — НЕ НАЙДЕНО: " + path);
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = label;
                go.transform.SetParent(group.transform, false);
                go.transform.position = new Vector3(startX + i * step, 0f, z);

                float h = Measure(go);
                tallest = Mathf.Max(tallest, h);
                report.Add(groupName + " · " + label + " — высота " + h.ToString("0.0") + " м");
            }

            return tallest;
        }

        /// <summary>
        /// Наш персонаж между рядами. Его рост — единственная величина в
        /// кадре, которую мы знаем наверняка, поэтому от него и меряется,
        /// насколько чужие деревья великоваты или мелковаты.
        /// </summary>
        private static void PlaceHero(Vector3 at, string name, List<string> report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hero);
            if (prefab == null)
            {
                report.Add("Модель героя не найдена: " + Hero);
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, CameraYaw + 180f, 0f);

            report.Add(name + " — рост " + Measure(go).ToString("0.0") + " м");
        }

        /// <summary>Габарит по мешам: то, что реально видно в кадре.</summary>
        private static float Measure(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0f;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            return bounds.size.y;
        }

        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(12f, 1f, 12f);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;

            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(0.34f, 0.36f, 0.28f));
            material.SetFloat("_Smoothness", 0.05f);
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateLight()
        {
            var go = new GameObject("Sun", typeof(Light));
            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(38f, 145f, 0f);
        }

        private static void CreateCamera(float size)
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";

            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.clearFlags = CameraClearFlags.Skybox;

            go.transform.rotation = Quaternion.Euler(CameraPitch, CameraYaw, 0f);
            go.transform.position = new Vector3(0f, 2f, 0f) - go.transform.forward * 60f;
            cam.farClipPlane = 200f;
        }
    }
}
