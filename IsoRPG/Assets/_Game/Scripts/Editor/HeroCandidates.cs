using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Лист кандидатов на роль героя: девять моделей в одном кадре.
    ///
    /// Выбирать модель по имени файла нельзя — «кочевник» и «отшельник»
    /// звучат одинаково правдоподобно, а выглядят по-разному. Поэтому
    /// показываем, а не перечисляем: три на три, один взгляд, один ответ.
    ///
    /// Порядок в сетке жёсткий и совпадает с порядком в списке ниже — по
    /// нему и называем выбранного. Подписей в кадре нет намеренно: шрифт
    /// в пакетном режиме рисуется своим шейдером, который в URP может не
    /// завестись, и вместо подписи получится пустое место или розовый
    /// квадрат. Номер надёжнее.
    /// </summary>
    public static class HeroCandidates
    {
        /// <summary>Размер клетки. Сетка считается от числа найденных.</summary>
        private static int Cell = 300;
        private static int Columns = 3;
        private static int Rows = 3;

        /// <summary>
        /// Где живут люди. Наборы фэнтезийные — современный «Generic» и
        /// ужасы сюда не берём: там офисные костюмы и оборотни, в нашей
        /// деревне им делать нечего.
        /// </summary>
        private static readonly string[] Folders =
        {
            "Assets/Synty/PolygonFantasyCharacters/Prefabs",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Characters",
            "Assets/Synty/PolygonFantasyKingdom/Prefabs/Characters",
            "Assets/PolygonElvenRealm/Prefabs/Characters",
            "Assets/PolygonDungeon/Prefabs/Characters",
        };

        /// <summary>
        /// Кого отсеиваем. Не люди и не персонажи: детали одежды, причёски,
        /// нежить, гоблины, привидения. Нежить и гоблины пойдут отдельным
        /// листом — там выбирают по другому признаку.
        /// </summary>
        private static readonly string[] Skip =
        {
            "Attach", "Hair", "Prop", "SM_Prop",
            "Skeleton", "Goblin", "Ghost", "Golem", "Demon",
            "Undead", "Tormented", "Fairy",
        };

        /// <summary>Съёмочная площадка — подальше от сцены, там пусто.</summary>
        private static readonly Vector3 Stage = new Vector3(0f, 5000f, 0f);

        /// <summary>
        /// Если заполнено — снимаем только этих и крупно.
        ///
        /// Нужно на последнем шаге выбора: общий лист отвечает на вопрос
        /// «кто у нас есть», а опознать конкретного в клетке 300 на 300
        /// нельзя. Тогда берём двух-трёх похожих и даём их во весь рост.
        /// </summary>
        public static string[] Only = new string[0];

        /// <summary>Собирает список людей по папкам, а не по памяти.</summary>
        private static List<string> Gather()
        {
            if (Only.Length > 0)
            {
                var picked = new List<string>();

                foreach (var name in Only)
                {
                    string path = Find(name);

                    if (path == null) Debug.LogWarning("[IsoRPG] Не нашёл " + name);
                    else picked.Add(path);
                }

                return picked;
            }

            var found = new List<string>();

            foreach (var folder in Folders)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (var path in Directory.GetFiles(folder, "*.prefab",
                                                        SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileNameWithoutExtension(path);

                    if (Skip.Any(s => name.Contains(s))) continue;

                    found.Add(path.Replace(Path.DirectorySeparatorChar, '/'));
                }
            }

            return found.OrderBy(p => Path.GetFileNameWithoutExtension(p)).ToList();
        }

        [MenuItem("Tools/IsoRPG/Герой: лист кандидатов", priority = 71)]
        public static void Shoot()
        {
            var wanted = Gather();

            if (wanted.Count == 0)
            {
                Debug.LogError("[IsoRPG] Людей не нашлось — проверь пути к наборам.");
                return;
            }

            Columns = Mathf.CeilToInt(Mathf.Sqrt(wanted.Count));
            Rows = Mathf.CeilToInt(wanted.Count / (float)Columns);

            // Мало моделей — значит идёт опознание, и клетку надо крупную.
            Cell = wanted.Count <= 6 ? 640 : 300;

            var rig = new GameObject("HeroRig") { hideFlags = HideFlags.HideAndDontSave };

            var cameraGo = new GameObject("Camera", typeof(Camera));
            cameraGo.transform.SetParent(rig.transform, false);

            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x1E, 0x1E, 0x22, 0xFF);
            camera.enabled = false;

            // Свет ставим щедрый, и это не вкусовщина.
            //
            // Первый лист вышел почти чёрным: в пакетном режиме сцена
            // приходит со своим ночным освещением и с пустым рассеянным
            // светом, а материалы Synty без него проваливаются в темноту.
            // Смотрины во тьме бессмысленны — заказчик увидит не модель, а
            // силуэт и решит, что все одинаковые.
            var ambientWas = RenderSettings.ambientLight;
            var modeWas = RenderSettings.ambientMode;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.68f);

            var key = new GameObject("Key", typeof(Light));
            key.transform.SetParent(rig.transform, false);
            key.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
            key.GetComponent<Light>().intensity = 2.2f;
            key.GetComponent<Light>().shadows = LightShadows.None;

            var fill = new GameObject("Fill", typeof(Light));
            fill.transform.SetParent(rig.transform, false);
            fill.transform.rotation = Quaternion.Euler(20f, -40f, 0f);
            fill.GetComponent<Light>().intensity = 1.1f;
            fill.GetComponent<Light>().shadows = LightShadows.None;

            var sheet = new Texture2D(Cell * Columns, Cell * Rows,
                                      TextureFormat.RGBA32, false);

            var blank = Enumerable.Repeat(new Color32(0x1E, 0x1E, 0x22, 0xFF),
                                          Cell * Cell).ToArray();

            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                    sheet.SetPixels32(x * Cell, y * Cell, Cell, Cell, blank);

            var found = new List<string>();

            for (int i = 0; i < wanted.Count; i++)
            {
                string path = wanted[i];

                found.Add(Path.GetFileNameWithoutExtension(path));

                var pixels = ShootOne(path, camera);
                if (pixels == null) continue;

                int column = i % Columns;
                // Сверху вниз для человека — снизу вверх для текстуры.
                int row = Rows - 1 - i / Columns;

                sheet.SetPixels32(column * Cell, row * Cell, Cell, Cell, pixels);
            }

            sheet.Apply();

            string folder = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "shots");

            Directory.CreateDirectory(folder);

            string file = Path.Combine(folder, "hero-candidates.png");
            File.WriteAllBytes(file, sheet.EncodeToPNG());

            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(rig);

            RenderSettings.ambientLight = ambientWas;
            RenderSettings.ambientMode = modeWas;

            Debug.Log("[IsoRPG] Лист кандидатов: " + file + "\nПорядок слева направо, сверху вниз:\n    " +
                      string.Join("\n    ", found.Select((n, i) => (i + 1) + ". " + n)));
        }

        private static string Find(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (Path.GetFileNameWithoutExtension(path) == name) return path;
            }

            return null;
        }

        private static Color32[] ShootOne(string path, Camera camera)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.position = Stage;
            instance.transform.rotation = Quaternion.Euler(0f, 205f, 0f);

            var renderers = instance.GetComponentsInChildren<Renderer>()
                                    .Where(r => !(r is ParticleSystemRenderer))
                                    .ToArray();

            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(instance);
                return null;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            // Кадрируем по РОСТУ, а не по общему габариту: у фигуры с
            // раскинутыми руками габарит шире, и она уехала бы мельче
            // остальных. Сравнивать надо в одном масштабе, иначе разница в
            // размере прочитается как разница в качестве.
            camera.orthographicSize = Mathf.Max(bounds.size.y, 1f) * 0.62f;

            var rotation = Quaternion.Euler(10f, 0f, 0f);
            camera.transform.position = bounds.center - rotation * Vector3.forward * 20f;
            camera.transform.rotation = rotation;

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

            var pixels = shot.GetPixels32();

            Object.DestroyImmediate(shot);
            Object.DestroyImmediate(instance);

            return pixels;
        }
    }
}
