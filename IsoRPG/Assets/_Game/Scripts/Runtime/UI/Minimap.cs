using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IsoRPG.Quests;
using IsoRPG.Items;

namespace IsoRPG.UI
{
    /// <summary>
    /// Миникарта в правом верхнем углу.
    ///
    /// Два слоя, и это важно. Нижний — вид сверху, который снимает отдельная
    /// камера: земля, дороги, стены. Верхний — маркеры, которые рисует
    /// интерфейс поверх снятого.
    ///
    /// Почему не отдать всё одной камере: сверху человек и скелет — это две
    /// макушки в несколько пикселей, различить их нельзя. Маркер же говорит
    /// ровно то, что нужно знать, и читается при любом размере.
    ///
    /// Показываем героя и задания. Врагов сознательно нет: карта существует,
    /// чтобы понимать, куда идти, а не следить за боем — для боя есть сам
    /// экран, где противник виден целиком.
    ///
    /// Карта не вращается. У нас изометрия с неподвижной камерой, поэтому верх
    /// карты — всегда одна и та же сторона мира, и местность запоминается, а
    /// не переучивается на каждом повороте.
    /// </summary>
    public sealed class Minimap : MonoBehaviour
    {
        private const float Size = 190f;
        private const float Margin = 14f;

        /// <summary>
        /// Сколько метров мира видно от центра до края. Двадцать восемь —
        /// примерно два экрана: видно, куда идти, но карта не превращается в
        /// россыпь значков.
        /// </summary>
        private const float Radius = 28f;

        private const float CameraHeight = 60f;
        private const float ArrowSize = 15f;
        private const float MarkSize = 20f;

        /// <summary>Как часто пересматривать, кто есть на карте.</summary>
        private const float RescanInterval = 1f;

        private static readonly Color ArrowColor = new Color32(0xF2, 0xD9, 0x7A, 0xFF);
        private static readonly Color QuestColor = new Color32(0xFF, 0xD1, 0x2A, 0xFF);
        private static readonly Color ShopColor = new Color32(0x9A, 0xD8, 0xF0, 0xFF);
        private static readonly Color FrameTint = new Color32(0x1C, 0x1A, 0x16, 0xFF);
        private static readonly Color EdgeTint = new Color32(0x6E, 0x5A, 0x32, 0xFF);

        private Camera mapCamera;
        private RenderTexture texture;

        private RectTransform dotsRoot;
        private RectTransform arrow;
        private Text coordinates;

        /// <summary>Цвет строки координат — тусклее меток, это служебное.</summary>
        private static readonly Color CoordinateColor = new Color32(0xC8, 0xC0, 0xAC, 0xFF);
        private readonly List<Text> marks = new List<Text>();

        private readonly List<QuestGiver> givers = new List<QuestGiver>();
        private readonly List<Merchant> shops = new List<Merchant>();
        private float nextScan;

        private GameObject canvasGo;

        private void Awake()
        {
            BuildCamera();
            BuildHud();
        }

        private void OnDestroy()
        {
            if (mapCamera != null) Destroy(mapCamera.gameObject);
            if (canvasGo != null) Destroy(canvasGo);

            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }

        // --- Камера вида сверху ---------------------------------------------

        private void BuildCamera()
        {
            texture = new RenderTexture(256, 256, 16)
            {
                name = "MinimapTexture",
                filterMode = FilterMode.Bilinear,
            };

            var go = new GameObject("MinimapCamera");
            mapCamera = go.AddComponent<Camera>();

            mapCamera.orthographic = true;
            mapCamera.orthographicSize = Radius;

            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            mapCamera.targetTexture = texture;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color32(0x14, 0x12, 0x0E, 0xFF);

            // Что снимаем: только неподвижный мир.
            //
            // UI (5) — иначе в карту втянулись бы полоски здоровья над
            // головами и вылетающие цифры урона.
            // Characters (8) — существа рисуются маркерами; сверху они всё
            // равно две макушки в три пикселя.
            // Preview (9) — сцена с моделью героя для окна снаряжения, она
            // стоит далеко под миром и в карту попадать не должна вовсе.
            const int layerUI = 5, layerCharacters = 8, layerPreview = 9;
            mapCamera.cullingMask = ~((1 << layerUI) |
                                      (1 << layerCharacters) |
                                      (1 << layerPreview));

            mapCamera.depth = -10;
            mapCamera.allowHDR = false;
            mapCamera.allowMSAA = false;

            // Постобработка карте вредна: свечение и глубина резкости
            // размывают и без того мелкую картинку.
            var extra = go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (extra == null)
            {
                extra = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }
            extra.renderPostProcessing = false;
            extra.renderShadows = false;
            extra.requiresColorOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
            extra.requiresDepthOption = UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
        }

