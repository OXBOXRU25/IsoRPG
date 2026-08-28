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
        /// <summary>
        /// Куда ведёт «Начать игру». Арена, а не старая песочница.
        ///
        /// Здесь стояло «Sandbox», и это одно слово съело целый круг: я собрал
        /// новую сцену, внёс её в список сборки — а следом отработал этот
        /// сборщик и переписал список ЦЕЛИКОМ, вернув песочницу. Игра
        /// запустилась в старой сцене, и выглядело это как «ничего не
        /// изменилось».
        ///
        /// Урок общий: сборщик, который не добавляет к списку, а ЗАМЕНЯЕТ его,
        /// обязан быть единственным хозяином этого списка — иначе любой, кто
        /// добавил строку до него, молча её теряет.
        /// </summary>
        private const string GameScenePath = "Assets/_Game/Scenes/Arena.unity";
        private const string BackgroundPath = "Assets/_Game/Art/UI/MainMenuBackground.png";
        private const string MusicPath = "Assets/_Game/Audio/Music/MainMenuTheme.mp3";
        private const string VideoPath = "Assets/_Game/Video/MainMenuBackground.mp4";
        private const string ScreenPath = "Assets/_Game/Video/MainMenuScreen.renderTexture";

        private static readonly Color TitleColor = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
        private static readonly Color SubtitleColor = new Color32(0xC8, 0xA8, 0x70, 0xFF);
        private static readonly Color ButtonColor = new Color32(0xC8, 0x9A, 0x3A, 0xFF);
        // Светлая, потому что теперь у надписи есть тёмная обводка: см.
        // MakeButton. Тёмная буква прямо на золоте давала контраст 1.99.
        private static readonly Color ButtonText = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
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
            // Тянемся за шириной, а не за средним между шириной и высотой.
            //
            // При среднем масштаб выходит дробным на любом экране, который не
            // 16:9: на 1920x1200 это 1.054, и шрифт растеризуется между
            // пикселями — надписи выглядят размытыми, особенно мелкие.
            // По ширине на том же экране масштаб ровно 1.0, и текст чёткий.
            scaler.matchWidthOrHeight = 0f;

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
            // Втрое тише исходного: на 0.45 тема забивала всё, и первым делом
            // тянуло убавить звук, а не начать игру.
            //
            // Правка 27.08 до игры не доехала, и это стоит записать: громкость
            // живёт не в коде, а в СЦЕНЕ меню. Пока сцена не пересобрана,
            // менять здесь число бессмысленно — в игре останется прежнее.
            // Отсюда правило: правка сборщика без пересборки его сцены —
            // это не правка, а намерение.
            source.volume = 0.18f;

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


            var go = new GameObject("VideoBackground", typeof(RawImage),
                                    typeof(UnityEngine.Video.VideoPlayer));

            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<RawImage>();
            image.raycastTarget = false;

            // Прозрачная до первого кадра — и в сцене тоже.
            //
            // Картинка без текстуры рисуется белым непрозрачным
            // прямоугольником. В игре её проявляет компонент фона, но в
            // редакторе он не работает, и собранная сцена показывала белый
            // лист вместо заставки — ровно то, что и выглядит как поломка.
            image.color = new Color(1f, 1f, 1f, 0f);

            // Текстуру не назначаем: кадры отдаёт сам проигрыватель, а
            // ставит их компонент фона. Промежуточная текстура была лишним
            // звеном, в котором ролик и застревал после первого прохода.

            // Обрезаем по краям, как и неподвижную картинку: растянутое
            // видео выдаёт себя мгновенно.
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;

            var player = go.GetComponent<UnityEngine.Video.VideoPlayer>();
            player.clip = clip;
            player.renderMode = UnityEngine.Video.VideoRenderMode.APIOnly;
            player.isLooping = true;
            player.playOnAwake = true;
            player.waitForFirstFrame = true;

            // Звук глушим: фоновый ролик заспорил бы с музыкой меню, и
            // получилась бы каша из двух дорожек.
            player.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;

            // Компонент фона: он забирает кадры у проигрывателя и следит,
            // чтобы они шли. Собственное зацикливание проигрывателя тут
            // не помогало — ролик замирал, продолжая считать себя
            // работающим.
            go.AddComponent<IsoRPG.UI.VideoBackground>();

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

            // Тень под заголовком: белый текст на пёстром небе теряется.
            // Здесь именно тень, а не обводка (она есть, компонент Outline, и
            // стоит на кнопках) — заголовок крупный, обводка на нём читается
            // как контур мультяшной надписи, а смещённая тень даёт объём.
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

            /*
              Высота кнопки считается от её собственных рамок, а не на глаз.

              У нарисованной кнопки верхний и нижний торцы не растягиваются —
              это заданные границы 9-slice, по 60 пикселей исходника, что при
              множителе 3.2 даёт 18.75 экранных с каждой стороны, всего 37.5.
              Прежние размеры их не учитывали: у «Выхода» при высоте 44 на
              текст оставалось 6.5 пикселя, и надпись в 16 лезла прямо на
              металл. У «Начать игру» запас был, но всего 2.5 пикселя.

              Считаем так: рамки 37.5 + кегль + воздух по половине кегля с
              каждой стороны. Оба размера заодно перевалили за 48 — минимум
              для пальца, если игра поедет на планшет.

              Главная поднята на 250: при новых высотах кнопки на прежних
              местах налезали друг на друга на четыре пикселя. Зазоры теперь
              равные — по 26 и до кнопок языка тоже 26.
            */
            var start = MakeButton(root, "StartButton", "НАЧАТЬ ИГРУ", ButtonColor, ButtonText, font,
                                   new Vector2(0f, 250f), new Vector2(340f, 90f), 26);

            var quit = MakeButton(root, "QuitButton", "ВЫХОД", QuitColor, QuitText, font,
                                  new Vector2(0f, 142f), new Vector2(250f, 74f), 18);

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

            Localize(start, "НАЧАТЬ ИГРУ");
            Localize(quit, "ВЫХОД");

            BuildLanguagePicker(root, font);
        }

        /// <summary>
        /// Выбор языка на стартовом экране.
        ///
        /// Внизу под кнопками, а не в отдельном окне настроек: человек,
        /// открывший игру не на своём языке, должен найти переключатель
        /// сразу, не разбираясь в незнакомых надписях.
        /// </summary>
        private static void BuildLanguagePicker(RectTransform root, Font font)
        {
            var picker = IsoRPG.Localization.LanguagePicker.Attach(root, font, 330f, 30f);
            var rect = (RectTransform)picker.transform;

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 64f);
        }

        private static void BuildCredits(RectTransform root)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Указание авторов обязательно по лицензии музыки (CC-BY). Место
            // ему на первом экране: в игре без титров это единственная
            // страница, которую увидят все.
            var credits = MakeText(root, "Credits",
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

        /// <summary>
        /// Вешает на подпись перевод.
        ///
        /// Текст кнопки попадает в сцену при сборке и больше не
        /// пересоздаётся, поэтому язык меняет отдельный компонент —
        /// он помнит русский оригинал и подставляет перевод.
        /// </summary>
        private static void Localize(Component target, string russian)
        {
            var text = target.GetComponentInChildren<Text>();
            if (text == null) return;

            var localized = text.gameObject.AddComponent<IsoRPG.Localization.LocalizedText>();
            localized.Setup(russian);
        }

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

            var image = go.GetComponent<Image>();

            // Нарисованная кнопка вместо плоской заливки.
            //
            // Главная — золотая, вторая — тёмная: две золотые рядом спорят
            // за внимание, и «Выход» кричит наравне с «Начать игру».
            //
            // Границы растяжения заданы в пикселях исходника, а он вчетверо
            // крупнее кнопки на экране: без множителя торцы не помещаются и
            // Unity рисует пустоту.
            string art = name == "StartButton" ? "UI/Button_Gold" : "UI/Button_Plain";
            var skin = Resources.Load<Sprite>(art);

            if (skin != null)
            {
                image.sprite = skin;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 3.2f;
                image.color = Color.white;
            }
            else
            {
                image.color = background;
                Debug.LogWarning("[IsoRPG] Нет спрайта " + art + " — кнопка осталась плашкой.");
            }

            var button = go.GetComponent<Button>();

            // Отклик на наведение: кнопка без него читается как картинка.
            //
            // У нарисованной кнопки красим саму картинку: осветление читается
            // как блик на металле, а не как смена цвета плашки.
            var tint = skin != null ? Color.white : background;

            var colors = button.colors;
            colors.normalColor = tint;
            colors.highlightedColor = Color.Lerp(tint, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(tint, Color.black, 0.14f);
            colors.selectedColor = tint;
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

            // Обводка вокруг надписи.
            //
            // Плашка кнопки нарисована пёстрой: по светлоте она идёт от 0.07
            // до 0.44, то есть занимает середину диапазона. На такой подложке
            // не читается ни тёмная буква, ни светлая — замер по картинке дал
            // 1.99 и 2.16 при норме 4.5. Сменой цвета это не лечится.
            //
            // Обводка снимает зависимость от плашки: буква лежит на
            // собственном контуре, и контраст там 18.5 при любом золоте под
            // ней. Тот же приём стоит на кнопке скачивания на сайте — человек
            // должен узнать её, а не гадать, та ли это игра.
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.10f, 0.07f, 0.02f, 0.95f);
            outline.effectDistance = new Vector2(1.6f, -1.6f);

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
