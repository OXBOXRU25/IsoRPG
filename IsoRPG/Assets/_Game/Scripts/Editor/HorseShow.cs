using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Витрина лошадей: все модели в ряд, один кадр на выбор.
    ///
    /// В ОТДЕЛЬНОЙ сцене, а не в мире. Пять вариантов пламени, поставленные
    /// 31.08.2026 рядом с лагерем ради одного кадра, заказчик увидел раньше
    /// меня и прочитал как новый баг — «дым в четырёх местах на земле». И
    /// прочитал справедливо: в мире, куда он заходит играть, щупов быть не
    /// должно. Сцена здесь создаётся в памяти и не сохраняется вовсе.
    ///
    /// Ставим по НИЖНЕЙ точке модели, а не по её началу координат: у разных
    /// наборов начало то в холке, то между копыт, и ряд иначе выходит
    /// ступеньками.
    /// </summary>
    public static class HorseShow
    {
        private const string PolyArt =
            "Assets/Malbers Animations/Horse AnimSet Pro/7 - Models/Horses/Horse Poly Art.FBX";

        private const string Realistic =
            "Assets/Malbers Animations/Horse AnimSet Pro/7 - Models/Horses/Horse Realistic.FBX";

        private const string Skins =
            "Assets/Malbers Animations/Horse AnimSet Pro/5 - Materials & Textures/";

        /// <summary>
        /// Что показываем: модель, материал масти, подпись.
        ///
        /// Материал назначаем сами, и это не прихоть витрины. Модели набора
        /// импортированы БЕЗ материалов (`materialImportMode: None`), Unity
        /// подставляет им стандартный «Lit» — оттого первая витрина вышла
        /// строем серых лошадей. В игре их красит наш же сборщик, здесь —
        /// этот список.
        ///
        /// Пустой материал означает «взять текстуру Synty» — у той лошади
        /// масть задаётся не материалом, а картинкой из десятка.
        /// </summary>
        private static readonly (string model, string skin, string label)[] Models =
        {
            (PolyArt, Skins + "Horse Poly Art/T_Horse_Brown.psd",        "1 гнедая"),
            (PolyArt, Skins + "Horse Poly Art/T_Horse_Black.psd",        "2 вороная"),
            (PolyArt, Skins + "Horse Poly Art/T_Horse_Gray_Spots.psd",   "3 в яблоках"),
            (PolyArt, Skins + "Horse Poly Art/T_Horse_Gray_Palomino.psd","4 соловая"),
            (Realistic, Skins + "Horse Realistic/Horse4 Albedo Brown.png",     "5 реалистичная"),
            ("Assets/POLYGONHorse/Model/SyntyHorse.FBX",
             "Assets/POLYGONHorse/Textures/Horse_01.png",                "6 Synty POLYGON"),
        };

        /// <summary>
        /// Материал масти. Пустой путь — лошадь Synty: у неё масть задаётся
        /// текстурой, и материал под неё собираем на месте.
        /// </summary>
        private static Material Paint(string skinPath)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");

            // Молча выйти отсюда нельзя. Первая версия возвращала null без
            // единого слова, и весь ряд оставался в родных материалах модели —
            // десять одинаковых лошадей и ни одной ошибки в журнале.
            if (shader == null)
            {
                Debug.LogError("[IsoRPG] Не найден шейдер URP/Lit — красить нечем.");
                return null;
            }

            var skin = AssetDatabase.LoadAssetAtPath<Texture2D>(skinPath);

            if (skin == null)
            {
                Debug.LogError("[IsoRPG] Нет текстуры " + skinPath);
                return null;
            }

            var made = new Material(shader);
            made.SetTexture("_BaseMap", skin);
            made.SetFloat("_Smoothness", 0.15f);
            return made;
        }

        /// <summary>
        /// Десять мастей лошади Synty. Два ряда по пять: одной линией в кадр
        /// не влезают, а отодвигать камеру — значит показать десять
        /// одинаковых пятнышек вместо мастей.
        ///
        /// Ближний ряд — с первой по пятую, дальний — с шестой по десятую,
        /// слева направо в обоих.
        /// </summary>
        public static void SyntySkins()
        {
            string previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Floor();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/POLYGONHorse/Model/SyntyHorse.FBX");

            if (source == null)
            {
                Debug.LogError("[IsoRPG] Нет модели лошади Synty.");
                return;
            }

            for (int i = 0; i < 10; i++)
            {
                string skin = "Assets/POLYGONHorse/Textures/Horse_" + (i + 1).ToString("00") + ".png";

                var go = (GameObject)Object.Instantiate(source);   // обычный клон: инстанс префаба не принимает подмену материала
                go.name = "Масть " + (i + 1);

                int column = i % 5;
                int row = i / 5;

                go.transform.position = new Vector3((column - 2) * 3.0f, 0f, row * 3.6f);
                go.transform.rotation = Quaternion.Euler(0f, 200f, 0f);

                var coat = Paint(skin);
                var renderers = go.GetComponentsInChildren<Renderer>();

                int painted = 0;

                if (coat != null)
                    foreach (var r in renderers)
                        if (!r.name.ToLowerInvariant().Contains("eye"))
                        {
                            r.sharedMaterial = coat;
                            painted++;
                        }

                // Читаем результат, а не намерение: спрашиваем сам рендерер,
                // что у него теперь за текстура.
                var check = renderers.Length > 0 ? renderers[0].sharedMaterial : null;
                var got = check != null && check.HasProperty("_BaseMap")
                    ? check.GetTexture("_BaseMap") : null;

                Debug.Log("[IsoRPG]   покрашено частей " + painted + " из " + renderers.Length +
                          ", на первой текстура «" + (got == null ? "НЕТ" : got.name) + "»");

                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                    go.transform.position += Vector3.up * (go.transform.position.y - bounds.min.y);
                }

                Debug.Log("[IsoRPG] Масть " + (i + 1) + ": " + skin);
            }

            Sun();
            SceneEye.Shot("horse-skins", new Vector3(0f, 0.9f, 1.8f), 14f, 16f, 0f);

            Debug.Log("[IsoRPG] Витрина мастей: 10 штук, кадр снят.");

            if (!string.IsNullOrEmpty(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }

        /// <summary>Пол витрины. Общий, чтобы не держать две копии настройки.</summary>
        private static void Floor()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Пол";
            ground.transform.localScale = Vector3.one * 4f;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;

            var floor = new Material(shader);
            floor.SetColor("_BaseColor", new Color(0.42f, 0.44f, 0.42f));
            floor.SetFloat("_Smoothness", 0.05f);
            ground.GetComponent<Renderer>().sharedMaterial = floor;
        }

        /// <summary>Свет витрины: без него модели выйдут плоскими силуэтами.</summary>
        private static void Sun()
        {
            var sun = new GameObject("Солнце", typeof(Light));
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(48f, 150f, 0f);
        }

        public static void Build()
        {
            // Запомним, где были: после кадра надо вернуться, иначе следующее
            // задание прогона отработает не в той сцене — и будет выглядеть
            // успешным, ничего не изменив.
            string previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,
                                                    NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Пол";
            ground.transform.localScale = Vector3.one * 4f;

            // Материал примитива — встроенный Diffuse, а он в Universal
            // рисуется пурпурным. На первой витрине пол вышел ярко-розовым и
            // спорил с моделями сильнее, чем сами модели друг с другом.
            var floorShader = Shader.Find("Universal Render Pipeline/Lit");

            if (floorShader != null)
            {
                var floor = new Material(floorShader);
                floor.SetColor("_BaseColor", new Color(0.62f, 0.62f, 0.60f));
                floor.SetFloat("_Smoothness", 0.1f);
                ground.GetComponent<Renderer>().sharedMaterial = floor;
            }

            float step = 3.2f;
            float x = -(Models.Length - 1) * step * 0.5f;

            for (int i = 0; i < Models.Length; i++)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Models[i].model);

                if (source == null)
                {
                    Debug.LogError("[IsoRPG] Нет модели " + Models[i].model);
                    x += step;
                    continue;
                }

                var go = (GameObject)Object.Instantiate(source);   // обычный клон: инстанс префаба не принимает подмену материала
                go.name = Models[i].label;
                go.transform.position = new Vector3(x, 0f, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 200f, 0f);

                // Красим: без этого весь ряд выйдет стандартным серым «Lit».
                var coat = Paint(Models[i].skin);
                var renderers = go.GetComponentsInChildren<Renderer>();

                if (coat != null)
                    foreach (var r in renderers)
                    {
                        // Глаза оставляем как есть: масть тела на них не
                        // распространяется, и лошадь со шкурой вместо глаз
                        // выглядит слепой.
                        if (r.name.ToLowerInvariant().Contains("eye")) continue;
                        r.sharedMaterial = coat;
                    }

                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);

                    float lift = go.transform.position.y - bounds.min.y;
                    go.transform.position += Vector3.up * lift;

                    Debug.Log("[IsoRPG] " + Models[i].label +
                              ": рост " + bounds.size.y.ToString("0.00") +
                              " м, длина " + bounds.size.z.ToString("0.00") + " м.");

                    // Чем покрашена. Замер вместо гадания: серую и пурпурную
                    // модель на кадре различить можно, а причину — нет.
                    foreach (var r in renderers)
                    {
                        var mat = r.sharedMaterial;

                        Debug.Log("[IsoRPG]     «" + r.name + "» материал " +
                                  (mat == null ? "ПУСТО"
                                   : "«" + mat.name + "» шейдер «" +
                                     (mat.shader == null ? "ПОТЕРЯН" : mat.shader.name) + "»"));
                    }
                }

                x += step;
            }

            // Свет: без него модели в пустой сцене выйдут плоскими силуэтами.
            var sun = new GameObject("Солнце", typeof(Light));
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            sun.transform.rotation = Quaternion.Euler(48f, 150f, 0f);

            SceneEye.Shot("horses", new Vector3(0f, 0.9f, 0f), 13f, 12f, 0f);

            Debug.Log("[IsoRPG] Витрина лошадей: моделей " + Models.Length + ", кадр снят.");

            // Возвращаемся на прежнюю сцену. Витрину не сохраняем — она
            // существовала только ради кадра.
            if (!string.IsNullOrEmpty(previous))
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }
    }
}
