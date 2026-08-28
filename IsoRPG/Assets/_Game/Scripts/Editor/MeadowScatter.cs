using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Наполнение луга: три яруса, рассыпанные пятнами.
    ///
    /// Метод взят не из головы, а с замера демо-сцены Synty. Он показал две
    /// вещи, которые я бы не угадал.
    ///
    /// <b>Первое: ярусов три, и нижние важнее верхнего.</b> У них 765 крон,
    /// 5228 объектов подлеска и 6183 мелочи у земли. У нас в лесу стояло 324
    /// дерева и ровно ничего больше — отсюда и «неживо». Дело было не в том,
    /// что деревья расставлены плохо; дело в том, что под ними пусто.
    ///
    /// <b>Второе: разнообразие.</b> По 36–86 разных моделей на ярус. Я брал
    /// четыре вида деревьев, и лес читался как обои.
    ///
    /// <b>Пятнами, а не сеткой.</b> Равномерный шаг с разбросом даёт грядку:
    /// глаз безошибочно видит регулярность даже под джиттером. Поэтому
    /// плотность берём из шума Перлина — получаются рощи, прогалины и
    /// открытые поляны, то есть композиция, а не заливка. У каждого яруса
    /// своя частота шума и своё смещение, иначе трава ляжет ровно под
    /// деревьями и всё соберётся в одинаковые кучи.
    /// </summary>
    public static class MeadowScatter
    {
        private const string Meadow =
            "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs";

        private const string HolderName = "Meadow";

        /// <summary>Сторона засеваемой площадки, метров.</summary>
        private const float Size = 160f;

        // ---- палитры леса TriForge ---------------------------------------
        //
        // Отдельно от палитр Synty, а не вместо них: наборы живут в разных
        // папках и разном языке, и смешивать их в одном ярусе значит получить
        // фотографический куст рядом с гранёным. Лес у нас теперь TriForge,
        // значит и земля под ним — оттуда же.

        /// <summary>Кусты: самый крупный ярус подлеска.</summary>
        private static readonly string[] FfeBushes =
        {
            "P_FFE_Bush_1", "P_FFE_Bush_2", "P_FFE_Largebush", "P_FFE_Smallbush_2",
        };

        /// <summary>
        /// Трава. Повторяется намеренно и берётся мешком: короткая должна
        /// выпадать чаще высокой, иначе поле выглядит заросшим пустырём, а не
        /// лесной подстилкой.
        /// </summary>
        private static readonly string[] FfeGrass =
        {
            "P_FFE_Grass_Short_01", "P_FFE_Grass_Short_01",
            "P_FFE_Grass_Short_02", "P_FFE_Grass_Short_02",
            "P_FFE_Grass_01", "P_FFE_Grass_02",
        };

        /// <summary>Цветы: редкие пятна цвета, иначе получается клумба.</summary>
        private static readonly string[] FfeFlowers =
        {
            "P_FFE_Flower_Yellow", "P_FFE_Flowers_Blue",
            "P_FFE_Flowers_White", "P_FFE_Large_Flower",
        };

        /// <summary>Камни. Единственный ярус, которому оставляем коллайдер.</summary>
        private static readonly string[] FfeStones =
        {
            "P_FFE_Rock1", "P_FFE_Rock2", "P_FFE_Rock3", "P_FFE_Rock4",
        };

        /// <summary>
        /// Грибы — не украшение, а разметка. Они выдают, где лес «старый»:
        /// растут группами и в тени, поэтому порог у них самый жёсткий.
        /// </summary>
        private static readonly string[] FfeMushrooms =
        {
            "P_FFE_Mushroom01_Group_Small", "P_FFE_Mushroom02_Group_Small",
            "P_FFE_Mushroom03_Group", "P_FFE_Mushroom04_Group",
            "P_FFE_Mushroom_5_Group",
        };

        private const string FloorHolder = "ForestFloor";



        // ---- палитры ------------------------------------------------------

        private static readonly string[] Canopy =
        {
            "SM_Env_Tree_Birch_01", "SM_Env_Tree_Birch_02", "SM_Env_Tree_Birch_03",
            "SM_Env_Tree_Fruit_01", "SM_Env_Tree_Fruit_02", "SM_Env_Tree_Fruit_03",
            "SM_Env_Tree_Meadow_01", "SM_Env_Tree_Meadow_02",
        };

        private static readonly string[] Undergrowth =
        {
            "SM_Env_Bush_01", "SM_Env_Bush_02", "SM_Env_Bush_03",
            "SM_Env_Grass_Bush_01",
            "SM_Env_Grass_Tall_Clump_01", "SM_Env_Grass_Tall_Clump_02",
            "SM_Env_Grass_Tall_Clump_03", "SM_Env_Grass_Tall_Clump_04",
            "SM_Env_Grass_Tall_Clump_05",
            "SM_Prop_Leaves_Pile_01", "SM_Prop_Leaves_Pile_02",
        };

        /// <summary>Камни идут отдельно: только им оставляем коллайдер.</summary>
        private static readonly string[] Stones =
        {
            "SM_Env_Rock_01", "SM_Env_Rock_02", "SM_Env_Rock_03",
            "SM_Env_Rock_04", "SM_Env_Rock_05", "SM_Env_Rock_06",
            "SM_Env_Rock_Pile_01",
        };

        /// <summary>
        /// Мелочь у земли. Трава повторяется намеренно, ветки — нет.
        ///
        /// Палитра работает как мешок, из которого тянут наугад: сколько раз
        /// имя в ней встречается, настолько чаще оно и выпадет. В первой
        /// версии ветка стояла наравне с травой и выпадала каждой
        /// двенадцатой — на пяти тысячах объектов это четыреста веток,
        /// разбросанных по чистому полю. Выглядело как после бури.
        ///
        /// Валуны отсюда убраны совсем: `SM_Env_Rock_Ground_01` при имени
        /// «камешек» имеет четыре метра высоты. Их отсекает и замер яруса, но
        /// держать в списке то, что заведомо не пройдёт, — значит каждый раз
        /// читать про это в журнале.
        /// </summary>
        private static readonly string[] Litter =
        {
            "SM_Env_Grass_Med_Clump_01", "SM_Env_Grass_Med_Clump_02", "SM_Env_Grass_Med_Clump_03",
            "SM_Env_Grass_Med_Clump_01", "SM_Env_Grass_Med_Clump_02", "SM_Env_Grass_Med_Clump_03",
            "SM_Env_Grass_Short_Clump_01", "SM_Env_Grass_Short_Clump_02",
            "SM_Env_Grass_Short_Clump_03",
            "SM_Env_Grass_Short_Clump_01", "SM_Env_Grass_Short_Clump_02",
            "SM_Env_Grass_Short_Clump_03",
            "SM_Env_Flowers_Flat_01", "SM_Env_Flowers_Flat_02", "SM_Env_Flowers_Flat_03",
            "SM_Prop_Leaves_Branch_01",
        };

        // ------------------------------------------------------------------

        /// <summary>
        /// Заводит слой для мелочи, если его ещё нет.
        ///
        /// Слои живут в настройках проекта, а не в сцене, и добавляются
        /// правкой их файла. Делаем это сами: просить человека завести слой
        /// руками — значит получить сборку, которая у него работает, а у
        /// брата нет.
        /// </summary>
        private static int DetailLayer()
        {
            int found = LayerMask.NameToLayer("Detail");
            if (found >= 0) return found;

            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset.Length == 0) return -1;

            var tags = new SerializedObject(asset[0]);
            var layers = tags.FindProperty("layers");

            // Первые восемь заняты Unity, свои начинаются с восьмого.
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);

                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = "Detail";
                tags.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();

                Debug.Log("[IsoRPG] Заведён слой «Detail» под номером " + i + ".");
                return i;
            }

            Debug.LogWarning("[IsoRPG] Свободных слоёв не осталось.");
            return -1;
        }

        [MenuItem("Tools/IsoRPG/Луг: засеять три яруса", priority = 3)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            Clear();
            skewed = 0;

            var holder = new GameObject(HolderName);

            // Шаг, разброс, порог шума, частота шума, смещение шума.
            //
            // Порог — доля площади, которая вообще засевается. Чем он выше,
            // тем больше открытых полян. У крон он самый жёсткий: роща должна
            // быть рощей, а не равномерным парком.
            // Последнее число — предел высоты яруса, метров. По нему из
            // палитры отсеивается всё, что на самом деле крупнее, чем звучит.
            int trees  = Sow(holder, Canopy,      9.0f, 3.4f, 0.56f, 0.018f,  17f, true,  0.85f, 1.35f, 40f);
            int bushes = Sow(holder, Undergrowth, 3.0f, 1.2f, 0.46f, 0.045f, 133f, false, 0.75f, 1.45f,  6f);
            int rocks  = Sow(holder, Stones,      7.0f, 2.6f, 0.62f, 0.030f, 271f, true,  0.60f, 1.60f,  5f);
            int litter = Sow(holder, Litter,      1.7f, 0.7f, 0.44f, 0.080f, 419f, false, 0.70f, 1.50f, 1.2f);

            NavBake.Rebake();

            Selection.activeGameObject = holder;

            Debug.Log("[IsoRPG] Луг засеян: крон " + trees + ", подлеска " + bushes +
                      ", камней " + rocks + ", мелочи " + litter +
                      ", всего " + (trees + bushes + rocks + litter) +
                      (skewed > 0 ? ". Посадка не сошлась у " + skewed +
                                    " — оставлены на нуле, чтобы не улетели" : "") +
                      ". Для сравнения: в демо-сцене Synty около 12 000.");
        }

        /// <summary>
        /// Только деревья Enchanted Forest. Первый шаг дробного наполнения.
        ///
        /// Смысл шага — проверить ОДНУ вещь: чист ли набор, который приехал
        /// в URP-версии. Enchanted Forest весь вечер выглядел правильно и не
        /// потребовал ни одной моей правки, потому что его материалы сидят на
        /// Shader Graph, то есть изначально под наш конвейер. Луг же
        /// существует только под старый, и всё, что мы ловили два часа, —
        /// следствия его перевода.
        ///
        /// Поэтому здесь нет ни подлеска, ни травы, ни камней. Одна вещь за
        /// круг: поставили, посмотрели, поняли. Дальше добавляем следующий
        /// ярус — но только если этот чистый.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Роща: только деревья Enchanted Forest", priority = 5)]
        public static void Grove()
        {
            if (EditorApplication.isPlaying) return;

            Clear();
            skewed = 0;

            var holder = new GameObject(HolderName);

            // Пальмы и мелочь убраны по решению Павла 28.08.2026.
            //
            // «Не подходят по смыслу» — и это не придирка: папоротниковое
            // дерево читается как тропики, а у нас умеренный лес. Один
            // чужеродный силуэт ломает место сильнее, чем десяток мелких
            // огрехов, потому что он спорит с самой историей.
            //
            // Мелкие деревья убраны по другой причине: рядом с крупными они
            // читаются не как молодая поросль, а как ошибка масштаба.
            // Подлесок этот ярус закроет лучше — он для того и нужен.
            // Только лиственные кроны — выбор Павла 28.08.2026.
            //
            // `Tree_Large_01` убран: у него крона собрана из крупных гранёных
            // комков, и рядом с лиственными деревьями это читается как два
            // разных набора. Один чужеродный силуэт ломает место сильнее
            // десятка мелких огрехов.
            //
            // `Tree_Large_02` той же лиственной формы, но его материал даёт
            // магенту даже на исправном URP-шейдере — разбирается отдельно.
            // Лучше лес из двух моделей, чем из трёх с розовой.
            //
            // Разнообразие даём разбросом роста: 0.85–1.5 вместо прежнего
            // узкого. Два дерева при таком разбросе читаются как лес, а не
            // как копипаста.
            string[] palette =
            {
                "SM_Env_Tree_Medium_01", "SM_Env_Tree_Medium_02",
            };

            // Полметра вглубь.
            //
            // Формально дерево и так стоит нижней точкой на нуле — но у этих
            // моделей низ это широкий корневой раструб, и на глаз он висит:
            // читается как «низ ствола» то место, где раструб уже сомкнулся.
            // Разница между геометрическим низом и видимым и есть эти
            // полметра. Заказчик увидел её сразу, а замер по границам её не
            // видит вовсе.
            // Притапливаем на 0.9 метра. Полметра оказалось мало: у этих
            // деревьев корневой раструб широкий и низкий, и на глаз дерево
            // всё равно стояло на цыпочках.
            int trees = Sow(holder, palette, 11f, 4f, 0.50f, 0.020f, 17f,
                            true, 0.85f, 1.5f, 40f, 0.9f);

            NavBake.Rebake();

            Selection.activeGameObject = holder;

            Debug.Log("[IsoRPG] Роща: деревьев " + trees +
                      ". Только кроны, ничего больше — смотрим их одни.");
        }

        [MenuItem("Tools/IsoRPG/Луг: убрать", priority = 4)]
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == HolderName);

            if (old != null) Object.DestroyImmediate(old);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Сеет один ярус.
        ///
        /// Идём по сетке с шагом step, каждую точку сдвигаем случайно на
        /// jitter — это убирает саму сетку. Дальше спрашиваем шум: если в
        /// этой точке плотность ниже порога, ничего не ставим. Так получаются
        /// пятна.
        /// </summary>
        private static int Sow(GameObject holder, string[] palette,
                               float step, float jitter, float gate,
                               float frequency, float offset,
                               bool keepCollider, float minScale, float maxScale,
                               float maxHeight, float sink = 0.05f)
        {
            // Ярус проверяем ЗАМЕРОМ, а не именем файла.
            //
            // Первый заход делил по названиям, и «мелочь» оказалась ковром
            // валунов: `SM_Env_Rock_Ground_01` по имени звучит как камешек, а
            // на деле это плита в несколько метров. Поле превратилось в
            // каменную осыпь.
            //
            // Имя — это то, как автор назвал; высота — то, что игрок увидит.
            // Верим второму.
            var prefabs = palette.Select(Find)
                                 .Where(p => p != null)
                                 .Where(p =>
                                 {
                                     float h = Height(p);

                                     // Печатаем ВСЕХ, а не только отвергнутых.
                                     //
                                     // «Белые точки правильными зонами» —
                                     // жалоба, которую нельзя разрешить
                                     // глазами: не видно, объект это в
                                     // сантиметр или сломанный материал.
                                     // Высота отвечает сразу.
                                     Debug.Log("[IsoRPG] ярус " + maxHeight + " м: " +
                                               p.name + " высотой " + h.ToString("0.00") +
                                               (h <= maxHeight ? "  берём" : "  ОТСЕЯН"));

                                     return h <= maxHeight;
                                 })
                                 .ToArray();

            if (prefabs.Length == 0)
            {
                Debug.LogWarning("[IsoRPG] Ни одного префаба яруса не нашлось: " +
                                 string.Join(", ", palette.Take(3)) + "…");
                return 0;
            }

            int detail = DetailLayer();

            var tier = new GameObject(palette == FfeBushes ? "Bushes"
                                    : palette == FfeGrass ? "Grass"
                                    : palette == FfeFlowers ? "Flowers"
                                    : palette == FfeStones ? "Rocks"
                                    : palette == FfeMushrooms ? "Mushrooms"
                                    : palette == Canopy ? "Trees"
                                    : palette == Stones ? "Stones"
                                    : palette == Undergrowth ? "Undergrowth" : "Litter");
            tier.transform.SetParent(holder.transform, false);

            var random = new System.Random(palette.Length * 7919 + (int)offset);
            float half = Size * 0.5f;
            int placed = 0;

            // Сетка со сдвигом через ряд, как кирпичная кладка.
            //
            // Одного разброса мало: точки всё равно сидят в своих ячейках, и
            // при взгляде под сорок пять градусов ряды читаются диагоналями —
            // на кадре из игры это первое, что бросается в глаза. Сдвиг
            // половины рядов на полшага ломает выравнивание по обеим осям.
            int row = 0;

            for (float x = -half; x <= half; x += step)
            {
                row++;
                float rowShift = (row % 2 == 0) ? step * 0.5f : 0f;

                for (float z = -half + rowShift; z <= half; z += step)
                {
                    float density = Mathf.PerlinNoise((x + offset) * frequency,
                                                      (z + offset) * frequency);

                    if (density < gate) continue;

                    float ox = (float)(random.NextDouble() - 0.5) * 2f * jitter;
                    float oz = (float)(random.NextDouble() - 0.5) * 2f * jitter;

                    var at = new Vector3(x + ox, 0f, z + oz);

                    if (Mathf.Abs(at.x) > half || Mathf.Abs(at.z) > half) continue;

                    var asset = prefabs[random.Next(prefabs.Length)];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, tier.transform);

                    go.transform.position = at;
                    go.transform.rotation = Quaternion.Euler(0f, random.Next(360), 0f);

                    float scale = minScale + (float)random.NextDouble() * (maxScale - minScale);
                    go.transform.localScale = Vector3.one * scale;

                    // Сажаем на землю по нарисованным границам.
                    //
                    // Ставить по нулю нельзя: точка отсчёта у префабов где
                    // угодно — у камня часто в центре, и половина камней
                    // повисает в воздухе, а половина тонет. Это видно с
                    // первого взгляда и читается как небрежность.
                    //
                    // Мелочь дополнительно притапливаем на сантиметр-другой:
                    // трава, стоящая ровно на плоскости, показывает свой
                    // плоский низ.
                    // Сажаем по ЗАМЕРУ ПРЕФАБА, а не по границам в сцене.
                    //
                    // Границы объекта, прочитанные сразу после того, как ему
                    // задали положение, поворот и масштаб, приходят
                    // устаревшими — от прежнего состояния. Поправка выходит
                    // неверной, и часть камней зависает в воздухе. На моих
                    // кадрах из редактора это терялось среди прочего, а в
                    // игре видно сразу: гряда валунов над горизонтом.
                    //
                    // Замер префаба берётся один раз, до размещения, и
                    // умножается на масштаб. Поворот вокруг вертикали высоту
                    // не меняет, поэтому замер остаётся верным.
                    // Высота берётся с террейна, если он есть.
                    //
                    // Раньше вся земля была плоским листом на нуле, и «сесть
                    // на землю» значило вычесть свой габарит. С рельефом это
                    // уже неверно: на холме объект зависает в воздухе, во
                    // впадине тонет, и оба случая видны с первого взгляда.
                    float groundY = GroundHeight(at);

                    go.transform.position = new Vector3(
                        at.x,
                        groundY - Bottom(asset) * scale - sink,
                        at.z);

                    // Уровни детализации убираем целиком.
                    //
                    // Synty подменяет дальний объект упрощённой версией, а
                    // совсем далёкий — плоской карточкой. Механика правильная,
                    // но у нас она сломана: карточки рисовались белыми
                    // коробками, а деревья пропадали, пока к ним бежишь.
                    // Чинить переключение вслепую — это подбор, а мы уже
                    // потратили на подбор половину вечера.
                    //
                    // Оставляем только ближнюю версию. Модели у Synty
                    // низкополигональные, шесть тысяч штук на площадке 160
                    // метров тянутся и без подмены, а мигать становится
                    // нечему. Вернём LOD отдельной задачей, когда будет чем
                    // проверить выигрыш в кадрах.
                    // Уровни детализации оставляем штатными.
                    //
                    // Я снял их, чтобы убрать мигание, — и получил хуже:
                    // с выключенной группой каждый куст рисует ВСЕ свои
                    // уровни разом, включая плоскую карточку дальнего плана.
                    // Вдали эти карточки видны россыпью точек, в том числе на
                    // фоне неба.
                    //
                    // Это третья моя попытка починить LOD вслепую, и все три
                    // сделали хуже. Настоящая причина мигания — потеря
                    // растворения при конвертации шейдера, и лечится она
                    // конвертером, а не в этом файле.
                    //
                    // Flatten(go);
                    // Отключено. Затея снять уровни детализации была разумной
                    // — мигающие карточки и правда от них, — но исполнение
                    // трижды подряд удалило деревья целиком, и каждый круг
                    // проверки стоил минут. Возвращаемся к тому, что работало,
                    // и разбираем LOD отдельной задачей, когда всё остальное
                    // будет цело.

                    // Коллайдеры снимаем со всего, кроме камней и стволов.
                    //
                    // Навигация печётся по коллайдерам: оставь их на траве —
                    // и по лугу нельзя будет пройти вовсе. Через кусты и
                    // цветы человек ходит, в камень и ствол упирается.
                    foreach (var collider in go.GetComponentsInChildren<Collider>(true))
                        Object.DestroyImmediate(collider);

                    if (keepCollider) Solid(go);

                    // Статика: шесть тысяч объектов без неё съедят кадры на
                    // одной только отрисовке.
                    // Статическое объединение НЕ включаем — и это главная
                    // правка вечера.
                    //
                    // Симптом был такой: деревья пропадают, когда к ним
                    // подходишь. Я трижды искал причину в уровнях детализации
                    // и трижды делал хуже. А решающий факт лежал на виду и я
                    // его не использовал: на снимках ИЗ РЕДАКТОРА деревья
                    // всегда на месте, пропадают они только в собранной игре.
                    //
                    // Значит виновато то, что происходит при сборке. Такое
                    // делает ровно одно — склейка статических мешей. Unity
                    // сливает помеченные объекты в общие меши, и отсечение
                    // начинает считаться по границам всей пачки: подошёл
                    // близко, камера оказалась внутри этих границ — и пачка
                    // ушла из кадра целиком, вместе с деревьями.
                    //
                    // ContributeGI убран заодно: он размечает объекты под
                    // запечённый свет, которого мы не печём, и на шести
                    // тысячах объектов только раздувает сборку.
                    //
                    // Урок общий: если поведение расходится между редактором и
                    // сборкой, причину надо искать среди того, что делает
                    // сборка, а не в сцене. Я потерял на этом три круга.
                    GameObjectUtility.SetStaticEditorFlags(go, 0);

                    // Ближний слой — по палитре, а не по высоте яруса.
                    //
                    // Раньше слой давался всему, что ниже полутора метров, и
                    // это разложило ярусы ровно наоборот: грибы и цветы (они
                    // низкие) попали в ближний слой и пропадали за 45 метров
                    // прямо на глазах у игрока, а сорок пять тысяч травинок
                    // (ярус до двух метров) рисовались до горизонта.
                    //
                    // Правильно наоборот: трава — то, что даёт рябь и стоит
                    // дорого, ей ближний слой и нужен. Грибы, кусты и камни —
                    // приметные одиночные объекты, их сотни, а не тысячи, и
                    // они должны быть видны, пока видно землю.
                    bool nearOnly = palette == FfeGrass || palette == Litter ||
                                    palette == Undergrowth;

                    if (detail >= 0 && nearOnly) SetLayer(go, detail);

                    placed++;
                }
            }

            return placed;
        }

        /// <summary>
        /// Ставит препятствие по стволу или по камню.
        ///
        /// Меш-коллайдером тут пользоваться нельзя: у дерева в него попадёт
        /// вся крона, и вокруг ствола встанет невидимая стена в десять
        /// метров. Мы это уже проходили, и стоило это дня.
        /// </summary>
        private static void Solid(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>()
                              .Where(r => !(r is ParticleSystemRenderer))
                              .ToArray();

            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float wide = Mathf.Max(go.transform.lossyScale.x, go.transform.lossyScale.z);
            float tall = Mathf.Max(go.transform.lossyScale.y, 0.01f);

            var capsule = go.AddComponent<CapsuleCollider>();

            // Ствол — малая доля габарита кроны. Для камня та же формула
            // даёт почти его собственный размер, потому что камень и есть
            // сплошной.
            bool leafy = bounds.size.y > 4f;

            float radius = leafy
                ? Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.07f, 0.25f, 1.2f)
                : Mathf.Max(bounds.size.x, bounds.size.z) * 0.40f;

            capsule.radius = radius / Mathf.Max(wide, 0.01f);
            capsule.height = bounds.size.y / tall;
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
            capsule.direction = 1;
        }

        /// <summary>
        /// Снимает уровни детализации, оставляя ближний.
        ///
        /// Порядок важен: сначала запоминаем, какие рендереры относятся к
        /// нулевому уровню, потом сносим сам компонент, и только потом
        /// удаляем всё лишнее. Снести компонент первым — значит потерять
        /// сведения о том, что чему принадлежало, и остаться со всеми
        /// уровнями сразу: дерево нарисуется трижды, вложенное само в себя.
        /// </summary>
        private static void Flatten(GameObject go)
        {
            // Ничего не удаляем — только выключаем саму механику.
            //
            // Прошлая версия сносила рендереры дальних уровней и трижды
            // подряд удаляла дерево целиком. Урок: если задача звучит как
            // «пусть всегда рисуется ближний вариант», то и делать надо
            // ровно это — снять переключатель и включить всё. Удаление
            // лишнего это отдельная задача про экономию, и она сейчас не
            // стоит.
            //
            // Зачем вообще: у Synty переключение сделано растворением по
            // маске, а конвертация под URP эту логику теряет. В игре объект
            // растворяется в ноль, когда к нему подходишь, — заказчик так и
            // сказал, «не могу подойти близко».
            foreach (var group in go.GetComponentsInChildren<LODGroup>(true))
                Object.DestroyImmediate(group);

            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;
        }

        private static void FlattenOld(GameObject go)
        {
            var group = go.GetComponentInChildren<LODGroup>();
            if (group == null) return;

            var levels = group.GetLODs();
            if (levels.Length == 0) { Object.DestroyImmediate(group); return; }

            var keep = new HashSet<Renderer>(levels[0].renderers.Where(r => r != null));

            Object.DestroyImmediate(group);

            // Пустой нулевой уровень — не повод сносить объект целиком.
            //
            // Ровно это и случилось: у части префабов список рендереров
            // нулевого уровня пуст, «оставить» оказалось нечего, и я удалял
            // ВСЁ. Деревья исчезли из сцены полностью, а выглядело это как
            // «опять пропадают».
            //
            // Правило общее: пустой список — это «сведений нет», а не
            // «ничего не нужно». Разница между ними стоит целого объекта.
            if (keep.Count == 0) return;

            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || keep.Contains(renderer)) continue;

                // Сносим объект целиком, а не только рендерер: пустышка в
                // дереве сцены ничего не рисует, но остаётся мусором, а их
                // тут тысячи.
                Object.DestroyImmediate(renderer.gameObject);
            }
        }

        /// <summary>Опускает объект так, чтобы его низ лёг на землю.</summary>
        private static void Sit(GameObject go, float sink)
        {
            var renderers = go.GetComponentsInChildren<Renderer>()
                              .Where(r => !(r is ParticleSystemRenderer))
                              .ToArray();

            if (renderers.Length == 0) return;

            var box = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) box.Encapsulate(renderers[i].bounds);

            float lift = -box.min.y - sink;

            // Поправка не может быть больше самого объекта.
            //
            // Здесь была тихая беда, которую видно только на небе. У части
            // префабов нижняя граница уходит далеко под точку отсчёта —
            // например у карточки дальнего плана, нарисованной вокруг
            // центра. Поднимая объект «на его нижнюю границу», я запускал
            // такую траву на несколько метров вверх, и она висела в воздухе
            // россыпью точек.
            //
            // Заметить это на земле нельзя: точка в воздухе выглядит так же,
            // как точка на траве. Видно стало, только когда заказчик поднял
            // взгляд к небу.
            float limit = Mathf.Max(box.size.y, 0.5f);

            if (Mathf.Abs(lift) > limit)
            {
                skewed++;
                lift = 0f;
            }

            go.transform.position += new Vector3(0f, lift, 0f);
        }

        /// <summary>Сколько объектов имели неправдоподобную посадку.</summary>
        private static int skewed;

        /// <summary>Ставит слой объекту и всем его частям.</summary>
        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;

            foreach (var child in go.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        /// <summary>
        /// Нижняя точка префаба относительно его точки отсчёта, в его же
        /// единицах.
        ///
        /// Меряем один раз на префаб и запоминаем. Экземпляр ставим далеко от
        /// сцены и с единичным масштабом — тогда мировые границы совпадают с
        /// собственными, и читать их безопасно.
        /// </summary>
        private static readonly Dictionary<GameObject, float> bottoms =
            new Dictionary<GameObject, float>();

        private static float Bottom(GameObject asset)
        {
            if (bottoms.TryGetValue(asset, out float known)) return known;

            // Считаем по САМОМУ ПРЕФАБУ, ни разу не трогая сцену.
            //
            // Оба прежних захода читали границы у объекта, только что
            // поставленного в сцену, — и оба раза получали устаревшие
            // значения. Сначала часть камней зависла в воздухе, потом улетело
            // вообще всё: замер дал минус пять тысяч, и объекты ушли в небо.
            //
            // Границы меша — это данные самого меша, они не зависят ни от
            // чего. Умножаем их на матрицу места этого меша внутри префаба и
            // берём самую низкую из восьми вершин коробки. Устаревать тут
            // нечему по построению.
            float low = float.MaxValue;

            foreach (var filter in asset.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                if (filter.GetComponent<ParticleSystemRenderer>() != null) continue;

                // Место меша относительно корня префаба.
                var place = Matrix4x4.identity;

                for (var t = filter.transform; t != null && t != asset.transform; t = t.parent)
                    place = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * place;

                var box = mesh.bounds;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? box.min.x : box.max.x,
                        (i & 2) == 0 ? box.min.y : box.max.y,
                        (i & 4) == 0 ? box.min.z : box.max.z);

                    float y = place.MultiplyPoint3x4(corner).y;

                    if (y < low) low = y;
                }
            }

            if (low == float.MaxValue) low = 0f;

            // Страховка от бессмыслицы: у травинки низ не может быть в
            // полусотне метров под ногами. Если вышло такое — замер врёт, и
            // лучше поставить на ноль, чем запустить в небо.
            if (Mathf.Abs(low) > 50f)
            {
                Debug.LogWarning("[IsoRPG] У " + asset.name + " низ вышел " +
                                 low.ToString("0.0") + " — не верю, ставлю 0.");
                low = 0f;
            }

            bottoms[asset] = low;
            return low;
        }

        /// <summary>Высота префаба по нарисованным границам, метров.</summary>
        private static readonly Dictionary<GameObject, float> heights =
            new Dictionary<GameObject, float>();

        private static float Height(GameObject asset)
        {
            if (heights.TryGetValue(asset, out float known)) return known;

            var probe = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            probe.transform.position = new Vector3(0f, 5000f, 0f);

            var renderers = probe.GetComponentsInChildren<Renderer>()
                                 .Where(r => !(r is ParticleSystemRenderer))
                                 .ToArray();

            float tall = 0f;

            if (renderers.Length > 0)
            {
                var box = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) box.Encapsulate(renderers[i].bounds);

                tall = box.size.y;
            }

            Object.DestroyImmediate(probe);

            heights[asset] = tall;
            return tall;
        }

        /// <summary>
        /// Высота земли в точке. Террейн спрашиваем напрямую — это дешевле и
        /// надёжнее луча: луч промахивается мимо тонких коллайдеров и ловит
        /// то, что уже посеяли на прошлом ярусе.
        /// </summary>
        private static float GroundHeight(Vector3 at)
        {
            if (terrainCache == null)
                terrainCache = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
                                     .FirstOrDefault();

            if (terrainCache == null) return 0f;

            return terrainCache.SampleHeight(at) + terrainCache.transform.position.y;
        }

        private static Terrain terrainCache;

        private static GameObject Find(string name)
        {
            string path = Meadow + "/" + name + ".prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (asset != null) return asset;

            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                string found = AssetDatabase.GUIDToAssetPath(guid);

                if (Path.GetFileNameWithoutExtension(found) == name)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(found);
            }

            Debug.LogWarning("[IsoRPG] Нет префаба " + name);
            return null;
        }


        /// <summary>
        /// Земля под лесом TriForge: кусты, трава, цветы, камни, грибы.
        ///
        /// Порядок ярусов от крупного к мелкому, и у каждого свой шум со
        /// своим смещением — иначе трава ляжет ровно под кустами и всё
        /// соберётся в одинаковые кучи вместо полян и зарослей.
        ///
        /// Числа взяты не из головы: замер демо-сцены Synty показал, что
        /// нижних ярусов должно быть на порядок больше верхнего — 765 крон
        /// против 5228 подлеска и 6183 мелочи. Наш лес это 108 деревьев,
        /// значит травы под ним нужны тысячи, а не сотни.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Лес: засеять землю (трава, кусты, цветы)", priority = 61)]
        public static void BuildFloor()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play посев не сохранится.");
                return;
            }

            ClearFloor();
            skewed = 0;
            terrainCache = null;

            var holder = new GameObject(FloorHolder);

            // Шаг травы 0.9 м, а не 1.8. Первый посев дал 5143 кустика на
            // 160 метрах — это одна травинка на пять квадратных метров, и с
            // высоты игрока земля осталась голой плоскостью с редкой щетиной.
            // Вчетверо гуще плюс более низкий порог шума дают подстилку, а не
            // сорняки на пустыре. Для сравнения: в демо-сцене Synty нижних
            // ярусов около 12 000 на ту же площадь.
            int bushes    = Sow(holder, FfeBushes,    6.0f, 5.2f, 0.52f, 0.026f,  53f, false, 0.55f, 1.00f, 5.0f);
            // Разброс почти равен шагу. При джиттере в половину шага точка
            // не выходит за свою ячейку, и сетка посева читается глазом как
            // ряды — на кадре это видно раньше, чем сама трава. Разброс в
            // 0.9 от шага перемешивает соседние ячейки и сетка пропадает.
            // Масштаб травы 0.45–0.85, а не 1.0–2.0. Прежние значения давали
            // траву в полтора-два метра при росте героя 2.1 — он утопал в ней
            // по грудь, и в бою его просто не было видно. Лесная трава должна
            // доходить до колена, тогда она читается подстилкой, а не полем.
            // Трава объектами вернулась: шейдерная от Brute Force рассчитана
            // на террейн 50x50 метров со своей раскладкой слоёв, а у нас
            // 600x600 — на нашем масштабе она даёт белую землю. Пробовать её
            // надо в отдельной сцене-полигоне, а не на боевой арене.
            int grass     = Sow(holder, FfeGrass,     0.7f, 0.66f, 0.28f, 0.055f, 181f, false, 0.45f, 0.85f, 2.0f);
            int flowers   = Sow(holder, FfeFlowers,   2.6f, 2.3f, 0.56f, 0.070f, 307f, false, 0.55f, 0.95f, 1.5f);
            int rocks     = Sow(holder, FfeStones,    10f,  4.0f, 0.62f, 0.022f, 431f, true,  0.55f, 1.50f, 4.0f);
            int mushrooms = Sow(holder, FfeMushrooms, 7.0f, 2.8f, 0.62f, 0.090f, 577f, false, 0.55f, 0.95f, 1.0f);

            NavBake.Rebake();
            MarkFloorDirty();

            Debug.Log("[IsoRPG] Земля засеяна: кустов " + bushes + ", травы " + grass +
                      ", цветов " + flowers + ", камней " + rocks +
                      ", грибов " + mushrooms +
                      ", всего " + (bushes + grass + flowers + rocks + mushrooms) + ".");
        }

        [MenuItem("Tools/IsoRPG/Лес: убрать землю", priority = 62)]
        public static void ClearFloor()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == FloorHolder);

            if (old != null) Object.DestroyImmediate(old);
            MarkFloorDirty();
        }

        /// <summary>
        /// Помечает сцену изменённой. В пакетном режиме без этого правка не
        /// доживает до сохранения: SaveOpenScenes трогает только грязные
        /// сцены, а создание объекта через PrefabUtility сцену такой не
        /// помечает — работа делается, отчитывается в журнал и пропадает.
        /// </summary>
        private static void MarkFloorDirty()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
