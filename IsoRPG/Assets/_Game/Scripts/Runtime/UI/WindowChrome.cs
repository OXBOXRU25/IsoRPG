using UnityEngine;
using IsoRPG.Localization;
using UnityEngine.UI;

namespace IsoRPG.UI
{
    /// <summary>
    /// Крестик закрытия — один на все окна.
    ///
    /// Esc закрывает всё и так, но полагаться на это нельзя: игрок узнаёт про
    /// Esc, только если попробует, а до тех пор окно без видимого выхода
    /// читается как зависшее. Крестик — единственная кнопка, которую ищут
    /// глазами, не думая.
    /// </summary>
    public static class WindowChrome
    {
        private static readonly Color Idle = new Color32(0x8A, 0x84, 0x76, 0xFF);
        private static readonly Color Hover = new Color32(0xE8, 0x9A, 0x8A, 0xFF);
        private static readonly Color Plate = new Color32(0x2A, 0x27, 0x21, 0x00);
        private static readonly Color PlateHover = new Color32(0x3A, 0x2A, 0x24, 0xFF);

        /// <summary>
        /// 26 на 26 — меньше обычной цели в 48, и намеренно: крестик стоит
        /// в углу окна, где мимо него промахнуться некуда, а полноразмерная
        /// кнопка съела бы заголовок.
        /// </summary>
        private const float Size = 26f;

        /// <summary>Размер каменной кнопки закрытия — у неё своя оправа, ей нужно место.</summary>
        private const float StoneSize = 36f;

        /// <summary>
        /// На сколько рамка выступает за края панели.
        ///
        /// Рамка кладётся ОТДЕЛЬНЫМ слоем позади и крупнее панели, а не на
        /// саму панель. Причина простая: содержимое окон расставлено вручную
        /// по координатам от краёв, и если бы рамка съедала место внутри,
        /// пришлось бы двигать каждую надпись в шести окнах. Так не двигается
        /// ничего — окно просто получает раму снаружи, как картина.
        ///
        /// Двадцать два — видимая стенка рамки (около 14 при нашем множителе)
        /// плюс запас, чтобы содержимое не липло к золоту.
        /// </summary>
        private const float FrameBleed = 22f;

        /// <summary>
        /// Толщина рамки на экране.
        ///
        /// Всё остальное считается от неё, поэтому разъехаться нечему. Двадцать
        /// шесть — столько, чтобы золотая линия и каменная кромка внутри неё
        /// остались различимы: при двадцати они сливаются в одну полосу.
        /// </summary>
        private const float Border = 28f;

        /// <summary>
        /// Во сколько раз угол крупнее толщины рамки.
        ///
        /// Считается из одного числа: какую долю детали занимает сама кромка,
        /// а не внутренность окна за ней. Деталь 451 на 451, и кромка в ней
        /// кончается на 197 пикселе сверху и на 200 слева — то есть занимает
        /// **44%**, а дальше идёт светлая поверхность будущего окна.
        ///
        /// Здесь раньше стояло 77% и множитель 1.3. Число было взято на глаз
        /// и оказалось почти вдвое завышенным: при толщине рамки 28 кромка
        /// угла выходила 15.9 пикселя против 28 у полос. Угол читался
        /// утопленным внутрь — рамка в нём вдвое тоньше, и светлая
        /// внутренность подступала к самому краю. Заметно это именно на
        /// стыке с полосой, где две толщины лежат рядом.
        ///
        /// Замер, а не подбор: множитель обязан быть 1/0.44, иначе кромки
        /// снова разойдутся. При Border 28 угол выходит 63.6 — значит окно
        /// не должно быть уже и ниже 128, иначе углы сойдутся посередине.
        /// Самое маленькое из наших — 750 на 228.
        /// </summary>
        private const float CornerRim = 0.44f;
        private const float CornerScale = 1f / CornerRim;

