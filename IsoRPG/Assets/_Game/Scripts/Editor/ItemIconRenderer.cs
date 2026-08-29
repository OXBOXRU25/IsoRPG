using System.IO;
using UnityEditor;
using UnityEngine;
using IsoRPG.Items;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Снимает иконки предметов с их же 3D-моделей.
    ///
    /// Так делают в настоящих играх, и причина не в экономии. Нарисованная
    /// иконка всегда чуть-чуть не тот предмет: другой наклон, другой оттенок,
    /// другая форма рукояти. Рендер модели совпадает с тем, что игрок видит в
    /// руке, ПО ПОСТРОЕНИЮ — это буквально один и тот же объект.
    ///
    /// Плюс новый предмет получает иконку сам, без художника и без меня.
    /// </summary>
    public static class ItemIconRenderer
    {
        private const string IconsFolder = "Assets/_Game/Art/UI/Icons";
        private const string ItemsFolder = "Assets/_Game/Data/Items";
        private const int Size = 256;

        /// <summary>
        /// Углы съёмки. Три четверти сверху — так предмет читается объёмным,
        /// а не плоским силуэтом, и видно и лезвие, и рукоять.
        /// </summary>
        private static readonly Vector3 CameraAngles = new Vector3(28f, 145f, 0f);

        [MenuItem("Tools/IsoRPG/Снять иконки предметов", priority = 16)]
        public static void RenderAll()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play ассеты не сохраняются на диск.", "Понятно");
                return;
            }

            EnsureFolder(IconsFolder);

            int made = 0, skipped = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);

                if (item == null) continue;

                if (item.worldModel == null)
                {
                    skipped++;
                    continue;
                }

                string file = IconsFolder + "/" + item.name + ".png";
                Render(item.worldModel, file);
                made++;
            }

            AssetDatabase.Refresh();

            // Настраиваем импорт уже после того, как файлы легли на диск:
            // до Refresh их для базы не существует.
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { IconsFolder }))
                SetupSprite(AssetDatabase.GUIDToAssetPath(guid));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Иконки сняты: " + made +
                      (skipped > 0 ? ", пропущено без модели: " + skipped : "") + ".");
        }

        /// <summary>Иконка предмета — ей пользуется интерфейс.</summary>
        public static Sprite Load(string itemName) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(IconsFolder + "/" + itemName + ".png");

        // ------------------------------------------------------------------

        private static void Render(GameObject model, string file)
        {
            // Сцена для съёмки строится и разбирается на каждый предмет.
            // Дороже, чем переиспользовать, но зато соседний предмет не может
            // попасть в кадр, а свет не остаётся от предыдущего.
            var stage = new GameObject("IconStage");

            var instance = Object.Instantiate(model, stage.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // Слой, который видит только наша камера: иначе в кадр попадёт
            // содержимое открытой сцены.
            SetLayerRecursive(stage, 31);

            var bounds = Measure(instance);

            var cameraGo = new GameObject("IconCamera", typeof(Camera));
            var camera = cameraGo.GetComponent<Camera>();

            camera.orthographic = true;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;

            // Прозрачный фон: иконка ложится на любую подложку интерфейса.
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);

            // Рамка вокруг предмета: вплотную обрезанная иконка выглядит
            // так, будто её кадрировали неудачно.
            camera.orthographicSize = bounds.extents.magnitude * 0.78f;

            cameraGo.transform.rotation = Quaternion.Euler(CameraAngles);
            cameraGo.transform.position = bounds.center - cameraGo.transform.forward * 12f;

            var lightGo = new GameObject("IconLight", typeof(Light));
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.97f, 0.9f);
            lightGo.transform.rotation = Quaternion.Euler(35f, 160f, 0f);
            lightGo.gameObject.layer = 31;

            var texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8
            };

            camera.targetTexture = texture;
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = texture;

            var shot = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            shot.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            shot.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;

            File.WriteAllBytes(file, shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
            texture.Release();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(stage);
        }

        private static Bounds Measure(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            return bounds;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        private static void SetupSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.alphaIsTransparency != true)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            // Без сжатия: иконка мелкая, а блочные артефакты по краю
            // прозрачности видно даже на ней.
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace(Path.DirectorySeparatorChar, '/');
            string leaf = Path.GetFileName(folder);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
