using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Сравнивает подробные и упрощённые модели растительности: кадром и числами.
    ///
    /// Павлон 02.09.2026 предложил перевести всю растительность на упрощённые
    /// уровни разом — это убрало бы мигание при зуме и сэкономило кадры. Идея
    /// проверяемая, и решать по картинке, а не по словам: снимаем сцену дважды
    /// из одной точки, принудительно держа сперва подробные модели, потом
    /// упрощённые.
    ///
    /// Заодно считаем цену в треугольниках — сколько стоит подробная
    /// растительность и сколько останется от упрощённой.
    ///
    /// Ничего не меняет насовсем: уровень задаётся глобальной настройкой
    /// качества и возвращается в конце.
    /// </summary>
    public static class LodCompare
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";
        private const string OutDir = "D:/GAME Ai/shots/lod";

        private const int Width = 1600;
        private const int Height = 900;

        [MenuItem("Tools/IsoRPG/Щуп: подробные против упрощённых", priority = 35)]
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            Directory.CreateDirectory(OutDir);

            Cost();

            // Камера над лугом, под нашим игровым углом.
            var camGo = new GameObject("LodCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();

            var look = new Vector3(25f, 0f, -55f);
            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain != null)
                look.y = terrain.SampleHeight(look) + terrain.transform.position.y;

            camGo.transform.position = look + Quaternion.Euler(0f, 140f, 0f) * Vector3.back * 18f
                                            + Vector3.up * 13f;
            camGo.transform.LookAt(look + Vector3.up * 1f);

            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.farClipPlane = 1500f;

            int wasMax = QualitySettings.maximumLODLevel;
            float wasBias = QualitySettings.lodBias;

            // Подробные: держим нулевой уровень принудительно.
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.lodBias = 20f;
            Shoot(cam, Path.Combine(OutDir, "detailed.png"));

            // Упрощённые: заставляем брать последний доступный уровень.
            QualitySettings.maximumLODLevel = 2;
            QualitySettings.lodBias = 0.05f;
            Shoot(cam, Path.Combine(OutDir, "simple.png"));

            QualitySettings.maximumLODLevel = wasMax;
            QualitySettings.lodBias = wasBias;

            Object.DestroyImmediate(camGo);

            Debug.Log("[IsoRPG] Кадры сравнения: " + OutDir);
        }

        /// <summary>Считает цену уровней в треугольниках по префабам набора.</summary>
        private static void Cost()
        {
            var text = new StringBuilder("[IsoRPG] Цена растительности:\n");

            long[] total = new long[4];
            int counted = 0;

            foreach (var path in Directory.GetFiles("Assets/PolygonNatureBiomes", "*.prefab",
                                                    SearchOption.AllDirectories))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                if (asset == null) continue;

                var group = asset.GetComponentInChildren<LODGroup>(true);
                if (group == null) continue;

                var lods = group.GetLODs();
                if (lods.Length < 2) continue;

                counted++;

                for (int i = 0; i < lods.Length && i < 4; i++)
                {
                    foreach (var renderer in lods[i].renderers)
                    {
                        var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                        if (filter != null && filter.sharedMesh != null)
                            total[i] += filter.sharedMesh.triangles.Length / 3;
                    }
                }
            }

            text.Append("  растений с уровнями: ").Append(counted).Append('\n');

            for (int i = 0; i < 4; i++)
            {
                if (total[i] == 0) continue;

                text.Append("  уровень ").Append(i).Append(": ")
                    .Append(total[i]).Append(" треугольников суммарно");

                if (i > 0 && total[0] > 0)
                    text.Append(" — ").Append((100 * total[i] / total[0])).Append("% от подробного");

                text.Append('\n');
            }

            Debug.Log(text.ToString());
        }

        private static void Shoot(Camera cam, string file)
        {
            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);

            cam.targetTexture = texture;
            cam.Render();

            RenderTexture.active = texture;

            var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            shot.Apply();

            RenderTexture.active = null;
            cam.targetTexture = null;

            File.WriteAllBytes(file, shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
            texture.Release();
        }
    }
}
