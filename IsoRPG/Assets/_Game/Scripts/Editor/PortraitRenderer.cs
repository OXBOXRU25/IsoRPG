using System.IO;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Снимает портреты персонажей с их же моделей.
    ///
    /// Портрет — это лицо того, кого игрок только что видел в бою, и совпасть
    /// он должен буквально. Нарисованный отдельно портрет всегда чуть другой:
    /// иной оттенок капюшона, иная форма черепа, — и это замечают, даже не
    /// понимая, что именно не так.
    ///
    /// Камера смотрит на голову и плечи: в полный рост на плашке 40 пикселей
    /// персонаж превращается в пятно, а лицо читается.
    /// </summary>
    public static class PortraitRenderer
    {
        private const string PortraitsFolder = "Assets/_Game/Art/UI/Icons/Portraits";
        private const string CharactersFolder = "Assets/_Game/Art/KayKit/Characters";
        private const int Size = 256;

        /// <summary>
        /// Кого снимаем. Имя файла совпадает с именем модели — по нему
        /// портрет и находится в игре.
        /// </summary>
        private static readonly string[] Models =
        {
            "Rogue_Hooded", "Rogue", "Mage", "Knight", "Barbarian", "Ranger",
            "Skeleton_Warrior", "Skeleton_Rogue", "Skeleton_Minion", "Skeleton_Mage",
        };

        // Пункт меню убран намеренно. Портреты теперь рисованные, лежат в той
        // же папке под теми же именами, и один случайный вызов затирал бы их
        // рендерами моделей без всякого предупреждения. Метод оставлен: он
        // пригодится, когда персонажей станет больше, чем рисунков.
        public static void RenderAll()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play ассеты не сохраняются на диск.", "Понятно");
                return;
            }

            EnsureFolder(PortraitsFolder);

            int made = 0;

            foreach (var name in Models)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                    CharactersFolder + "/" + name + ".fbx");

                if (model == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет модели " + name + " — портрет пропущен.");
                    continue;
                }

                Render(model, PortraitsFolder + "/" + name + ".png");
                made++;
            }

            AssetDatabase.Refresh();

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitsFolder }))
                SetupSprite(AssetDatabase.GUIDToAssetPath(guid));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Портреты сняты: " + made + ".");
        }

        public static Sprite Load(string modelName) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(PortraitsFolder + "/" + modelName + ".png");

        // ------------------------------------------------------------------

        private static void Render(GameObject model, string file)
        {
            var stage = new GameObject("PortraitStage");

            var instance = Object.Instantiate(model, stage.transform);
            instance.transform.localPosition = Vector3.zero;

            // Разворачиваем в три четверти: анфас читается как паспортное
            // фото, профиль не показывает лица. Три четверти — то, как
            // портреты рисуют веками, и не случайно.
            instance.transform.localRotation = Quaternion.Euler(0f, 205f, 0f);

            SetLayerRecursive(stage, 31);

            var bounds = Measure(instance);

            // Голова и плечи: берём верхнюю треть роста.
            float headY = bounds.max.y - bounds.size.y * 0.16f;
            var focus = new Vector3(bounds.center.x, headY, bounds.center.z);

            var cameraGo = new GameObject("PortraitCamera", typeof(Camera));
            var camera = cameraGo.GetComponent<Camera>();

            camera.orthographic = true;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographicSize = bounds.size.y * 0.19f;

            // Чуть сверху: взгляд слегка снизу делает героя внушительным, а
            // строго сбоку — плоским.
            cameraGo.transform.rotation = Quaternion.Euler(6f, 180f, 0f);
            cameraGo.transform.position = focus - cameraGo.transform.forward * 12f;

            var keyGo = new GameObject("Key", typeof(Light));
            var key = keyGo.GetComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.35f;
            key.color = new Color(1f, 0.96f, 0.88f);
            keyGo.transform.rotation = Quaternion.Euler(18f, 205f, 0f);

            // Контровой свет сзади: он отделяет силуэт от прозрачного фона.
            // Без него тёмный капюшон сливается с пустотой и портрет читается
            // как дыра.
            var rimGo = new GameObject("Rim", typeof(Light));
            var rim = rimGo.GetComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 0.9f;
            rim.color = new Color(0.6f, 0.7f, 1f);
            rimGo.transform.rotation = Quaternion.Euler(-12f, 20f, 0f);

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
            Object.DestroyImmediate(keyGo);
            Object.DestroyImmediate(rimGo);
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

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

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