        /// <summary>
        /// Насколько полоса отступает внутрь от края угла, в долях толщины.
        ///
        /// Ноль. Здесь стояло 0.052, и это была не поправка на рисунок, а
        /// компенсация завышенного CornerScale выше: полосу двигали, чтобы
        /// скрыть разъехавшийся стык. Причину исправили — подпорка не нужна.
        /// Мягкий край у детали есть, но он в две точки исходника из 451, то
        /// есть сотая доля пикселя на экране.
        /// </summary>
        private const float EdgeInset = 0f;

        /// <summary>Цвет поверхности внутри рамки — базовый цвет окон.</summary>
        private static readonly Color Face = new Color32(0x1C, 0x1A, 0x16, 0xF4);

        /// <summary>
        /// Одеть окно в рамку, собранную из деталей.
        ///
        /// Не одной картинкой, а углом и повторяющейся полосой. Причина в том,
        /// что окна у нас от 340 до 750 пикселей, а цельная рамка при
        /// растяжении портит всё, что нарисовано в её середине: ромб на кромке
        /// вытягивался в лепёшку, узор размазывался. У собранной из деталей
        /// такой беды нет по построению — тянется только то, что для этого и
        /// нарисовано.
        ///
        /// Возвращает false, если деталей нет: тогда вызывающий оставляет
        /// прежнюю плоскую заливку с обводкой. Молча получить окно без фона
        /// хуже, чем некрасивое окно.
        /// </summary>
        public static bool ApplyFrame(GameObject panel)
        {
            if (panel == null) return false;

            bool framed = BuildFrame(panel);

            // Перетаскивание — здесь, а не в AddCloseButton, где оно жило
            // раньше.
            //
            // Обещание «обвязка в одном месте, чтобы новое окно не забыли ею
            // снабдить» держалось ровно наполовину: окно без крестика не
            // звало AddCloseButton вовсе и оставалось неподвижным. Так и
            // вышло у диалога с НПС, настроек и добычи — три окна из девяти.
            // Признак ошибки был виден в самом правиле: чтобы перечислить
            // исключения, приходилось вспоминать их поимённо.
            //
            // Рамку строим ДО: ручка спрашивает у панели, досталась ли ей
            // рамка, и берёт поправку на её вылет.
            MakeDraggable((RectTransform)panel.transform);

            return framed;
        }

