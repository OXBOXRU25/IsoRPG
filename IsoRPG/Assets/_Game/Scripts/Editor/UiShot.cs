using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Снимает демо-сцены интерфейса в кадры.
    ///
    /// Зачем. У чужого набора есть авторский эталон компоновки — демо-сцена,
    /// где Synty сам расставил панели, выбрал размеры и отступы. Оттуда
    /// читается норматив, которого из спрайтов не выведешь. Плюс Павлону
    /// нужно ПОСМОТРЕТЬ, прежде чем решать: примеры в наборе идут без
    /// интерфейса, это просто фоны.
    ///
    /// Как. Экранный холст (Screen Space - Overlay) в отрисовку камеры не
    /// попадает — его рисует сам движок поверх кадра. Поэтому на время
    /// съёмки переводим холсты в режим камеры и рендерим в текстуру нужного
    /// размера: заодно получаем ровно 1920×1080 независимо от того, какое
    /// окно у пакетного редактора.
    ///
    /// Сцену НЕ сохраняем: правка холстов нужна только для кадра.
    /// </summary>
    public static class UiShot
    {
        private const string Folder = "Assets/Synty/InterfaceFantasyWarriorHUD/Samples/Scenes/";

        private static readonly string[] Scenes =
        {
            "08_Demo_FantasyWarrior_HUD_ActionRPG01",
            "06_Demo_FantasyWarrior_HUD_Adventure01",
            "02_Demo_FantasyWarrior_Components_HPBars_Stats",
            "03_Demo_FantasyWarrior_Components_Compass-Minimap-CharacterPortraits",
        };

        /// <summary>Сцены за пределами папки Synty — путь целиком.</summary>
        private static readonly string[] OtherScenes =
        {
            "Assets/Scenes/GUI_Fantasy_Kit.unity",
        };

        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("Tools/IsoRPG/Интерфейс: снять демо-сцены Synty", priority = 41)]
        public static void Run()
        {
            string outDir = "D:/GAME Ai/shots/ui";
            Directory.CreateDirectory(outDir);

            var all = new System.Collections.Generic.List<string>();
            foreach (var name in Scenes) all.Add(Folder + name + ".unity");
            all.AddRange(OtherScenes);

            foreach (var path in all)
            {
                string name = Path.GetFileNameWithoutExtension(path);

                if (!File.Exists(path))
                {
                    Debug.LogWarning("[IsoRPG] Нет сцены " + path);
                    continue;
                }

                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                var camera = Object.FindFirstObjectByType<Camera>();

                if (camera == null)
                {
                    var holder = new GameObject("ShotCam");
                    camera = holder.AddComponent<Camera>();
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.08f, 0.08f, 0.09f);
                }

                var canvases = Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                foreach (var canvas in canvases)
                {
                    if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 5f;
                }

                Canvas.ForceUpdateCanvases();

                // Сцена без холстов — набор разложен спрайтами прямо в мире
                // (так сделан gui_fantasy_kit). Тогда переводить нечего, зато
                // надо навести камеру: иначе она смотрит из нуля мимо всего и
                // отдаёт чёрный кадр — ровно это и вышло с первого раза.
                if (canvases.Length == 0) FitToWorld(camera);

                var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                var previous = camera.targetTexture;

                camera.targetTexture = texture;
                camera.Render();

                RenderTexture.active = texture;

                var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                shot.Apply();

                RenderTexture.active = null;
                camera.targetTexture = previous;

                string file = Path.Combine(outDir, name + ".png");
                File.WriteAllBytes(file, shot.EncodeToPNG());

                Object.DestroyImmediate(shot);
                texture.Release();

                Debug.Log("[IsoRPG] Кадр интерфейса: " + file);
            }
        }

        /// <summary>
        /// Наводит камеру на всё, что лежит в сцене: ортографический вид по
        /// общим границам всех отрисовщиков, с запасом в десятую часть.
        /// </summary>
        private static void FitToWorld(Camera camera)
        {
            var renderers = Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);

            camera.orthographic = true;
            camera.transform.position = new Vector3(bounds.center.x, bounds.center.y,
                                                    bounds.min.z - 20f);
            camera.transform.rotation = Quaternion.identity;

            // Берём большее из двух: по высоте напрямую, по ширине — с учётом
            // пропорций кадра. Иначе широкий набор обрежется по краям.
            float byHeight = bounds.extents.y;
            float byWidth = bounds.extents.x * Height / Width;

            camera.orthographicSize = Mathf.Max(byHeight, byWidth) * 1.1f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.15f, 0.17f);

            Debug.Log($"[IsoRPG] Камера наведена на сцену: {bounds.size.x:0.0}×{bounds.size.y:0.0}, " +
                      $"отрисовщиков {renderers.Length}");
        }
    }
}
