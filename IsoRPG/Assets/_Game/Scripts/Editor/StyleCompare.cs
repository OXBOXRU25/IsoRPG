using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит кандидатов на визуальный стиль в один ряд под нашей игровой
    /// камерой.
    ///
    /// Смысл — не в удобстве, а в честности сравнения. На сайтах авторов
    /// модели сняты крупно и в три четверти, с художественным светом; у нас
    /// вид сверху под 35 градусами, персонаж размером с ноготь и ровный
    /// дневной свет. Выбирать стиль по чужим картинкам значит выбирать не то,
    /// что игрок увидит.
    /// </summary>
    public static class StyleCompare
    {
        private const string ScenePath = "Assets/_Game/Scenes/StyleCompare.unity";

        // Камера ровно как в песочнице: углы сняты замерами с Albion.
        private const float CameraPitch = 35f;
        private const float CameraYaw = 50f;
        private const float GameSize = 9f;    // игровой масштаб
        private const float CloseSize = 2.4f; // крупный план

        private static readonly (string path, string label)[] Candidates =
        {
            ("Assets/_Game/Art/KayKit/Characters/Rogue.fbx",            "KayKit Разбойник"),
            ("Assets/_Game/Art/KayKit/Characters/Rogue_Hooded.fbx",     "KayKit Разбойник в капюшоне"),
            ("Assets/_Game/Art/KayKit/Characters/Skeleton_Warrior.fbx", "KayKit Скелет-воин"),
            ("Assets/_Game/Art/KayKit/Characters/Skeleton_Minion.fbx",  "KayKit Скелет-прислужник"),
            ("Assets/_Game/Art/Characters/Quaternius/Male_Ranger.fbx",  "Quaternius Следопыт"),
            ("Assets/_Game/Art/Characters/Quaternius/Male_Peasant.fbx", "Quaternius Крестьянин"),
            ("Assets/_Game/Art/Characters/Ch24_nonPBR@T-Pose.fbx",      "Наш нынешний (Mixamo)"),
        };

        [MenuItem("Tools/IsoRPG/Сравнить стили моделей", priority = 30)]
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

            var placed = new List<string>();
            float step = 1.6f;
            float startX = -(Candidates.Length - 1) * step * 0.5f;

            for (int i = 0; i < Candidates.Length; i++)
            {
                var (path, label) = Candidates[i];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    Debug.LogWarning("[IsoRPG] Не найдена модель " + path + " — пропускаю.");
                    continue;
                }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = (i + 1) + ". " + label;
                go.transform.position = new Vector3(startX + i * step, 0f, 0f);

                // Разворачиваем лицом к камере: у моделей разных авторов
                // «вперёд» смотрит в разные стороны, и без этого половина
                // ряда окажется спиной.
                go.transform.rotation = Quaternion.Euler(0f, CameraYaw + 180f, 0f);

                placed.Add((i + 1) + ". " + label + "  (высота " +
                           Measure(go).ToString("0.00") + " м)");
            }

            var cam = CreateCamera(GameSize);

            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("[IsoRPG] Сцена сравнения собрана. В ряду слева направо:\n" +
                      string.Join("\n", placed) +
                      "\n\nСнимай кадр: Tools/IsoRPG/Снять кадр. " +
                      "Для крупного плана поставь камере Size " + CloseSize + ".");
        }

        /// <summary>
        /// Высота модели по её же мешам. Число важнее, чем кажется: если
        /// наборы разных авторов расходятся по росту вдвое, смешивать их
        /// нельзя вообще, и это видно раньше, чем начнутся споры о вкусе.
        /// </summary>
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
            ground.transform.localScale = new Vector3(4f, 1f, 4f);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                material.SetColor("_BaseColor", new Color(0.42f, 0.44f, 0.36f));
                material.SetFloat("_Smoothness", 0.05f);
                ground.GetComponent<Renderer>().sharedMaterial = material;
            }
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

        private static Camera CreateCamera(float size)
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";

            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.15f);

            go.transform.rotation = Quaternion.Euler(CameraPitch, CameraYaw, 0f);
            go.transform.position = new Vector3(0f, 1f, 0f) - go.transform.forward * 20f;

            return cam;
        }
    }
}
