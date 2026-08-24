using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using IsoRPG.UI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает сцену главного меню.
    ///
    /// Тем же способом, что и песочницу: числа и раскладка живут в одном
    /// месте, где их видно, а сцена пересобирается одинаково в любой момент.
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string ScenePath = "Assets/_Game/Scenes/MainMenu.unity";
        private const string GameScenePath = "Assets/_Game/Scenes/Sandbox.unity";
        private const string BackgroundPath = "Assets/_Game/Art/UI/MainMenuBackground.png";
        private const string MusicPath = "Assets/_Game/Audio/Music/MainMenuTheme.mp3";
        private const string VideoPath = "Assets/_Game/Video/MainMenuBackground.mp4";
        private const string ScreenPath = "Assets/_Game/Video/MainMenuScreen.renderTexture";

        private static readonly Color TitleColor = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
        private static readonly Color SubtitleColor = new Color32(0xC8, 0xA8, 0x70, 0xFF);
        private static readonly Color ButtonColor = new Color32(0xC8, 0x9A, 0x3A, 0xFF);
        private static readonly Color ButtonText = new Color32(0x24, 0x1C, 0x10, 0xFF);
        private static readonly Color QuitColor = new Color32(0x3A, 0x34, 0x2C, 0xD0);
        private static readonly Color QuitText = new Color32(0xD0, 0xC8, 0xB4, 0xFF);

        [MenuItem("Tools/IsoRPG/Собрать главное меню", priority = 1)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play сцены не сохраняются на диск.", "Понятно");
                return;
            }

            PrepareBackground();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvasGo = new GameObject("MenuCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)canvasGo.transform;

            var menu = canvasGo.AddComponent<MainMenu>();
            menu.SetGameScene(Path.GetFileNameWithoutExtension(GameScenePath));

            BuildBackground(root);

            // Видео поверх картинки: она остаётся первым кадром, пока
            // проигрыватель раскручивается.
            BuildVideo(root);

            BuildMusic();

            // Название набирается текстом, только если картинки нет:
            // на самом лого оно уже написано, и дубль под ним читался бы
            // как недоделка.
            if (!BuildLogo(root)) BuildTitle(root);

            // Номер версии в углу. Первый вопрос к любому отчёту об ошибке —
            // какая сборка у человека на руках; без подписи на него не
            // ответить ни ему, ни нам.
            IsoRPG.UI.VersionLabel.Attach(
                root, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            BuildButtons(root, menu);
            BuildCredits(root);

            // Камера нужна даже при экранном интерфейсе: без неё Unity ругается
            // и в сборке экран уходит в чёрное.
            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterScenes();

            Debug.Log("[IsoRPG] Главное меню собрано: " + ScenePath +
                      ". Обе сцены добавлены в список сборки, меню первым.");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Готовит картинку фона к использованию в интерфейсе.
        ///
        /// Unity импортирует PNG как обычную текстуру, а интерфейсу нужен
        /// спрайт. Без этого картинка лежит в проекте, выглядит нормально в
        /// папке — и просто не появляется на экране. Ошибка тихая: ни
        /// предупреждения, ни ошибки, только пустой фон.
        /// </summary>
        private static void PrepareBackground()
        {
            var importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;

            if (importer == null)
            {
                Debug.LogWarning("[IsoRPG] Нет картинки " + BackgroundPath);
                return;
            }

            bool dirty = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            // Полноэкранная картинка: сжатие на ней видно сильнее всего,
            // а весит она единицы мегабайт — экономить тут нечего.
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }

            if (importer.maxTextureSize < 2048)
            {
                importer.maxTextureSize = 2048;
                dirty = true;
            }

            if (dirty) importer.SaveAndReimport();
        }

        /// <summary>
        /// Музыка стартового экрана.
        ///
        /// Отдельным объектом в сцене, а не частью холста: звук не имеет
        /// отношения к интерфейсу, и при первой же перестройке кнопок его
        /// иначе снесло бы вместе с ними.
        ///
        /// Играет сразу и по кругу. Громкость заметно ниже единицы: тема
        /// на полной громкости перекрывает всё, а первое, что делает
        /// человек, — тянется убавить звук, вместо того чтобы нажать
        /// «Начать игру».
        /// </summary>
        private static void BuildMusic()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);

            if (clip == null)
            {
                Debug.LogWarning("[IsoRPG] Нет музыки " + MusicPath +
                                 " — меню будет тихим.");
                return;
            }

            // Слушатель звука в той же сцене.
            //
            // Без него не слышно ничего: источник играет, но принимать звук
            // некому. В обычной сцене слушатель приезжает вместе с камерой,
            // а меню я собираю сам — и камеры с ним тут нет.
            //
            // Симптом обманчивый: в редакторе на вкладке источника видно, что
            // клип назначен и воспроизведение идёт, а в колонках тишина.
            var go = new GameObject("Music", typeof(AudioSource), typeof(AudioListener));
            var source = go.GetComponent<AudioSource>();

            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;
            source.volume = 0.45f;

            // Плоский звук: у объёмного громкость зависела бы от того, где
            // стоит камера, а в меню камеры как таковой нет.
            source.spatialBlend = 0f;

            EditorUtility.SetDirty(source);
        }

        /// <summary>
        /// Оживляет фон видео, если оно есть в проекте.
        ///
        /// Видео кладётся поверх неподвижной картинки, а не вместо неё.
        /// Причина в первом кадре: проигрыватель начинает не мгновенно, и
        /// без подложки меню на долю секунды показывает чёрный экран —
        /// та самая мелочь, по которой видно наспех сделанное.
        ///
        /// Кадры идут в отдельную текстуру, а оттуда в картинку на холсте.
        /// Напрямую в интерфейс видео выводить нечем: проигрыватель умеет
        /// рисовать в камеру или в текстуру, а камеры в меню нет.
        /// </summary>
        private static void BuildVideo(RectTransform root)
        {
            var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(VideoPath);

            if (clip == null)
            {
                // Самая частая причина — кодек, а не отсутствие файла.
                //
                // Unity читает H.264, но не HEVC и не 10-битный цвет, а
                // генераторы видео отдают именно такое. Файл при этом лежит
                // на месте, мета создаётся, и всё выглядит нормально — просто
                // клипа внутри нет. Проверяется одной строкой:
                // ffmpeg -i файл, смотреть на «Video:».
                Debug.Log("[IsoRPG] Видео фона не прочиталось: " + VideoPath +
                          ". Проверь кодек — нужен H.264 и 8-битный цвет, " +
                          "HEVC движок не читает. Пока остаётся картинка.");
                return;
            }

            var screen = AssetDatabase.LoadAssetAtPath<RenderTexture>(ScreenPath);

            if (screen == null)
            {
                // Половина разрешения кадра: на фоне за логотипом разницы не
                // видно, а памяти под текстуру уходит вчетверо меньше.
                screen = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
                screen.name = "MainMenuScreen";

                AssetDatabase.CreateAsset(screen, ScreenPath);
                AssetDatabase.SaveAssets();
            }

            var go = new GameObject("VideoBackground", typeof(RawImage),
                                    typeof(UnityEngine.Video.VideoPlayer));

            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<RawImage>();
            image.texture = screen;
            image.raycastTarget = false;

            // Обрезаем по краям, как и неподвижную картинку: растянутое
            // видео выдаёт себя мгновенно.
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            var player = go.GetComponent<UnityEngine.Video.VideoPlayer>();
            player.clip = clip;
            player.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            player.targetTexture = screen;
            player.isLooping = true;
            player.playOnAwake = true;
            player.waitForFirstFrame = true;

            // Звук глушим: фоновый ролик заспорил бы с музыкой меню, и
            // получилась бы каша из двух дорожек.
            player.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;

            EditorUtility.SetDirty(player);

            Debug.Log("[IsoRPG] Фон оживлён видео.");
        }

        private static void BuildBackground(RectTransform root)
        {
            var go = new GameObject("Background", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);

            if (sprite != null)
            {
                image.sprite = sprite;

                // Обрезаем, а не растягиваем: заставка нарисована в 16:9, а
                // экран может быть другим, и растянутая картинка выдаёт себя
                // мгновенно — по вытянутым лицам и кривым аркам.
                image.preserveAspect = false;
                image.type = Image.Type.Simple;

                var fitter = go.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = 16f / 9f;
            }
            else
            {
                image.color = new Color32(0x14, 0x16, 0x1E, 0xFF);

                Debug.LogWarning("[IsoRPG] Нет картинки " + BackgroundPath +
                                 " — фон будет однотонным.");
            }

            // Затемнение снизу: кнопка должна читаться на любой картинке, а
            // светлые плиты внизу заставки съедают тёмный текст.
            //
            // ГРАДИЕНТОМ, а не сплошным прямоугольником. Ровная заливка на
            // 42% высоты давала резкую границу поперёк всего экрана, и она
            // читалась как полоса, прочерченная по картинке. Плавное
            // затухание к середине незаметно вовсе.
            var shade = new GameObject("Shade", typeof(Image));
            var shadeRect = (RectTransform)shade.transform;
            shadeRect.SetParent(root, false);
            shadeRect.anchorMin = new Vector2(0f, 0f);
            shadeRect.anchorMax = new Vector2(1f, 0.55f);
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;

            var shadeImage = shade.GetComponent<Image>();
            shadeImage.sprite = BuildGradientSprite();
            shadeImage.type = Image.Type.Simple;
            shadeImage.color = Color.white;
            shadeImage.raycastTarget = false;
        }

        /// <summary>
        /// Полоса, плавно темнеющая книзу. Собирается кодом и кладётся
        /// ассетом: рисовать её в редакторе изображений ради шестидесяти
        /// пикселей — лишняя зависимость, а градиент в Unity без спрайта
        /// не сделать.
        /// </summary>
        private static Sprite BuildGradientSprite()
        {
            const string path = "Assets/_Game/Art/UI/MenuShade.png";
            const int height = 128;

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                // Снизу плотнее, кверху в ноль. Квадрат кривой убирает
                // видимую границу: линейное затухание глаз всё равно ловит
                // как край.
                float up = y / (float)(height - 1);
                float alpha = (1f - up) * (1f - up) * 0.62f;

                texture.SetPixel(0, y, new Color(0.02f, 0.02f, 0.04f, alpha));
            }

            texture.Apply();

            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Эмблема над названием. Пусто — меню обходится без неё.
        ///
        /// Название набирается шрифтом, а не рисуется на картинке: кириллицу
        /// генераторы изображений выводят с ошибками в буквах, и заметить их
        /// проще всего именно в крупном заголовке. Текстом же его можно
        /// поправить в любой момент, не перерисовывая ничего.
        /// </summary>
        private static bool BuildLogo(RectTransform root)
        {
            IconBinder.PrepareSprites("Assets/_Game/Art/UI");

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/UI/Logo.png");
            if (sprite == null) return false;

            var go = new GameObject("Logo", typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -46f);

            // Пропорции держит preserveAspect, поэтому задаём рамку, а не
            // точный размер картинки: лого можно перерисовать, и меню не
            // придётся править.
            rect.sizeDelta = new Vector2(760f, 530f);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            return true;
        }

        private static void BuildTitle(RectTransform root)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var title = MakeText(root, "Title", "ПРИКЛЮЧЕНИЯ РАЗБОЙНИКА ЖЕНИ", 54, TitleColor, font);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -282f);
            titleRect.sizeDelta = new Vector2(1400f, 72f);
            title.alignment = TextAnchor.MiddleCenter;
            title.fontStyle = FontStyle.Bold;

            // Тень под заголовком: белый текст на пёстром небе теряется, а
            // обводка средствами обычного текста недоступна.
            var shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(3f, -3f);

            var subtitle = MakeText(root, "Subtitle", "птицы высокого полёта", 26, SubtitleColor, font);
            var subRect = (RectTransform)subtitle.transform;
            subRect.anchorMin = new Vector2(0.5f, 1f);
            subRect.anchorMax = new Vector2(0.5f, 1f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0f, -356f);
            subRect.sizeDelta = new Vector2(800f, 30f);
            subtitle.alignment = TextAnchor.MiddleCenter;

            var subShadow = subtitle.gameObject.AddComponent<Shadow>();
            subShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            subShadow.effectDistance = new Vector2(2f, -2f);
        }

        private static void BuildButtons(RectTransform root, MainMenu menu)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var start = MakeButton(root, "StartButton", "НАЧАТЬ ИГРУ", ButtonColor, ButtonText, font,
                                   new Vector2(0f, 210f), new Vector2(320f, 62f), 22);

            var quit = MakeButton(root, "QuitButton", "ВЫХОД", QuitColor, QuitText, font,
                                  new Vector2(0f, 132f), new Vector2(220f, 44f), 16);

            // ПОСТОЯННЫЕ подписки, а не обычный AddListener.
            //
            // Обычный слушатель живёт в памяти и в сцену не записывается.
            // Сборщик отрабатывает в редакторе, сцена сохраняется — и
            // сохраняется с пустыми обработчиками. В редакторе это незаметно,
            // потому что сцена ещё в памяти; в собранной игре обе кнопки
            // оказываются мёртвыми, и выглядит это как «меню не работает».
            //
            // Постоянная подписка пишется в сцену как ссылка на объект и имя
            // метода — ровно то, что делает рука в инспекторе.
            UnityEventTools.AddPersistentListener(start.onClick, menu.StartGame);
            UnityEventTools.AddPersistentListener(quit.onClick, menu.Quit);
        }

        private static void BuildCredits(RectTransform root)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Указание авторов обязательно по лицензии музыки (CC-BY). Место
            // ему на первом экране: в игре без титров это единственная
            // страница, которую увидят все.
            var credits = MakeText(root, "Credits",
                "Модели: KayKit by Kay Lousberg (CC0)   •   " +
                "Звуки: Kenney (CC0)   •   " +
                "Музыка: Kevin MacLeod, incompetech.com (CC BY 4.0)",
                13, new Color(0.72f, 0.70f, 0.66f, 0.85f), font);

            var rect = (RectTransform)credits.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 22f);
            rect.sizeDelta = new Vector2(1400f, 20f);
            credits.alignment = TextAnchor.MiddleCenter;
        }

        // ------------------------------------------------------------------

        private static Button MakeButton(RectTransform root, string name, string label,
                                         Color background, Color textColor, Font font,
                                         Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            go.GetComponent<Image>().color = background;

            var button = go.GetComponent<Button>();

            // Отклик на наведение: кнопка без него читается как картинка.
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.22f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.18f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            var text = MakeText(rect, "Label", label, fontSize, textColor, font);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;

            return button;
        }

        private static Text MakeText(RectTransform parent, string name, string content,
                                     int size, Color color, Font font)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        /// <summary>
        /// Вносит обе сцены в список сборки, меню первым.
        ///
        /// Без этого кнопка «Начать игру» в собранной игре не сделает ничего:
        /// загрузить можно только сцену из списка, и ошибка эта проявляется
        /// лишь в билде, а не в редакторе.
        /// </summary>
        private static void RegisterScenes()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };

            EditorBuildSettings.scenes = scenes;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
