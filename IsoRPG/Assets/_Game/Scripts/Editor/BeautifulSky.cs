using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Небо из набора Beautiful Sky: 36 готовых панорам.
    ///
    /// Почему именно этот набор снимает старую боль. До сих пор небо мы
    /// брали куполом из наборов Synty — большой сферой с текстурой изнутри.
    /// Купол Enchanted Forest давал чёрный верх, красную полосу и серый
    /// провал между своим краем и горизонтом, и разбираться с этим надо было
    /// отдельно. Здесь неба как объекта в сцене нет вообще: это материал на
    /// штатном шейдере <c>Skybox/Panoramic</c>, который движок рисует за
    /// всей геометрией. Ни купола, ни его края, ни провала — рисовать нечего.
    ///
    /// Набор назван «реалистичным», но реалистичного в нём ничего нет:
    /// двадцать небес с суффиксом Anime и три LowPoly, все — мягкие
    /// нарисованные градиенты с плоскими облаками. Для нас это плюс, а не
    /// минус: гранёные деревья Synty и рыцарь KayKit живут ровно в этом
    /// языке. Фотонебо над ними читалось бы как чужая подложка.
    ///
    /// Выбранное небо запоминается в настройках редактора, поэтому пересборка
    /// любой сцены ставит то же самое, а не «первое из списка».
    /// </summary>
    public static class BeautifulSky
    {
        private const string Folder = "Assets/BeautifulSky/Panoramic/Materials";

        /// <summary>Ключ памяти редактора. Проектный, чтобы не мешать другим.</summary>
        private const string PrefKey = "IsoRPG.BeautifulSky.Chosen";

        /// <summary>
        /// Небо по умолчанию. Дневное с редкими облаками: у нас бой при свете
        /// дня, и небо не должно спорить с интерфейсом за внимание.
        /// </summary>
        private const string Default = "Sky_Anime_01_Day_a";

        /// <summary>Имя выбранного неба; пустая строка — берём по умолчанию.</summary>
        public static string Chosen
        {
            get
            {
                var v = EditorPrefs.GetString(PrefKey, Default);
                return string.IsNullOrEmpty(v) ? Default : v;
            }
            set => EditorPrefs.SetString(PrefKey, value);
        }

        [MenuItem("Tools/IsoRPG/Небо Beautiful Sky: поставить в открытую сцену", priority = 44)]
        public static void ApplyMenu()
        {
            if (Apply())
                Debug.Log("[IsoRPG] Небо поставлено: " + Chosen +
                          ". Выбрать другое: Tools/IsoRPG/Небо Beautiful Sky: выбрать.");
        }

        /// <summary>
        /// Ставит выбранное небо в открытую сцену. Возвращает false, если
        /// набор не найден, — вызывающий сам решает, ругаться ему или молча
        /// оставить прежнее небо.
        /// </summary>
        public static bool Apply()
        {
            var mat = Load(Chosen) ?? Load(Default);

            if (mat == null)
            {
                Debug.LogWarning("[IsoRPG] Набор Beautiful Sky не найден в " + Folder +
                                 " — небо оставляю прежним.");
                return false;
            }

            RenderSettings.skybox = mat;

            // Освещение от неба, а не от заданных вручную трёх цветов: у
            // панорамы есть и верх, и низ, и движок соберёт свет честнее,
            // чем это сделаю я числами. Отражения — оттуда же.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode =
                UnityEngine.Rendering.DefaultReflectionMode.Skybox;

            DynamicGI.UpdateEnvironment();
            return true;
        }

        [MenuItem("Tools/IsoRPG/Небо Beautiful Sky: выбрать", priority = 45)]
        public static void OpenPicker() => SkyPickerWindow.Open();

        /// <summary>Материал неба по короткому имени.</summary>
        public static Material Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");

        /// <summary>
        /// Все небеса набора, отсортированные по имени. Читаем папку, а не
        /// держим список в коде: набор может доехать не целиком, и жёсткий
        /// список тогда врёт про то, чего нет.
        /// </summary>
        public static string[] All()
        {
            if (!Directory.Exists(Folder)) return new string[0];

            return Directory.GetFiles(Folder, "*.mat")
                            .Select(Path.GetFileNameWithoutExtension)
                            .OrderBy(n => n)
                            .ToArray();
        }
    }

    /// <summary>
    /// Окно выбора неба: список кнопок, разложенный по времени суток.
    ///
    /// Превью намеренно не рисуем. Панорамы весят по 8–16 МБ, и окно с
    /// тридцатью шестью картинками будет грузить их все разом при каждом
    /// открытии. Нажать и посмотреть в сцене — быстрее, чем ждать превью.
    /// </summary>
    public class SkyPickerWindow : EditorWindow
    {
        private Vector2 scroll;

        public static void Open()
        {
            var w = GetWindow<SkyPickerWindow>(true, "Небо Beautiful Sky", true);
            w.minSize = new Vector2(360f, 420f);
        }

        private void OnGUI()
        {
            var all = BeautifulSky.All();

            if (all.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Набор Beautiful Sky не найден. Ожидается в Assets/BeautifulSky/Panoramic/Materials.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Сейчас: " + BeautifulSky.Chosen, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Нажатие ставит небо в открытую сцену и запоминает выбор.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            foreach (var group in Group(all))
            {
                EditorGUILayout.LabelField(group.Key, EditorStyles.miniBoldLabel);

                foreach (var name in group.Value)
                {
                    var isCurrent = name == BeautifulSky.Chosen;
                    var label = (isCurrent ? "● " : "   ") + Pretty(name);

                    if (GUILayout.Button(label, EditorStyles.miniButton))
                    {
                        BeautifulSky.Chosen = name;
                        BeautifulSky.Apply();
                        SceneView.RepaintAll();
                        Repaint();
                    }
                }

                EditorGUILayout.Space();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>Раскладка по времени суток — по суффиксу в имени файла.</summary>
        private static List<KeyValuePair<string, List<string>>> Group(string[] all)
        {
            var order = new[]
            {
                ("morning", "Утро"), ("day",   "День"),
                ("dusk",    "Закат"), ("night", "Ночь"),
                ("rain",    "Дождь"),
            };

            var result = new List<KeyValuePair<string, List<string>>>();
            var taken = new HashSet<string>();

            foreach (var (key, title) in order)
            {
                var list = all.Where(n => n.ToLowerInvariant().Contains("_" + key)).ToList();
                foreach (var n in list) taken.Add(n);
                if (list.Count > 0)
                    result.Add(new KeyValuePair<string, List<string>>(title, list));
            }

            var rest = all.Where(n => !taken.Contains(n)).ToList();
            if (rest.Count > 0)
                result.Add(new KeyValuePair<string, List<string>>("Прочее", rest));

            return result;
        }

        /// <summary>«Sky_Anime_01_Day_a» → «Anime 01 Day a».</summary>
        private static string Pretty(string name) =>
            name.StartsWith("Sky_") ? name.Substring(4).Replace('_', ' ') : name;
    }
}