        // --- Интерфейс -------------------------------------------------------

        private void BuildHud()
        {
            canvasGo = new GameObject("MinimapCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0f;

            var root = (RectTransform)canvasGo.transform;

            // --- Рамка ---
            var frameGo = new GameObject("MinimapFrame", typeof(Image));
            var frame = (RectTransform)frameGo.transform;
            frame.SetParent(root, false);

            frame.anchorMin = new Vector2(1f, 1f);
            frame.anchorMax = new Vector2(1f, 1f);
            frame.pivot = new Vector2(1f, 1f);
            frame.anchoredPosition = new Vector2(-Margin, -Margin);
            frame.sizeDelta = new Vector2(Size, Size);

            var frameImage = frameGo.GetComponent<Image>();
            frameImage.color = EdgeTint;
            frameImage.raycastTarget = false;

            var innerGo = new GameObject("Inner", typeof(Image));
            var inner = (RectTransform)innerGo.transform;
            inner.SetParent(frame, false);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(2f, 2f);
            inner.offsetMax = new Vector2(-2f, -2f);
            innerGo.GetComponent<Image>().color = FrameTint;
            innerGo.GetComponent<Image>().raycastTarget = false;

            // --- Вид ---
            var viewGo = new GameObject("MinimapView", typeof(RawImage));
            var view = (RectTransform)viewGo.transform;
            view.SetParent(inner, false);
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.offsetMin = new Vector2(2f, 2f);
            view.offsetMax = new Vector2(-2f, -2f);

            var raw = viewGo.GetComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;

            // --- Слой маркеров ---
            //
            // С обрезкой по краю: значок за пределами радиуса не должен
            // вылезать за карту.
            var dotsGo = new GameObject("MinimapMarks", typeof(RectMask2D));
            dotsRoot = (RectTransform)dotsGo.transform;
            dotsRoot.SetParent(view, false);
            dotsRoot.anchorMin = Vector2.zero;
            dotsRoot.anchorMax = Vector2.one;
            dotsRoot.offsetMin = Vector2.zero;
            dotsRoot.offsetMax = Vector2.zero;

            // --- Стрелка героя ---
            //
            // Всегда в центре карты, поворачивается по тому, куда он смотрит.
            // Это единственный маркер, который говорит не только «где», но и
            // «куда» — по нему сверяются с направлением, не отрывая глаз.
            var arrowGo = new GameObject("Player", typeof(Image));
            arrow = (RectTransform)arrowGo.transform;
            arrow.SetParent(dotsRoot, false);
            arrow.anchorMin = new Vector2(0.5f, 0.5f);
            arrow.anchorMax = new Vector2(0.5f, 0.5f);
            arrow.pivot = new Vector2(0.5f, 0.5f);
            arrow.anchoredPosition = Vector2.zero;
            arrow.sizeDelta = new Vector2(ArrowSize, ArrowSize);

            var arrowImage = arrowGo.GetComponent<Image>();
            arrowImage.sprite = ArrowSprite();
            arrowImage.color = ArrowColor;
            arrowImage.raycastTarget = false;

            BuildCoordinates(frame);
        }

        /// <summary>
        /// Строка координат под миникартой.
        ///
        /// Служебная вещь, а не игровая: пока мы расставляем по карте новые
        /// места, единственный способ сказать «это вон там» — назвать числа,
        /// а игрок видит лес и руины, а не сетку. Со стрелкой на карте она
        /// работает в паре: карта говорит «где примерно», строка — «где
        /// точно», и по ней можно дойти до записанной точки.
        ///
        /// Под рамкой, а не внутри: внутри она закрыла бы кусок карты, ради
        /// которого карта и нужна.
        /// </summary>
        private void BuildCoordinates(RectTransform frame)
        {
            var go = new GameObject("Coordinates", typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(frame, false);

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -4f);
            rect.sizeDelta = new Vector2(0f, 18f);

            coordinates = go.GetComponent<Text>();
            coordinates.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            coordinates.fontSize = 12;
            coordinates.alignment = TextAnchor.UpperCenter;
            coordinates.color = CoordinateColor;
            coordinates.raycastTarget = false;

            // Тень под текстом: строка висит поверх мира, а не на плашке, и
            // над светлым песком белые цифры иначе пропадают.
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        // --- Каждый кадр -----------------------------------------------------

        private void LateUpdate()
        {
            if (mapCamera == null) return;

            var p = transform.position;
            mapCamera.transform.position = new Vector3(p.x, p.y + CameraHeight, p.z);

            // Целые числа, без долей: доли меняются каждый кадр и строка
            // начинает мельтешить, а нужна она для того, чтобы сверить своё
            // место с записанным, — там точности до метра хватает с запасом.
            if (coordinates != null)
                coordinates.text = "X " + Mathf.RoundToInt(p.x) +
                                   "   Z " + Mathf.RoundToInt(p.z);

            // Куда смотрит герой. Ноль градусов на карте — вверх, то есть
            // вдоль оси Z мира; отсюда и знак.
            if (arrow != null)
            {
                var f = transform.forward;
                float angle = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                arrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + RescanInterval;
                Rescan();
            }

            DrawMarks(p);
        }

        /// <summary>
        /// Пересобрать список того, что показываем.
        ///
        /// Раз в секунду, а не каждый кадр: обход сцены дорогой, а торговцы и
        /// квестодатели не бегают. Секунда — быстрее, чем игрок успеет дойти
        /// до нового.
        /// </summary>
        private void Rescan()
        {
            givers.Clear();
            givers.AddRange(FindObjectsByType<QuestGiver>(FindObjectsSortMode.None));

            shops.Clear();
            shops.AddRange(FindObjectsByType<Merchant>(FindObjectsSortMode.None));
        }

        private void DrawMarks(Vector3 centre)
        {
            int used = 0;

            foreach (var giver in givers)
            {
                if (giver == null) continue;

                // Восклицательный знак — есть что взять, вопросительный —
                // пора возвращаться. Ровно как в играх, из которых игрок это
                // и так знает.
                string sign;
                switch (giver.State)
                {
                    case QuestState.Available:     sign = "!"; break;
                    case QuestState.ReadyToTurnIn: sign = "?"; break;
                    default: continue;             // взято и ещё не сделано — молчим
                }

                if (Place(used, giver.transform.position - centre, sign, QuestColor)) used++;
            }

            foreach (var shop in shops)
            {
                if (shop == null) continue;
                if (Place(used, shop.transform.position - centre, "$", ShopColor)) used++;
            }

            for (int i = used; i < marks.Count; i++) marks[i].enabled = false;
        }

        /// <summary>
        /// Поставить значок. Смещение — в метрах мира от героя.
        /// Возвращает false, если существо вне радиуса карты.
        /// </summary>
        private bool Place(int index, Vector3 delta, string sign, Color colour)
        {
            // Расстояние по земле: высота на карте ничего не значит, и стоящий
            // на холме иначе выпал бы из радиуса.
            var flat = new Vector2(delta.x, delta.z);
            if (flat.sqrMagnitude > Radius * Radius) return false;

            while (marks.Count <= index)
            {
                var go = new GameObject("Mark", typeof(Text), typeof(Outline));
                var rect = (RectTransform)go.transform;
                rect.SetParent(dotsRoot, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(MarkSize, MarkSize);

                var text = go.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 17;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;

                // Обводка обязательна: карта пёстрая, и жёлтый знак на песке
                // без контура не читается.
                var outline = go.GetComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(1.4f, -1.4f);

                marks.Add(text);
            }

            var mark = marks[index];
            mark.enabled = true;
            mark.text = sign;
            mark.color = colour;

            float scale = (Size * 0.5f - 4f) / Radius;
            ((RectTransform)mark.transform).anchoredPosition =
                new Vector2(flat.x * scale, flat.y * scale);

            return true;
        }

        // --- Стрелка ----------------------------------------------------------

        private static Sprite arrowSprite;

        /// <summary>
        /// Треугольник, смотрящий вверх. Рисуется кодом: это одна фигура из
        /// трёх точек, и заводить ради неё файл в проекте — лишний повод
        /// однажды забыть его при сборке.
        /// </summary>
        private static Sprite ArrowSprite()
        {
            if (arrowSprite != null) return arrowSprite;

            const int side = 48;
            var tex = new Texture2D(side, side, TextureFormat.RGBA32, false);

            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    // Координаты от −1 до 1, начало в центре.
                    float u = (x + 0.5f) / side * 2f - 1f;
                    float v = (y + 0.5f) / side * 2f - 1f;

                    // Треугольник с вырезом снизу — так он читается стрелкой,
                    // а не просто клином.
                    bool inside = v <= 1f - Mathf.Abs(u) * 1.6f &&
                                  v >= -0.75f + Mathf.Abs(u) * 0.35f;

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside ? 1f : 0f));
                }
            }

            tex.Apply();
            arrowSprite = Sprite.Create(tex, new Rect(0, 0, side, side),
                                        new Vector2(0.5f, 0.5f));
            return arrowSprite;
        }
    }
}