        /// <summary>Построить саму рамку. Обвязка — в <see cref="ApplyFrame"/>.</summary>
        private static bool BuildFrame(GameObject panel)
        {
            // Рамка одной картинкой — ПЕРЕД рубильником, и это важно.
            //
            // Выбор Павлона 01.09.2026: золотая `Frame_Box05` из набора Synty
            // внешним бордюром на все окна. Она умеет 9-slice — углы не
            // тянутся, тянутся только стороны, — поэтому собирать раму из
            // четырёх краёв и четырёх углов больше незачем.
            //
            // Рубильник `UiFrames.Enabled` выключен с 27.08.2026 и гасит
            // СТАРЫЙ покупной арт: плашку приёмов, панель игрока в дереве с
            // золотом и панель цели в камне с трещинами — три материала,
            // читавшиеся как три разные игры. Новая рамка одна на все окна и
            // ни с чем не спорит, поэтому под рубильник не попадает. Сначала
            // я поставил её ПОСЛЕ проверки — и окна остались голыми, потому
            // что до неё дело не доходило.
            var whole = Resources.Load<Sprite>("UI/Frame_Synty05");
            if (whole != null) return ApplyWholeFrame(panel, whole);

            // Старый путь из нарезанных деталей — только при рубильнике.
            if (!UiFrames.Enabled) return false;

            var cornerSprite = Resources.Load<Sprite>("UI/Win2_Corner");
            var edgeSprite = Resources.Load<Sprite>("UI/Win2_Edge");

            if (cornerSprite == null || edgeSprite == null)
            {
                Debug.LogWarning("[IsoRPG] Нет деталей рамки UI/Win2_Corner " +
                                 "и UI/Win2_Edge — окно осталось плашкой.");
                return false;
            }

            // Своя заливка панели больше не нужна: поверхность даёт рамка.
            // Прозрачной, а не выключенной — Image продолжает ловить клики, и
            // окно по-прежнему можно таскать за любое пустое место.
            var own = panel.GetComponent<Image>();
            if (own != null)
            {
                own.sprite = null;
                own.color = new Color(0f, 0f, 0f, 0f);
            }

            var root = new GameObject("Frame", typeof(RectTransform));
            var frame = (RectTransform)root.transform;
            frame.SetParent((RectTransform)panel.transform, false);

            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(-FrameBleed, -FrameBleed);
            frame.offsetMax = new Vector2(FrameBleed, FrameBleed);

            // Первым в списке — значит рисуется раньше всех детей, то есть
            // под ними. Иначе рама легла бы поверх содержимого.
            frame.SetAsFirstSibling();

            var element = root.AddComponent<LayoutElement>();
            element.ignoreLayout = true;

            // Поверхность внутри рамки. Пока плоская: плитки у нас ещё нет, а
            // окно без фона — прозрачная дыра, сквозь которую видно бой.
            var face = MakePiece(frame, "Face", null);
            face.rectTransform.anchorMin = Vector2.zero;
            face.rectTransform.anchorMax = Vector2.one;
            // Ровно на толщину рамки, не меньше.
            //
            // Здесь стояло 0.8 от толщины — поверхность заканчивалась, не
            // дойдя до кромки, и в зазор был виден тот кусок детали угла,
            // ради которого всё и переделывалось (см. ниже).
            face.rectTransform.offsetMin = new Vector2(Border, Border);
            face.rectTransform.offsetMax = new Vector2(-Border, -Border);
            face.color = Face;

            float corner = Border * CornerScale;
            float inset = Border * EdgeInset;

            // --- Края ---
            //
            // Плитка вдоль своей оси: Tiled повторяет спрайт целиком, не
            // растягивая, поэтому рисунок не поедет на окне любой ширины.
            //
            // Вертикальные стороны — отдельная картинка, повёрнутая заранее, а
            // не поворот объекта. Повёрнутый RectTransform не умеет тянуться
            // якорями: он растянулся бы по ширине окна, будучи при этом его
            // боком.
            var edgeV = Resources.Load<Sprite>("UI/Win2_EdgeV");

            AddEdge(frame, edgeSprite, "EdgeTop", 1, corner, inset);
            AddEdge(frame, edgeSprite, "EdgeBottom", 2, corner, inset);
            AddEdge(frame, edgeV ?? edgeSprite, "EdgeLeft", 3, corner, inset);
            AddEdge(frame, edgeV ?? edgeSprite, "EdgeRight", 4, corner, inset);

            // --- Углы ---
            //
            // Один рисунок в четырёх отражениях. Деталь нарисована левым
            // верхним углом, остальные три получаются зеркалами — потому и
            // важно было заказать её именно в этой ориентации.
            AddCorner(frame, cornerSprite, "CornerTL", 0f, 1f, corner, 1f, 1f);
            AddCorner(frame, cornerSprite, "CornerTR", 1f, 1f, corner, -1f, 1f);
            AddCorner(frame, cornerSprite, "CornerBL", 0f, 0f, corner, 1f, -1f);
            AddCorner(frame, cornerSprite, "CornerBR", 1f, 0f, corner, -1f, -1f);

            // Поверхность — ПОВЕРХ деталей рамки, и это главное здесь.
            //
            // Деталь угла состоит из двух частей: кромка это внешние 44%, а
            // внутренние 56% — светлая поверхность (#876743) того окна, для
            // которого угол рисовали. Вырезать её нельзя, она L-образная. И
            // пока поверхность лежала ПОД деталями, в каждом углу поверх
            // тёмного нутра окна оставалось светлое пятно размером во всю
            // внутреннюю часть угла.
            //
            // Раньше пятно было 20 пикселей и терялось; когда множитель угла
            // исправили с 1.3 на верные 2.27, оно выросло до 36 и полезло в
            // глаза квадратами. То есть предыдущая правка была верной, а
            // видна стала эта беда, которая была всё это время.
            //
            // Теперь поверхность накрывает всё, что внутри кромки: отступ
            // ровно Border, а рисуется последней. Кромке это не мешает —
            // прямоугольник накрывает квадрат внутри угла, а сама кромка
            // остаётся углом буквы Г снаружи от него.
            face.transform.SetAsLastSibling();

            return true;
        }

