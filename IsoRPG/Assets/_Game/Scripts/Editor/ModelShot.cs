using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Приёмка модели: состав числами и снимки с трёх сторон.
    ///
    /// Когда приносят модель со стороны — из генератора, от художника, из
    /// чужого набора, — на вопрос «пойдёт ли нам» надо отвечать не на глаз,
    /// а по составу: тип скелета решает, лягут ли на неё наши анимации;
    /// число костей — не окажется ли она вдвое дороже наших; клипы внутри
    /// говорят, что уже сделано; полигоны и материалы — впишется ли в
    /// стиль. Всё это видно за один прогон.
    ///
    /// Снимки нужны отдельно: числа не покажут, похож ли рыцарь на наших
    /// персонажей. Три ракурса — спереди, три четверти и сзади: со спины
    /// герой виден в игре чаще всего.
    ///
    /// Кладёт в shots/models/.
    /// </summary>
    public static class ModelShot
    {
        private const int Size = 512;

        /// <summary>Что снимаем. Путь от корня проекта.</summary>
        private const string Target = "Assets/_Game/Art/Incoming/Knight_Game.fbx";

        [MenuItem("Tools/IsoRPG/Приёмка модели: снять и описать", priority = 62)]
        public static void Shoot()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Target);

            if (asset == null)
            {
                Debug.LogError("[IsoRPG] Не нашёл модель " + Target);
                return;
            }

            var report = new StringBuilder();

            report.AppendLine("МОДЕЛЬ: " + Path.GetFileName(Target));
            report.AppendLine();

            // ---- состав ---------------------------------------------------
            var importer = AssetImporter.GetAtPath(Target) as ModelImporter;

            if (importer != null)
            {
                report.AppendLine("Тип скелета: " + importer.animationType +
                                  "   (Humanoid — наши анимации лягут ретаргетом," +
                                  " Generic — только свои)");
                report.AppendLine("Масштаб импорта: " + importer.globalScale);
                report.AppendLine("Материалы: " + importer.materialImportMode);
                report.AppendLine();
            }

            // Снимаем ВДАЛИ от сцены.
            //
            // Первый заход поставил модель в начало координат — а там наши
            // руины, и на снимках оказалась стена. Съёмочная площадка
            // должна быть пустой, иначе фотографируешь что угодно, кроме
            // того, что принесли.
            var stage = new Vector3(0f, 5000f, 0f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.position = stage;

            var renderers = instance.GetComponentsInChildren<Renderer>();
            var skinned = instance.GetComponentsInChildren<SkinnedMeshRenderer>();

            int triangles = 0;
            var materials = new System.Collections.Generic.HashSet<string>();

            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                    if (material != null) materials.Add(material.name);

                var filter = renderer.GetComponent<MeshFilter>();

                if (filter != null && filter.sharedMesh != null)
                    triangles += filter.sharedMesh.triangles.Length / 3;
            }

            foreach (var skin in skinned)
                if (skin.sharedMesh != null) triangles += skin.sharedMesh.triangles.Length / 3;

            var bounds = new Bounds(instance.transform.position, Vector3.zero);
            bool first = true;

            foreach (var renderer in renderers)
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }

            int bones = skinned.Length > 0 && skinned[0].bones != null ? skinned[0].bones.Length : 0;

            report.AppendLine("Полигонов: " + triangles);
            report.AppendLine("Костей: " + bones + "   (у наших KayKit — 60-70)");
            report.AppendLine("Материалов: " + materials.Count + " — " + string.Join(", ", materials));
            report.AppendLine("Размер: " + bounds.size.x.ToString("0.00") + " × " +
                              bounds.size.y.ToString("0.00") + " × " +
                              bounds.size.z.ToString("0.00") + " м   (наш герой 1.9 м)");
            report.AppendLine();

            // ---- клипы внутри ---------------------------------------------
            var clips = AssetDatabase.LoadAllAssetsAtPath(Target)
                                     .OfType<AnimationClip>()
                                     .Where(c => !c.name.StartsWith("__preview"))
                                     .ToArray();

            report.AppendLine("Анимаций внутри: " + clips.Length);

            foreach (var clip in clips)
                report.AppendLine("    " + clip.name + "   " + clip.length.ToString("0.00") + " с" +
                                  (clip.isLooping ? "   зациклен" : "   НЕ зациклен"));

            // ---- снимки ---------------------------------------------------
            string folder = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "shots", "models");

            Directory.CreateDirectory(folder);

            var rig = new GameObject("ShotRig") { hideFlags = HideFlags.HideAndDontSave };

            var cameraGo = new GameObject("Camera", typeof(Camera));
            cameraGo.transform.SetParent(rig.transform, false);

            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x24, 0x24, 0x28, 0xFF);
            camera.enabled = false;

            var key = new GameObject("Key", typeof(Light));
            key.transform.SetParent(rig.transform, false);
            key.transform.rotation = Quaternion.Euler(45f, 150f, 0f);
            key.GetComponent<Light>().intensity = 1.2f;
            key.GetComponent<Light>().shadows = LightShadows.None;

            float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
            camera.orthographicSize = radius * 1.1f;

            foreach (var (name, angle) in new[] { ("front", 180f), ("three-quarter", 215f), ("back", 0f) })
            {
                var rotation = Quaternion.Euler(12f, angle, 0f);

                camera.transform.position = bounds.center - rotation * Vector3.forward * (radius * 4f);
                camera.transform.rotation = rotation;

                var texture = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;

                camera.targetTexture = texture;
                camera.Render();

                RenderTexture.active = texture;

                var shot = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                shot.Apply();

                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(texture);

                File.WriteAllBytes(Path.Combine(folder, "knight-" + name + ".png"), shot.EncodeToPNG());
                Object.DestroyImmediate(shot);
            }

            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(instance);

            File.WriteAllText(Path.Combine(folder, "knight.txt"), report.ToString());

            Debug.Log("[IsoRPG] Приёмка модели готова: " + folder + "\n" + report);
        }
    }
}
