using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Глаза и линейка: снимок сцены из редактора и числа её композиции.
    ///
    /// Обе половины решают одну беду. Расстановку я до сих пор делал вслепую:
    /// писал координаты в коде и видел результат только после сборки игры —
    /// четыре минуты на один взгляд. Уровни так не собирают, там за минуту
    /// делают сотню мелких сдвигов.
    ///
    /// <b>Снимок</b> сокращает круг с четырёх минут до двадцати секунд:
    /// камера ставится прямо в редакторе, кадр уходит в PNG, игра не нужна.
    ///
    /// <b>Числа</b> нужны, чтобы копировать чужую композицию, а не выдумывать
    /// свою. У наборов Synty лежат демо-сцены, собранные их художниками. На
    /// них можно смотреть — но смотреть мало: «красиво» не переносится в код.
    /// Переносятся плотность, расстояния между соседями, разброс масштаба и
    /// поворота, соотношение ярусов. Это и меряем.
    /// </summary>
    public static class SceneEye
    {
        private const int Width = 1600;
        private const int Height = 900;

        /// <summary>Куда складываем кадры.</summary>
        private static string Folder => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "shots");

        // ------------------------------------------------------------------

        /// <summary>
        /// Снимок открытой сцены с трёх четвертей, как смотрит игрок.
        ///
        /// Угол не случайный: 30 градусов сверху — примерно то, что видит
        /// наша камера из-за плеча на среднем отдалении. Снимать сверху вниз
        /// бессмысленно — композиция уровня читается с высоты глаз, а не с
        /// птичьего полёта.
        /// </summary>
        public static void Shot(string name, Vector3 at, float distance = 40f,
                                float pitch = 22f, float yaw = 35f)
        {
            var rig = new GameObject("SceneEye") { hideFlags = HideFlags.HideAndDontSave };

            var cameraGo = new GameObject("Camera", typeof(Camera));
            cameraGo.transform.SetParent(rig.transform, false);

            var camera = cameraGo.GetComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2000f;

            // Небо и туман берём из самой сцены: снимок должен показывать то,
            // что увидит игрок, а не студийную выкладку.
            camera.clearFlags = RenderSettings.skybox != null
                ? CameraClearFlags.Skybox
                : CameraClearFlags.SolidColor;

            // Тот же предел видимости мелочи, что и у игровой камеры.
            //
            // Без этой строки съёмочный кадр врёт: он снимает СВОЕЙ камерой,
            // на которой никаких пределов нет, и показывает мир не таким,
            // каким его увидит игрок. Я на этом уже поставил опыт впустую —
            // проверял, ушла ли дальняя трава, кадром, где она заведомо
            // рисуется.
            //
            // Правило: если у игровой камеры есть настройка, влияющая на
            // картинку, она обязана быть и у съёмочной. Иначе глаза
            // показывают не то, что происходит.
            int detail = LayerMask.NameToLayer("Detail");

            if (detail >= 0)
            {
                var limits = camera.layerCullDistances;
                limits[detail] = 45f;
                camera.layerCullDistances = limits;
                camera.layerCullSpherical = true;
            }

            var rotation = Quaternion.Euler(pitch, yaw, 0f);

            camera.transform.position = at - rotation * Vector3.forward * distance;
            camera.transform.rotation = rotation;

            var texture = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;

            camera.targetTexture = texture;
            camera.Render();

            RenderTexture.active = texture;

            var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            shot.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(texture);

            Directory.CreateDirectory(Folder);

            string file = Path.Combine(Folder, name + ".png");
            File.WriteAllBytes(file, shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
            Object.DestroyImmediate(rig);

            Debug.Log("[IsoRPG] Кадр: " + file);
        }

        [MenuItem("Tools/IsoRPG/Глаз: снять открытую сцену", priority = 74)]
        public static void ShotHere() => Shot("scene", Vector3.zero);

        // ------------------------------------------------------------------

        /// <summary>
        /// Разбирает композицию открытой сцены на числа.
        ///
        /// Ярусы делим по высоте объекта, а не по имени: имена у наборов
        /// разные, а высота — это то, чем ярус и является. Крона держит
        /// силуэт, подлесок закрывает переходы, мелочь у земли даёт
        /// подробность под ногами. Соотношение между ними и есть половина
        /// того, что читается как «живая локация».
        ///
        /// Расстояние до соседа меряем ВНУТРИ яруса. Дерево и травинка рядом
        /// — норма; два дерева вплотную — мешанина. Смешав ярусы в одну
        /// статистику, получишь среднюю температуру по больнице.
        /// </summary>
        public static void Audit(string title)
        {
            var report = new StringBuilder();

            report.AppendLine("КОМПОЗИЦИЯ: " + title);
            report.AppendLine();

            var pieces = new List<(string name, Vector3 at, float height, float scale, float yaw)>();
            var bounds = new Bounds();
            bool first = true;

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject);
                if (root == null) root = renderer.gameObject;

                // Один объект считаем один раз: у сложного префаба рендереров
                // много, и без этого дерево из шести кусков посчиталось бы за
                // шесть деревьев, а плотность выросла бы вшестеро.
                if (root != renderer.gameObject &&
                    pieces.Any(p => p.name == root.name && p.at == root.transform.position))
                    continue;

                var box = renderer.bounds;

                if (first) { bounds = box; first = false; }
                else bounds.Encapsulate(box);

                pieces.Add((root.name,
                            root.transform.position,
                            box.size.y,
                            root.transform.lossyScale.y,
                            root.transform.eulerAngles.y));
            }

            if (pieces.Count == 0)
            {
                Debug.LogWarning("[IsoRPG] В сцене нет ни одного объекта для замера.");
                return;
            }

            float area = Mathf.Max(bounds.size.x * bounds.size.z, 1f);

            report.AppendLine("Объектов: " + pieces.Count);
            report.AppendLine("Занятая площадь: " + bounds.size.x.ToString("0") + " x " +
                              bounds.size.z.ToString("0") + " м");
            report.AppendLine("Плотность: " + (pieces.Count * 10000f / area).ToString("0.0") +
                              " объектов на гектар");
            report.AppendLine();

            Tier(report, "КРОНЫ (выше 6 м)", pieces.Where(p => p.height > 6f).ToList());
            Tier(report, "ПОДЛЕСОК (1.5 - 6 м)", pieces.Where(p => p.height > 1.5f && p.height <= 6f).ToList());
            Tier(report, "МЕЛОЧЬ (ниже 1.5 м)", pieces.Where(p => p.height <= 1.5f).ToList());

            Debug.Log("[IsoRPG]\n" + report);
        }

        private static void Tier(StringBuilder report, string title,
                                 List<(string name, Vector3 at, float height, float scale, float yaw)> tier)
        {
            report.AppendLine(title + ": " + tier.Count);

            if (tier.Count == 0) { report.AppendLine(); return; }

            // Расстояние до ближайшего соседа по ярусу.
            var gaps = new List<float>();

            for (int i = 0; i < tier.Count && i < 400; i++)
            {
                float nearest = float.MaxValue;

                for (int j = 0; j < tier.Count; j++)
                {
                    if (i == j) continue;

                    float dx = tier[i].at.x - tier[j].at.x;
                    float dz = tier[i].at.z - tier[j].at.z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);

                    if (d < nearest) nearest = d;
                }

                if (nearest < float.MaxValue) gaps.Add(nearest);
            }

            if (gaps.Count > 0)
            {
                gaps.Sort();

                report.AppendLine("    до соседа: минимум " + gaps[0].ToString("0.0") +
                                  ", середина " + gaps[gaps.Count / 2].ToString("0.0") +
                                  ", максимум " + gaps[gaps.Count - 1].ToString("0.0") + " м");
            }

            var scales = tier.Select(p => p.scale).OrderBy(s => s).ToList();

            report.AppendLine("    масштаб: от " + scales[0].ToString("0.00") +
                              " до " + scales[scales.Count - 1].ToString("0.00"));

            // Сколько разных префабов участвует — это и есть разнообразие.
            var kinds = tier.Select(p => System.Text.RegularExpressions.Regex
                                           .Replace(p.name, @"\s*\(\d+\)$", ""))
                            .Distinct()
                            .ToList();

            report.AppendLine("    разных моделей: " + kinds.Count);
            report.AppendLine("    примеры: " + string.Join(", ", kinds.Take(6)));
            report.AppendLine();
        }
    }
}