        /// <summary>
        /// Во сколько раз ужата рамка. Число АВТОРСКОЕ, не наше.
        ///
        /// Щуп `ui-norms` прочитал 249 префабов набора: `Frame_Box05` стоит у
        /// Synty 17 раз, и всегда с множителем 3 при авторских границах 200.
        /// Стенка картинки — 50 пикселей (замер по альфе), значит на экране
        /// она выходит примерно в 17 точек, а угол — в 67.
        ///
        /// Здесь стояло «ужать так, чтобы стенка вышла в 28» — число я взял
        /// от прежней рамки, собранной из деталей. Оно и близко не совпало с
        /// тем, как эту рамку использует её художник.
        /// </summary>
        private const float AuthorSlice = 3f;

        /// <summary>Толщина видимой стенки рамки в пикселях картинки — замер по альфе.</summary>
        private const float WallInSprite = 50f;

        /// <summary>
        /// Рамка одной картинкой с 9-slice.
        ///
        /// Углы у такой картинки не растягиваются — Unity режет её на девять
        /// кусков и тянет только стороны и середину. Середина у рамки пустая,
        /// поэтому окно любого размера получает раму с неискажённым узором.
        /// </summary>
        private static bool ApplyWholeFrame(GameObject panel, Sprite sprite)
        {
            // Своя заливка панели больше не нужна — поверхность даёт рамка.
            // Прозрачной, а не выключенной: Image продолжает ловить клики, и
            // окно по-прежнему таскается за любое пустое место.
            var own = panel.GetComponent<Image>();
            if (own != null)
            {
                own.sprite = null;
                own.color = new Color(0f, 0f, 0f, 0f);
            }

            var root = new GameObject("Frame", typeof(RectTransform));
            var frame = (RectTransform)root.transform;
            frame.SetParent((RectTransform)panel.transform, false);

            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.offsetMin = new Vector2(-FrameBleed, -FrameBleed);
            frame.offsetMax = new Vector2(FrameBleed, FrameBleed);
            frame.SetAsFirstSibling();

            var element = root.AddComponent<LayoutElement>();
            element.ignoreLayout = true;

            // Поверхность окна — под рамой и с отступом ровно в её толщину,
            // чтобы золото легло на край, а не поверх содержимого.
            // Отступ поверхности — ровно под стенку рамки в её авторском
            // масштабе, а не под прежние 28 точек.
            float wall = WallInSprite / AuthorSlice;

            var face = MakePiece(frame, "Face", null);
            face.rectTransform.anchorMin = Vector2.zero;
            face.rectTransform.anchorMax = Vector2.one;
            face.rectTransform.offsetMin = new Vector2(wall, wall);
            face.rectTransform.offsetMax = new Vector2(-wall, -wall);
            face.color = Face;

            var border = MakePiece(frame, "Border", sprite);
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = Vector2.one;
            border.rectTransform.offsetMin = Vector2.zero;
            border.rectTransform.offsetMax = Vector2.zero;
            border.type = Image.Type.Sliced;
            border.fillCenter = false;

            border.pixelsPerUnitMultiplier = AuthorSlice;

            return true;
        }

