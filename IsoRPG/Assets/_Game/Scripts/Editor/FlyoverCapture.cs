using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Съёмка мира для роликов: одиночный кадр на пробу и облёт по дуге.
    /// Кадры пишутся PNG-последовательностью, склейка в видео — снаружи, ffmpeg.
    /// Recorder не используем: он хочет режим Play, а из пакетного режима
    /// надёжнее рендерить камерой напрямую.
    /// </summary>
    public static class FlyoverCapture
    {
        const string Scene  = "Assets/_Game/Scenes/ArenaAuthor.unity";
        const string OutDir = "D:/GAME Ai/Youtube/Frames";
        const int    W = 1920, H = 1080;

        [MenuItem("Tools/IsoRPG/Ютуб: пробный кадр мира")]
        public static void SingleFrame()
        {
            var cam = Prepare(out var centre, out var radius);
            // Пробный кадр: три четверти оборота, чтобы поймать характерный вид.
            PlaceCamera(cam, centre, radius, 215f);
            Directory.CreateDirectory(OutDir);
            Shoot(cam, Path.Combine(OutDir, "proba.png"));
            Debug.Log($"[Ютуб] Пробный кадр снят. Центр мира {centre}, радиус {radius:F0} м");
            Object.DestroyImmediate(cam.gameObject);
        }

        [MenuItem("Tools/IsoRPG/Ютуб: облёт мира")]
        public static void Flyover()
        {
            const int frames = 240;          // 8 секунд при 30 кадрах
            var cam = Prepare(out var centre, out var radius);
            Directory.CreateDirectory(OutDir);
            for (int i = 0; i < frames; i++)
            {
                // Пол-оборота за облёт: полный круг на изометрии читается как карусель.
                float angle = 190f + 180f * i / (frames - 1f);
                PlaceCamera(cam, centre, radius, angle);
                Shoot(cam, Path.Combine(OutDir, $"kadr_{i:D4}.png"));
                if (i % 30 == 0) Debug.Log($"[Ютуб] кадр {i}/{frames}");
            }
            Debug.Log($"[Ютуб] Облёт снят: {frames} кадров в {OutDir}");
            Object.DestroyImmediate(cam.gameObject);
        }

        static Camera Prepare(out Vector3 centre, out float radius)
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);

            // Центр и размах мира считаем по обычным мешам: у скелетных границы
            // заданы «с запасом на любую позу» и уводят счёт на сотни метров.
            var pts = new List<Vector3>();
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (mr.enabled && mr.gameObject.activeInHierarchy) pts.Add(mr.bounds.center);

            // Считаем в локальные: out-параметр внутри лямбды недопустим.
            Vector3 c; float r;
            if (pts.Count == 0) { c = Vector3.zero; r = 100f; }
            else
            {
                // Медиана вместо среднего: одинокий объект на отшибе не должен
                // утаскивать центр кадра на пустое место.
                c = new Vector3(Median(pts.Select(p => p.x)), Median(pts.Select(p => p.y)),
                                Median(pts.Select(p => p.z)));
                var flatCentre = new Vector3(c.x, 0f, c.z);
                var dists = pts.Select(p => Vector3.Distance(new Vector3(p.x, 0f, p.z), flatCentre))
                               .OrderBy(d => d).ToList();
                // Не крайняя точка, а девятый дециль: край мира — это задник и мусор.
                r = dists[Mathf.Clamp((int)(dists.Count * 0.9f), 0, dists.Count - 1)];
            }
            centre = c; radius = r;
            Debug.Log($"[Ютуб] мешей учтено {pts.Count}, центр {centre}, радиус {radius:F0} м");

            var go = new GameObject("__FlyoverCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = Mathf.Max(1500f, radius * 4f);
            cam.clearFlags = CameraClearFlags.Skybox;
            return cam;
        }

        static float Median(IEnumerable<float> xs)
        {
            var a = xs.OrderBy(v => v).ToList();
            return a.Count == 0 ? 0f : a[a.Count / 2];
        }

        static void PlaceCamera(Camera cam, Vector3 centre, float radius, float angleDeg)
        {
            // Наклон 28°: у изометрии в игре угол пологий, и облёт должен
            // читаться как тот же мир, а не как вид с самолёта.
            const float pitch = 28f;
            float dist = radius * 1.15f;
            float rad = angleDeg * Mathf.Deg2Rad;
            var flat = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * dist;
            var pos = centre + flat + Vector3.up * (dist * Mathf.Tan(pitch * Mathf.Deg2Rad));
            cam.transform.position = pos;
            cam.transform.LookAt(centre + Vector3.up * (radius * 0.05f));
        }

        static void Shoot(Camera cam, string path)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
