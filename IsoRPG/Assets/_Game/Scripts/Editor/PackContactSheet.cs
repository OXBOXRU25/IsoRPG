using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Контактный лист набора: сетка отрендеренных моделей одним PNG.
    ///
    /// Зачем в дополнение к витрине. Витрина отвечает на вопрос «как это
    /// смотрится в игре, рядом с нашим героем» — и отвечает лучше всего, но
    /// требует запустить игру и обойти площадки ногами. Контактный лист
    /// отвечает на другой вопрос — «что вообще есть в наборе» — и его можно
    /// открыть в чате, переслать, положить рядом со вторым таким же и
    /// сравнить два набора, не запуская ничего.
    ///
    /// Листы кладутся в shots/packs/ — папка не версионируется, они
    /// пересобираются в любой момент.
    /// </summary>
    public static class PackContactSheet
    {
        /// <summary>Сколько моделей на лист: сетка 8 × 6.</summary>
        private const int Columns = 8;
        private const int Rows = 6;

        /// <summary>Сторона одной ячейки в пикселях.</summary>
        private const int Cell = 256;

        /// <summary>
        /// Угол съёмки — тот же, что у иконок предметов.
        ///
        /// Три четверти сверху: видно и лицевую сторону, и верх. Прямо в лоб
        /// стена и сундук выглядят одинаковыми прямоугольниками.
        /// </summary>
        private static readonly Vector3 CameraAngles = new Vector3(28f, 145f, 0f);

        /// <summary>Далеко от сцены: там пусто и ничего не лезет в кадр.</summary>
        private static readonly Vector3 Stage = new Vector3(0f, 5000f, 0f);

        [MenuItem("Tools/IsoRPG/Снять контактные листы наборов", priority = 47)]
        public static void Shoot()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play съёмка не сохранится.", "Понятно");
                return;
            }

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName,
                                         "shots", "packs");
            Directory.CreateDirectory(folder);

            var packs = PackCatalog.Shown.ToArray();

            if (packs.Length == 0)
            {
                Debug.LogError("[IsoRPG] Наборов в проекте не нашлось.");
                return;
            }

            var rig = BuildRig(out Camera camera);
            int done = 0;

            try
            {
                foreach (var pack in packs)
                {
                    EditorUtility.DisplayProgressBar("Контактные листы",
                        pack.Title, (float)done / packs.Length);

                    var models = Pick(pack.Folder, Columns * Rows);

                    if (models.Count == 0)
                    {
                        Debug.LogWarning("[IsoRPG] " + pack.Title + ": нечего снимать.");
                        continue;
                    }

                    var sheet = Compose(models, camera);

                    string file = Path.Combine(folder, Safe(pack.Title) + ".png");
                    File.WriteAllBytes(file, sheet.EncodeToPNG());
                    Object.DestroyImmediate(sheet);

                    // Имена рядом текстом: на картинке подписи в 256 пикселей
                    // читались бы хуже, чем не читались, а знать, что именно
                    // на снимке, нужно — по имени деталь потом и ищется.
                    File.WriteAllLines(Path.Combine(folder, Safe(pack.Title) + ".txt"),
                                       models.Select((p, i) => (i + 1) + ". " + Path.GetFileNameWithoutExtension(p)));

                    Debug.Log("[IsoRPG] " + pack.Title + ": снято " + models.Count +
                              " → " + file);
                    done++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Object.DestroyImmediate(rig);
            }

            Debug.Log("[IsoRPG] Контактные листы готовы, " + done + " шт. Лежат в shots/packs/.");
            EditorUtility.RevealInFinder(folder);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Съёмочная площадка: камера и два источника света.
        ///
        /// Свет свой, а не сценический: сцена стоит под закатным солнцем под
        /// двадцатью градусами, и на контактном листе половина деталей ушла
        /// бы в чёрное. Задача листа — показать форму, а не настроение.
        /// </summary>
        private static GameObject BuildRig(out Camera camera)
        {
            var rig = new GameObject("ContactSheetRig");
            rig.transform.position = Stage;
            rig.hideFlags = HideFlags.HideAndDontSave;

            var cameraGo = new GameObject("Camera", typeof(Camera));
            cameraGo.transform.SetParent(rig.transform, false);

            camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x20, 0x1E, 0x1C, 0xFF);
            camera.cullingMask = ~0;
            camera.enabled = false;

            cameraGo.transform.rotation = Quaternion.Euler(CameraAngles);

            var key = new GameObject("Key", typeof(Light));
            key.transform.SetParent(rig.transform, false);
            key.transform.rotation = Quaternion.Euler(50f, 150f, 0f);

            var keyLight = key.GetComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.1f;
            keyLight.shadows = LightShadows.None;

            var fill = new GameObject("Fill", typeof(Light));
            fill.transform.SetParent(rig.transform, false);
            fill.transform.rotation = Quaternion.Euler(15f, -40f, 0f);

            var fillLight = fill.GetComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.45f;
            fillLight.shadows = LightShadows.None;

            return rig;
        }

        /// <summary>Снимает все модели и складывает в одну картинку.</summary>
        private static Texture2D Compose(List<string> models, Camera camera)
        {
            var sheet = new Texture2D(Cell * Columns, Cell * Rows, TextureFormat.RGBA32, false);

            var empty = new Color[Cell * Cell];
            for (int i = 0; i < empty.Length; i++) empty[i] = new Color32(0x20, 0x1E, 0x1C, 0xFF);

            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                    sheet.SetPixels(x * Cell, y * Cell, Cell, Cell, empty);

            for (int i = 0; i < models.Count && i < Columns * Rows; i++)
            {
                var pixels = ShootOne(models[i], camera);
                if (pixels == null) continue;

                int column = i % Columns;

                // Сверху вниз: в текстуре начало координат внизу, а список
                // читается сверху — без переворота номер в .txt не сходился
                // бы с местом на картинке.
                int row = Rows - 1 - i / Columns;

                sheet.SetPixels(column * Cell, row * Cell, Cell, Cell, pixels);
            }

            sheet.Apply();
            return sheet;
        }

        /// <summary>Одна модель: поставить, вписать в кадр, снять, убрать.</summary>
        private static Color[] ShootOne(string path, Camera camera)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.position = Stage;

            try
            {
                var renderers = go.GetComponentsInChildren<Renderer>()
                                  .Where(r => !(r is ParticleSystemRenderer) && r.enabled)
                                  .ToArray();

                if (renderers.Length == 0) return null;

                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);

                camera.orthographicSize = radius * 1.15f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = radius * 20f + 100f;
                camera.transform.position = bounds.center
                                          - camera.transform.forward * (radius * 6f + 5f);

                var texture = RenderTexture.GetTemporary(Cell, Cell, 24, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;

                camera.targetTexture = texture;
                camera.Render();

                RenderTexture.active = texture;

                var shot = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
                shot.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
                shot.Apply();

                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(texture);

                var pixels = shot.GetPixels();
                Object.DestroyImmediate(shot);

                return pixels;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Что показываем: равномерно по всему набору, как на витрине.
        ///
        /// Отдельная выборка, а не первые сорок восемь: у Fantasy Kingdom
        /// первые полсотни по алфавиту — это ящики и заборы, и лист сказал бы
        /// про набор ровно противоположное правде.
        /// </summary>
        private static List<string> Pick(string folder, int limit)
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Where(p => !p.ToLowerInvariant().Contains("/demo"))
                                     .OrderBy(p => p)
                                     .ToList();

            if (paths.Count == 0)
                paths = AssetDatabase.FindAssets("t:Model", new[] { folder })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .OrderBy(p => p)
                                     .ToList();

            if (paths.Count <= limit) return paths;

            var result = new List<string>(limit);
            double step = (double)paths.Count / limit;

            for (int i = 0; i < limit; i++) result.Add(paths[(int)(i * step)]);

            return result;
        }

        private static string Safe(string name)
        {
            foreach (char bad in Path.GetInvalidFileNameChars())
                name = name.Replace(bad, '_');

            return name.Replace(' ', '_');
        }
    }
}