        /// <summary>Заготовка куска рамки: объект, картинка, без нажатий.</summary>
        private static Image MakePiece(RectTransform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;

            // Клики сквозь раму — они принадлежат панели под ней. Иначе рама
            // перехватывала бы перетаскивание за собственные поля.
            image.raycastTarget = false;

            return image;
        }

        /// <summary>
        /// Один край рамки. Сторона: 1 верх, 2 низ, 3 лево, 4 право.
        ///
        /// Растягивается якорями, а не заданным размером. Так надёжнее: размер
        /// окна в момент сборки ещё не посчитан движком, и всё, что вычислено
        /// из него здесь, вышло бы нулём. Якоря же считаются потом, когда
        /// размеры уже известны.
        /// </summary>
        private static void AddEdge(RectTransform frame, Sprite sprite, string name,
                                    int side, float corner, float inset)
        {
            var image = MakePiece(frame, name, sprite);
            image.type = Image.Type.Tiled;

            bool horizontal = side <= 2;

            // Плитка повторяет спрайт в НАТУРАЛЬНУЮ величину, а полоса
            // нарисована в 346 пикселей толщиной при рамке в 26. Без множителя
            // в кромку попал бы обрезанный кусок середины полосы, и рисунка на
            // ней было бы не узнать. Множитель ужимает плитку ровно до толщины
            // рамки — тогда в кадр входит вся слоёнка целиком.
            float thickness = horizontal ? sprite.rect.height : sprite.rect.width;
            image.pixelsPerUnitMultiplier = Mathf.Max(1f, thickness / Border);

            var rect = image.rectTransform;

            // Точка привязки — в центре у всех четырёх.
            //
            // Отражение масштабом считается от неё: с привязкой у края
            // отражённая полоса уезжает наружу на свою толщину. Ровно на этом
            // до того разъехались углы.
            rect.pivot = new Vector2(0.5f, 0.5f);

            if (horizontal)
            {
                // Тянется по ширине между углами, толщина задана.
                rect.anchorMin = new Vector2(0f, side == 1 ? 1f : 0f);
                rect.anchorMax = new Vector2(1f, side == 1 ? 1f : 0f);
                rect.offsetMin = new Vector2(corner, side == 1 ? -Border : 0f);
                rect.offsetMax = new Vector2(-corner, side == 1 ? 0f : Border);
            }
            else
            {
                rect.anchorMin = new Vector2(side == 3 ? 0f : 1f, 0f);
                rect.anchorMax = new Vector2(side == 3 ? 0f : 1f, 1f);
                rect.offsetMin = new Vector2(side == 3 ? 0f : -Border, corner);
                rect.offsetMax = new Vector2(side == 3 ? Border : 0f, -corner);
            }

            // Отражение противоположных сторон.
            //
            // Полоса нарисована для ВЕРХНЕЙ кромки: золото ближе к внутренней
            // стороне, камень — к внешней. Нижняя и правая без зеркала идут тем
            // же рисунком, и золото у них оказывается не с той стороны — рамка
            // читается перекошенной, хотя геометрия верна.
            rect.localScale = new Vector3(side == 4 ? -1f : 1f,
                                          side == 2 ? -1f : 1f, 1f);

            // Поправка на пустоту у верха угла: без неё полоса встаёт выше
            // кромки на пару пикселей, и стык расходится там, где виднее всего.
            var pos = rect.anchoredPosition;
            if (side == 1) pos.y = -inset;
            else if (side == 2) pos.y = inset;
            else if (side == 3) pos.x = inset;
            else pos.x = -inset;
            rect.anchoredPosition = pos;
        }

