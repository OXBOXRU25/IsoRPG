using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using IsoRPG.Cameras;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Рендерит кадр игровой камеры в PNG-файл рядом с проектом.
    ///
    /// Нужен, чтобы результат можно было разглядывать и сравнивать с
    /// референсом, не пересказывая словами. Файлы кладутся ВНЕ папки Assets:
    /// иначе Unity импортирует их как игровые текстуры и засорит проект.
    /// </summary>
    public static class SceneCapture
    {
        private const string OutputFolder = "../shots";
        private const int Width = 1920;
        private const int Height = 1080;

        [MenuItem("Tools/IsoRPG/Снять кадр", priority = 20)]
        public static void CaptureSingle()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[IsoRPG] Не найдена камера с тегом MainCamera.");
                return;
            }

            string path = Capture(cam, "shot");
            Debug.Log("[IsoRPG] Кадр сохранён: " + path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Tools/IsoRPG/Снять серию по углам камеры", priority = 21)]
        public static void CaptureAngleSweep()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[IsoRPG] Не найдена камера с тегом MainCamera.");
                return;
            }

            var rig = cam.GetComponent<IsoCameraRig>();
            if (rig == null)
            {
                Debug.LogError("[IsoRPG] На камере нет IsoCameraRig.");
                return;
            }

            var so = new SerializedObject(rig);
            var pitchProp = so.FindProperty("pitch");
            var yawProp = so.FindProperty("yaw");

            float originalPitch = pitchProp.floatValue;
            float originalYaw = yawProp.floatValue;

            // Углы, между которыми имеет смысл выбирать: 45 — каноническая
            // изометрия, 81 — наше текущее значение, остальные между ними.
            float[] yaws = { 45f, 55f, 65f, 75f, 81f, 90f };

            foreach (float yaw in yaws)
            {
                yawProp.floatValue = yaw;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Риг ставит камеру в LateUpdate, которого в редакторе может
                // не случиться до рендера. Дёргаем его вручную через Update
                // сцены, иначе снимем кадр со старым углом — и не заметим.
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();

                string name = string.Format(CultureInfo.InvariantCulture,
                                            "yaw_{0:00}", yaw);
                Capture(cam, name);
            }

            pitchProp.floatValue = originalPitch;
            yawProp.floatValue = originalYaw;
            so.ApplyModifiedPropertiesWithoutUndo();

            string folder = GetOutputFolder();
            Debug.Log("[IsoRPG] Серия снята в " + folder + ". Угол камеры возвращён на " + originalYaw + ".");
            EditorUtility.RevealInFinder(folder);
        }

        private static string Capture(Camera cam, string fileName)
        {
            string folder = GetOutputFolder();
            Directory.CreateDirectory(folder);

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };

            RenderTexture previousTarget = cam.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            tex.Apply();

            cam.targetTexture = previousTarget;
            RenderTexture.active = previousActive;

            byte[] png = tex.EncodeToPNG();

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);

            string path = Path.Combine(folder, fileName + ".png");
            File.WriteAllBytes(path, png);
            return path;
        }

        private static string GetOutputFolder()
        {
            // Application.dataPath указывает на .../IsoRPG/Assets,
            // поэтому на уровень выше — корень проекта, а ещё выше — наша папка.
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputFolder));
        }
    }
}