        /// <summary>
        /// Один угол. Знаки говорят, в какую сторону его отразить.
        /// </summary>
        private static void AddCorner(RectTransform frame, Sprite sprite, string name,
                                      float ax, float ay, float size,
                                      float flipX, float flipY)
        {
            var image = MakePiece(frame, name, sprite);

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(ax, ay);
            rect.anchorMax = new Vector2(ax, ay);

            // Точка привязки — в ЦЕНТРЕ, а не в углу.
            //
            // Это не мелочь: отражение масштабом считается относительно точки
            // привязки. С привязкой в углу отражённый угол уезжает наружу
            // ровно на свою ширину — и три из четырёх повисают в воздухе, а
            // левый верхний остаётся на месте только потому, что его не
            // отражают вовсе. Ровно это и было видно в игре.
            //
            // С привязкой в центре зеркало не двигает элемент никуда.
            rect.pivot = new Vector2(0.5f, 0.5f);

            // Раз привязка в центре, до угла надо доехать на половину размера.
            rect.anchoredPosition = new Vector2(
                (ax < 0.5f ? 1f : -1f) * size * 0.5f,
                (ay < 0.5f ? 1f : -1f) * size * 0.5f);

            rect.sizeDelta = new Vector2(size, size);

            // Отражение масштабом, а не поворотом: поворот развернул бы рисунок
            // накладки поперёк рамки, а нам нужно зеркало.
            rect.localScale = new Vector3(flipX, flipY, 1f);
        }

        /// <summary>
        /// Окно двигается мышью за полосу заголовка и всплывает над
        /// соседями по нажатию в любое своё место.
        ///
        /// Одним вызовом, потому что порознь их и забывали. Зовётся из
        /// <see cref="ApplyFrame"/> — то есть достаётся каждому окну, у
        /// которого есть рамка, независимо от того, чем оно ещё снабжено.
        /// Окну без рамки (добыча) вызов ставится руками, на месте сборки.
        ///
        /// Идемпотентен: повторный вызов ничего не задваивает.
        /// </summary>
        /// <param name="titleHeight">
        /// Высота полосы, за которую окно берут. Тридцать — заголовок и
        /// ничего больше: сорок, стоявшие тут сначала, залезали на первый ряд
        /// ячеек сумки и отнимали у них часть площади нажатия. Окно с более
        /// низким заголовком передаёт свою высоту, иначе полоса накроет
        /// верхнюю строку списка и промах по ней поедет окном.
        /// </param>
        public static void MakeDraggable(RectTransform panel, float titleHeight = 30f)
        {
            if (panel == null) return;

            // Полоса заголовка обязана накрыть видимый верх окна, а он
            // зависит от того, досталась ли окну нарисованная рамка: та
            // выступает за панель на FrameBleed и уводит золотую кромку
            // наружу от неё. Спрашиваем у самой панели, а не заводим ещё
            // один флаг: ApplyFrame уже оставил на ней ровно один след с
            // этим именем, и он же — единственный признак, который не
            // разойдётся с правдой.
            float overhang = panel.Find("Frame") != null ? FrameBleed : 0f;

            DraggableWindow.Attach(panel, titleHeight, overhang);

            // Нажатие в любую точку окна поднимает его над соседями — как в
            // любой игре с окнами. Вешаем на саму панель: её картинка после
            // ApplyFrame прозрачна, но нажатия ловит, поэтому событие придёт
            // и с пустого места, и сквозь надписи. Кнопкам внутри это не
            // мешает — они лежат выше и получают своё нажатие первыми, а
            // подъём срабатывает по пути вверх по иерархии.
            if (panel.GetComponent<WindowRaiser>() == null)
                panel.gameObject.AddComponent<WindowRaiser>();
        }

        /// <summary>
        /// Крестик закрытия в углу окна.
        ///
        /// Перетаскивание и всплытие сюда больше не входят: они достаются
        /// окну от <see cref="ApplyFrame"/>, потому что окно без крестика
        /// тоже обязано двигаться.
        /// </summary>
        public static void AddCloseButton(RectTransform panel, Font font,
                                          UnityEngine.Events.UnityAction onClose)
        {
            // Окну без рамки крестик достаётся, а ApplyFrame его миновал —
            // значит и обвязку вешаем здесь. Повторный вызов безвреден.
            MakeDraggable(panel);

            var go = new GameObject("Close", typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(panel, false);

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);

            // В самый угол рамки.
            //
            // Отступ в 34 точки был нужен прежней рамке с вымпелом на верхней
            // кромке — крестик ложился прямо на него. У золотой рамки Synty
            // вымпела нет, кромка ровная, и крестик просто висел посреди
            // заголовка. Павлон 01.09.2026: «крестик надо поднимать выше и
            // правее». Ставим на угол: рамка выступает наружу на FrameBleed,
            // туда крестик и садится.
            // Ноль — это угол самой панели; рамка выступает наружу на
            // FrameBleed, так что крестик садится ровно на её угол.
            // Павлон 01.09.2026: с -6/+6 он «зашёл на верхнюю рамку, а до
            // правой не дошёл» — то есть был выше и левее, чем надо.
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Size, Size);

            var plate = go.GetComponent<Image>();
            plate.color = Plate;

            // Каменная кнопка с крестиком вместо буквы «икс».
            //
            // Выбор Павлона 01.09.2026: `close-btn` из набора gui_fantasy_kit.
            // Буква рисовалась встроенным шрифтом и читалась как отладочная
            // заглушка, особенно рядом с золотой рамкой окна.
            var art = Resources.Load<Sprite>("UI/Button_CloseStone");

            if (art != null)
            {
                plate.sprite = art;
                plate.type = Image.Type.Simple;
                plate.color = Color.white;

                // Кнопка крупнее прежней плашки: у картинки своя каменная
                // оправа, и в 26 точках от неё остаётся каша. Тридцать шесть —
                // столько, чтобы крестик внутри оправы читался.
                rect.sizeDelta = new Vector2(StoneSize, StoneSize);
            }

            var button = go.GetComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(onClose);

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = PlateHover;
            colors.pressedColor = PlateHover;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            // С картинкой буква не нужна — крестик уже нарисован на камне.
            // Подсветку тогда даёт сама кнопка через цвета состояний.
            if (art != null)
            {
                colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.86f, 0.78f, 1f);
                colors.pressedColor = new Color(0.78f, 0.72f, 0.66f, 1f);
                button.colors = colors;
                return;
            }

            var textGo = new GameObject("X", typeof(Text));
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.font = font;

            // Латинская «икс», а не символ умножения: встроенный шрифт Unity
            // рисует далеко не всё, и вместо крестика легко получить пустой
            // прямоугольник.
            LocalizedText.Bind(text, "x");
            text.fontSize = 16;
            text.color = Idle;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            // Подсветка цветом текста: закрашивать плашку целиком под мышью —
            // слишком громко для кнопки, которую задевают мимоходом.
            var tint = go.AddComponent<CloseButtonTint>();
            tint.Setup(text, Idle, Hover);
        }
    }

    /// <summary>
    /// Поднимает своё окно наверх, когда по нему нажали.
    ///
    /// Отдельным компонентом, а не внутри DraggableWindow, потому что ручка
    /// перетаскивания — это узкая полоса заголовка, а нажимают по всему окну.
    /// Ловим на самой панели: события в uGUI всплывают вверх по иерархии,
    /// поэтому нажатие по любой ячейке, надписи или пустому месту доходит
    /// сюда — и доходит ПОСЛЕ того, как своё получила кнопка под курсором.
    /// </summary>
    public sealed class WindowRaiser : MonoBehaviour,
        UnityEngine.EventSystems.IPointerDownHandler
    {
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            DraggableWindow.BringToFront(canvas);
        }
    }

    /// <summary>Перекрашивает крестик под курсором.</summary>
    public sealed class CloseButtonTint : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        private Text target;
        private Color idle;
        private Color hover;

        public void Setup(Text text, Color idleColor, Color hoverColor)
        {
            target = text;
            idle = idleColor;
            hover = hoverColor;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (target != null) target.color = hover;
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (target != null) target.color = idle;
        }
    }
}
