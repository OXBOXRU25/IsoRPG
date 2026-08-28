using System.Collections.Generic;
using System.Linq;
using IsoRPG.Dev;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Витрина наборов: под каждый импортированный набор — своя площадка с
    /// его содержимым, разложенным по полкам.
    ///
    /// Зачем не смотреть набор в его собственной демо-сцене: демо собрана
    /// автором под его свет, его камеру и его масштаб. Ответ оттуда всегда
    /// «красиво», и он ничего не говорит про НАШУ игру. Здесь все наборы
    /// стоят в одной сцене, под одним светом, с одинаковой камерой — и
    /// рядом с нашим персонажем, потому что вопрос на самом деле в том, не
    /// выглядят ли наши люди в этих декорациях чужими.
    ///
    /// Площадки стоят за южным краем карты на собственных помостах, ряд за
    /// рядом. Дойти пешком нельзя, поэтому вместе с витриной в сцену
    /// добавляется <see cref="ShowcaseJumper"/>: F1…F9 переносят героя к
    /// площадке, F10 возвращает в зал.
    ///
    /// Всё складывается в один объект "PackShowcase" и убирается соседним
    /// пунктом меню — следов в сцене не остаётся.
    /// </summary>
    public static class PackShowcase
    {
        private const string HolderName = "PackShowcase";

        /// <summary>
        /// Сколько деталей показываем с одного набора.
        ///
        /// Не всё содержимое: у Fantasy Kingdom его две тысячи с лишним, и
        /// площадка размером с половину карты не помогает решать, а мешает.
        /// Берём равномерно по всему списку — так в выборку попадают все
        /// разделы набора, а не первые двести по алфавиту.
        /// </summary>
        private const int MaxPerPack = 240;

        /// <summary>Ширина площадки: на ней укладываются полки.</summary>
        private const float PadWidth = 56f;

        /// <summary>Зазор между соседними деталями на полке.</summary>
        private const float Gap = 1.2f;

        /// <summary>Промежуток между площадками.</summary>
        private const float PadGap = 16f;

        /// <summary>Сколько площадок в ряду, дальше — следующий ряд.</summary>
        private const int PadsPerRow = 4;

        /// <summary>
        /// Больше этого деталь не ставим.
        ///
        /// Порог был 22 метра — и отрезал ровно то, ради чего наборы
        /// покупались: гигантские деревья Enchanted Forest (30–50 м),
        /// башни, скалы. На витрине оставалась мелочь, и набор с деревьями
        /// выглядел набором без деревьев.
        ///
        /// Шестьдесят метров пропускают всё настоящее и отсекают только
        /// панорамы «вид вдалеке» — гряды на двести метров, которые
        /// накрывают собой полвитрины. Полки считаются по фактическим
        /// габаритам, поэтому гигант просто занимает свою строку целиком.
        /// </summary>
        private const float MaxItemSize = 60f;

        /// <summary>Мелочь без видимого размера (пустышки, системы частиц).</summary>
        private const float MinItemSize = 0.05f;

        /// <summary>
        /// На сколько поднимать экспонат над помостом.
        ///
        /// Три сантиметра не спасли: плита с шипами толщиной 20 см всё
        /// равно мерцала. Буфер глубины различает поверхности тем хуже, чем
        /// дальше они от камеры, а витрину смотрят с двадцати метров —
        /// зазор должен быть виден буферу, а не только в числах.
        /// Двенадцать сантиметров глазом не читаются: экспонат всё так же
        /// «стоит на полу».
        /// </summary>
        private const float Lift = 0.12f;

        /// <summary>
        /// Куда развернуть экспонаты, чтобы они смотрели в камеру.
        ///
        /// Наша камера стоит под неизменным yaw 50° и смотрит сверху-сбоку.
        /// Деталь, поставленная «как лежала», оказывается к зрителю боком:
        /// у стены видно торец, у сундука — заднюю стенку, у знамени —
        /// изнанку. Разворот на 50 + 180 ставит лицевую сторону навстречу
        /// взгляду.
        ///
        /// Разворачиваем ДО замера габаритов: полки считаются по
        /// повёрнутой детали, иначе соседи налезут друг на друга.
        /// </summary>
        private const float FaceCamera = 230f;

        private static readonly string[] SkipInPath =
        {
            "/demo", "/demos", "/scenes", "/example", "/examples",
            "/sample", "/samples", "/showcase", "/scripts", "/editor"
        };

        // ------------------------------------------------------------------

        [MenuItem("Tools/IsoRPG/Витрина наборов: собрать", priority = 45)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            Clear();

            var packs = PackCatalog.Shown.ToArray();

            if (packs.Length == 0)
            {
                Debug.LogError("[IsoRPG] Ни одного набора из каталога в проекте нет. " +
                               "Проверь PackCatalog.");
                return;
            }

            var holder = new GameObject(HolderName);
            var padMaterial = PadMaterial();

            // Начало — от западного края карты, к югу от неё. Считаем от
            // карты, а не числом: карта уже однажды выросла вправо, и все
            // записанные руками координаты разом стали врать.
            float westX = RuinsLayout.CellToWorld(0, 0).x;
            float southZ = RuinsLayout.CellToWorld(0, RuinsLayout.Map.Length - 1).z;

            var footprint = new Bounds();
            var spots = new List<Vector3>();
            var titles = new List<string>();

            // Отсчитываем от края ЗЕМЛИ, а не от края карты руин.
            //
            // На этом я потерял три круга. Карта руин кончается на Z -32, а
            // земля под ней — плита 230 на 230 — тянется до -115. Отступ в
            // шестьдесят метров от руин оставлял площадки прямо на земле:
            // помост лежит верхней гранью ровно на нуле, там же, где земля,
            // и обе поверхности мерцают, споря за пиксели. Со стороны это
            // выглядит как «мигает вся картинка», и в этом виде причина не
            // угадывается ни с первого раза, ни с третьего.
            float edgeZ = southZ;

            var groundObject = GameObject.Find("Ground");
            var groundRenderer = groundObject != null ? groundObject.GetComponent<Renderer>() : null;

            if (groundRenderer != null)
            {
                edgeZ = groundRenderer.bounds.min.z;
                westX = Mathf.Min(westX, groundRenderer.bounds.min.x);
            }
            else
            {
                Debug.LogWarning("[IsoRPG] Не нашёл Ground — отступ считаю от карты руин, " +
                                 "витрина может лечь на землю.");
            }

            float rowZ = edgeZ - 40f;
            float rowDepth = 0f;
            int total = 0, oversized = 0;

            for (int i = 0; i < packs.Length; i++)
            {
                int column = i % PadsPerRow;

                if (column == 0 && i > 0)
                {
                    rowZ -= rowDepth + PadGap;
                    rowDepth = 0f;
                }

                var origin = new Vector3(westX + column * (PadWidth + PadGap), 0f, rowZ);

                float depth = BuildPad(holder.transform, packs[i], origin,
                                       out Vector3 spot, out int placed, out int skipped,
                                       out Bounds area);

                if (total == 0 && i == 0) footprint = area; else footprint.Encapsulate(area);

                rowDepth = Mathf.Max(rowDepth, depth);
                total += placed;
                oversized += skipped;

                spots.Add(spot);
                titles.Add(packs[i].Title);
            }

            // Перенос героя — вместе с витриной, чтобы жил ровно столько же.
            var jumper = holder.AddComponent<ShowcaseJumper>();
            jumper.Spots = spots.ToArray();
            jumper.Titles = titles.ToArray();
            jumper.Home = RuinsLayout.HallCentre;

            // Тени снимаем ещё раз, уже по всему собранному дереву.
            //
            // При постановке снятие идёт по рендерерам экспоната, но часть
            // наборов подключает свои LOD и дочерние объекты позже, и до них
            // первый проход не достаёт. Проход по холдеру ловит всё разом и
            // стоит миллисекунды.
            foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // И приём теней тоже снимаем.
                //
                // Плоская плита под солнцем в двадцать градусов затеняет
                // сама себя: луч идёт почти вдоль поверхности, точности
                // теневой карты не хватает, и по плите бежит рябь — со
                // стороны «мигает свет на этом квадрате». Витрине светопись
                // не нужна вовсе: там смотрят форму и цвет модели, а ровный
                // свет для этого даже лучше.
                renderer.receiveShadows = false;
            }

            AddCommonPad(holder, padMaterial, footprint);

            WarnIfOverlapsMap(holder);

            Rebake();

            Selection.activeGameObject = holder;

            Debug.Log("[IsoRPG] Витрина: площадок " + packs.Length + ", деталей " + total +
                      (oversized > 0 ? ", крупных пропущено " + oversized : "") +
                      ". В игре F1…F" + Mathf.Min(packs.Length, 9) +
                      " переносят к набору, F10 — обратно в зал.");

            for (int i = 0; i < packs.Length && i < 9; i++)
                Debug.Log("[IsoRPG]   F" + (i + 1) + " — " + packs[i].Title +
                          (packs[i].InGame ? "  (в игре)" : "  (новый)") +
                          "   " + spots[i]);

            if (packs.Length > 9)
                Debug.LogWarning("[IsoRPG] Наборов " + packs.Length + ", а клавиш девять. " +
                                 "К последним площадкам придётся подходить в редакторе.");
        }

        [MenuItem("Tools/IsoRPG/Витрина наборов: убрать", priority = 46)]
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == HolderName);

            if (old != null) Object.DestroyImmediate(old);
        }

        // ------------------------------------------------------------------
        // Одна площадка
        // ------------------------------------------------------------------

        /// <summary>
        /// Ставит помост, раскладывает по нему детали набора и подписывает.
        /// Возвращает глубину получившейся площадки.
        /// </summary>
        private static float BuildPad(Transform parent, PackCatalog.Entry pack,
                                      Vector3 origin,
                                      out Vector3 spot, out int placed, out int oversized,
                                      out Bounds area)
        {
            var pad = new GameObject(pack.Title);
            pad.transform.SetParent(parent, false);
            pad.transform.position = origin;

            // Экспонаты — в отдельном узле, исключённом из выпечки
            // навигации. Без этого сетка считается по двум тысячам моделей
            // витрины и печётся минутами, а толку ноль: между экспонатами
            // герою нужна не тропинка в обход бочки, а свобода пройти
            // насквозь и рассмотреть заднюю стенку.
            var items = new GameObject("Items");
            items.transform.SetParent(pad.transform, false);

            var skip = items.AddComponent<NavMeshModifier>();
            skip.ignoreFromBuild = true;
            skip.applyToChildren = true;

            var models = Models(pack.Folder);

            placed = 0;
            oversized = 0;

            // Полки. Деталь занимает столько, сколько занимает: в наборе
            // рядом лежат кружка и башня, и общий шаг сетки одинаково плохо
            // подходит обеим — мелочь теряется в пустоте, крупное налезает
            // на соседа.
            float cursorX = 0f;
            float shelfZ = 0f;
            float shelfDepth = 0f;

            foreach (string path in models)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, items.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.Euler(0f, FaceCamera, 0f);

                if (!Measure(go, out Bounds bounds))
                {
                    Object.DestroyImmediate(go);
                    continue;
                }

                Vector3 size = bounds.size;

                if (size.x > MaxItemSize || size.z > MaxItemSize || size.y > MaxItemSize * 1.5f)
                {
                    Object.DestroyImmediate(go);
                    oversized++;

                    // Называем поимённо: пропущенное молча выглядит как
                    // «в наборе этого нет», и вопрос возвращается к нам
                    // через час в виде «а где деревья?».
                    Debug.Log("[IsoRPG] " + pack.Title + ": не ставлю " +
                              System.IO.Path.GetFileNameWithoutExtension(path) +
                              " — " + size.x.ToString("0") + "×" + size.y.ToString("0") +
                              "×" + size.z.ToString("0") + " м, накроет соседей.");
                    continue;
                }

                if (size.x < MinItemSize && size.z < MinItemSize)
                {
                    Object.DestroyImmediate(go);
                    continue;
                }

                float step = size.x + Gap;

                if (cursorX + step > PadWidth && cursorX > 0f)
                {
                    cursorX = 0f;
                    shelfZ += shelfDepth + Gap;
                    shelfDepth = 0f;
                }

                // Ставим по нарисованным границам, а не по началу координат
                // модели: у покупных наборов точка отсчёта где угодно — у
                // двери в петле, у дерева в кроне. По границам деталь всегда
                // садится на помост и попадает в свою ячейку.
                var target = new Vector3(cursorX + size.x * 0.5f, 0f, shelfZ + size.z * 0.5f);
                Vector3 centre = items.transform.InverseTransformPoint(bounds.center);
                float bottom = items.transform.InverseTransformPoint(bounds.min).y;

                // Приподнимаем на сантиметры над помостом.
                //
                // Плоские детали — плиты пола, коврики, потолочные панели —
                // толщиной почти в ноль. Посаженные низом ровно на уровень
                // помоста, они оказываются С НИМ В ОДНОЙ ПЛОСКОСТИ, и обе
                // поверхности начинают спорить за пиксели: пол мерцает
                // полосами при каждом шаге. У крупных деталей этого не
                // видно, поэтому ошибка находится не сразу — мерцают именно
                // плитки, а ищут причину во всей витрине.
                go.transform.localPosition = new Vector3(target.x - centre.x,
                                                         -bottom + Lift,
                                                         target.z - centre.z);

                // Тени снимаем со всего, что стоит на витрине. Склад не
                // должен участвовать в освещении мира: две тысячи предметов
                // за краем карты стоят дороже, чем весь остальной кадр, а
                // видно их всё равно только с их же площадки.
                foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                cursorX += step;
                shelfDepth = Mathf.Max(shelfDepth, size.z);
                placed++;
            }

            float depth = shelfZ + shelfDepth;

            // Площадка растёт полками на СЕВЕР — в сторону карты. Поэтому,
            // разложив её, сдвигаем целиком на собственную глубину к югу:
            // иначе линия начала уходит от карты, а сама витрина ползёт
            // обратно на игровую площадь.
            //
            // Ровно на этом я и попался: отодвинул начало со ста метров на
            // сто сорок, а площадка Dungeons всё равно накрывала карту от
            // Z -180 до Z -42 — потому что двигал я не тот край.
            pad.transform.position += new Vector3(0f, 0f, -(depth + 14f));

            AddSign(pad.transform, pack, placed, models.Count, depth);

            // Герой встаёт у южного края, лицом к набору: там же табличка,
            // и первое, что он видит, — чей это набор.
            spot = pad.transform.TransformPoint(new Vector3(PadWidth * 0.5f, 0f, -2f));

            // Границы площадки — по расчёту, а не по нарисованным границам
            // того, что на ней стоит.
            //
            // У персонажей рендерер скелетный, и его границы в редакторе
            // заданы с запасом «на любую позу» — у иного экспоната они
            // уходят на сотни метров. Общий помост, посчитанный по ним,
            // растянулся на 619 метров и лёг поверх карты: земля и помост
            // оказались на одной высоте и замерцали. Расчётные границы врать
            // не умеют — площадка занимает ровно то, что мы ей отвели.
            var padCentre = pad.transform.position +
                            new Vector3(PadWidth * 0.5f, 0f, depth * 0.5f - 3f);

            area = new Bounds(padCentre, new Vector3(PadWidth + 6f, 1f, depth + 10f));

            return depth + 10f;
        }

        /// <summary>
        /// Один помост на всю витрину, а не по помосту на набор.
        ///
        /// Отдельные помосты оставляли между наборами пустоту — дыры, через
        /// которые не пройти: за краем карты земли нет, и навигация
        /// обрывается на кромке. Чтобы сравнить два набора, приходилось
        /// телепортироваться, а сравнение глазом требует именно перейти от
        /// одного к другому, не теряя картинку из виду.
        ///
        /// Размер берём по фактическим границам расставленного, а не по
        /// расчётным: часть деталей вылезает за свою ячейку, и посчитанный
        /// прямоугольник оказался бы уже настоящего.
        /// </summary>
        private static void AddCommonPad(GameObject holder, Material material, Bounds area)
        {
            if (area.size.sqrMagnitude < 1f) return;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Pad";
            floor.transform.SetParent(holder.transform, true);

            const float Margin = 10f;

            floor.transform.position = new Vector3(area.center.x, -0.2f, area.center.z);
            floor.transform.localScale = new Vector3(area.size.x + Margin * 2f,
                                                     0.4f,
                                                     area.size.z + Margin * 2f);

            var renderer = floor.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Тайлинг по фактическому размеру: та же текстура, растянутая на
            // триста метров, превращается в мыло и снова читается как
            // отсутствие текстуры.
            const float MetersPerTile = 8f;

            var tiles = new Vector2((area.size.x + Margin * 2f) / MetersPerTile,
                                    (area.size.z + Margin * 2f) / MetersPerTile);

            if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", tiles);
            if (material.HasProperty("_MainTex")) material.SetTextureScale("_MainTex", tiles);

            EditorUtility.SetDirty(material);

            Debug.Log("[IsoRPG] Общий помост " + (area.size.x + Margin * 2f).ToString("0") +
                      " на " + (area.size.z + Margin * 2f).ToString("0") + " м — между наборами " +
                      "можно ходить пешком.");
        }

        /// <summary>
        /// Табличка у ближнего края.
        ///
        /// Низко и мелко — намеренно. Прошлые витрины подписывались крупным
        /// текстом над содержимым, и подпись закрывала ровно то, ради чего
        /// витрину и собирали. Название нужно прочитать один раз, а модели
        /// смотреть долго.
        /// </summary>
        private static void AddSign(Transform pad, PackCatalog.Entry pack,
                                    int placed, int total, float depth)
        {
            var go = new GameObject("Sign");
            go.transform.SetParent(pad, false);
            go.transform.localPosition = new Vector3(PadWidth * 0.5f, 1.1f, -3.2f);

            // Разворот под нашу камеру: она стоит под неизменным углом,
            // поэтому надпись достаточно повернуть один раз.
            go.transform.localRotation = Quaternion.Euler(35f, 50f, 0f);

            var mesh = go.AddComponent<TextMesh>();

            mesh.text = pack.Title + "   ·   " + placed + " из " + total + "   ·   " +
                        (pack.InGame ? "В ИГРЕ" : "НОВЫЙ") + "\n" + pack.Origin;

            mesh.characterSize = 0.05f;
            mesh.fontSize = 48;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = pack.InGame
                ? new Color32(0x9C, 0xD1, 0x6A, 0xFF)
                : new Color32(0xE0, 0x90, 0x40, 0xFF);
        }

        // ------------------------------------------------------------------
        // Что показываем
        // ------------------------------------------------------------------

        /// <summary>
        /// Список деталей набора: префабы, а если их нет — модели.
        ///
        /// KayKit приезжает без префабов вовсе, одними fbx, и по правилу
        /// «берём только префабы» наш собственный набор оказался бы на
        /// витрине пустой площадкой.
        /// </summary>
        private static List<string> Models(string folder)
        {
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Where(Suitable)
                                     .OrderBy(p => p)
                                     .ToList();

            if (paths.Count == 0)
                paths = AssetDatabase.FindAssets("t:Model", new[] { folder })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Where(Suitable)
                                     .OrderBy(p => p)
                                     .ToList();

            return Thin(paths, MaxPerPack);
        }

        private static bool Suitable(string path)
        {
            string lower = path.ToLowerInvariant();
            foreach (string skip in SkipInPath)
                if (lower.Contains(skip)) return false;

            return true;
        }

        /// <summary>
        /// Прореживание равномерно по всему списку.
        ///
        /// Список отсортирован по пути, то есть разложен по разделам
        /// набора. Обрезание «первых N» показало бы один-два раздела
        /// целиком и ни одного из остальных; шаг через весь список даёт по
        /// горсти из каждого.
        /// </summary>
        private static List<string> Thin(List<string> paths, int limit)
        {
            if (paths.Count <= limit) return paths;

            var result = new List<string>(limit);
            double step = (double)paths.Count / limit;

            for (int i = 0; i < limit; i++)
                result.Add(paths[(int)(i * step)]);

            return result;
        }

        /// <summary>
        /// Нарисованные границы объекта.
        ///
        /// Через Renderer, а не коллайдер: у декоративных деталей коллайдера
        /// может не быть вовсе, а нарисованные границы есть всегда. Системы
        /// частиц отсеиваем: в редакторе они не играют, границы у них нулевые
        /// или бессмысленно огромные.
        /// </summary>
        private static bool Measure(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds();

            var renderers = go.GetComponentsInChildren<Renderer>()
                              .Where(r => !(r is ParticleSystemRenderer) && r.enabled)
                              .ToArray();

            if (renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            return bounds.size.sqrMagnitude > 0.0001f;
        }

        // ------------------------------------------------------------------
        // Вспомогательное
        // ------------------------------------------------------------------

        /// <summary>
        /// Материал помоста — тёмно-серый, чтобы не спорить с наборами.
        ///
        /// Заводится один раз ассетом. Материал, созданный в памяти, Unity
        /// записывает внутрь сцены, и при каждой пересборке витрины в сцене
        /// оседает ещё одна его копия.
        /// </summary>
        private static Material PadMaterial()
        {
            const string path = "Assets/_Game/Materials/M_ShowcasePad.mat";

            // Старый ассет удаляем, а не переиспользуем.
            //
            // Переиспользование выглядело бережно — «вдруг человек правил
            // материал руками», — но на деле означало, что любая правка
            // здесь молча не применяется: помост так и остался серой плитой
            // без текстуры, хотя код уже клал на него землю.
            AssetDatabase.DeleteAsset(path);

            // Берём землю нашего мира, а не ровный серый.
            //
            // Однотонная плита читается не как пол, а как пропавшая
            // текстура — первое, что о ней сказали: «пропала вся текстура на
            // полу». И это справедливо: глазу не за что зацепиться, масштаб
            // деталей не с чем сравнить, а витрина как раз про масштаб.
            var ground = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Materials/M_Ground.mat");

            Material material;

            if (ground != null)
            {
                material = new Material(ground);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                material = new Material(shader);
                material.color = new Color32(0x3A, 0x38, 0x35, 0xFF);
            }

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            // Возвращаем сам объект, а не результат загрузки: сразу после
            // CreateAsset база ещё не проиндексирована, и LoadAssetAtPath
            // отдаёт пустую ссылку — помост вышел бы без материала.
            return material;
        }

        /// <summary>
        /// Перепекает навигацию, чтобы по помостам можно было ходить.
        ///
        /// Витрина стоит за краем карты, и её помосты становятся отдельными
        /// островами сетки. Перенос героя (<c>NavMeshAgent.Warp</c>)
        /// работает на любом острове, а вот ходить по площадке без выпечки
        /// не выйдет вовсе: клик по земле не найдёт пути.
        /// </summary>
        private static void Rebake()
        {
            var ground = GameObject.Find("Ground");

            if (ground == null)
            {
                Debug.LogWarning("[IsoRPG] Не нашёл объект Ground — навигацию под " +
                                 "витриной не перепёк. По площадкам ходить не выйдет.");
                return;
            }

            var surface = ground.GetComponent<NavMeshSurface>();

            if (surface == null)
            {
                Debug.LogWarning("[IsoRPG] На Ground нет NavMeshSurface — собери сцену " +
                                 "заново пунктом «Собрать песочницу».");
                return;
            }

            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);

            Debug.Log("[IsoRPG] Навигация перепечена с учётом помостов витрины.");
        }

        /// <summary>
        /// Проверяет, что витрина не залезла на игровую карту.
        ///
        /// Заведено после того, как площадки наползли на руины и это
        /// разбиралось три круга подряд: сначала как «мигает», потом как
        /// «жёлтый блок», потом как «чужие бочки посреди леса». Все три
        /// жалобы были про одно, и ни одна не называла причину — потому что
        /// со стороны игрока наложение выглядит как что угодно, кроме
        /// наложения.
        ///
        /// Проверка стоит одного сравнения границ и печатает виновника
        /// сразу, а не после игры.
        /// </summary>
        private static void WarnIfOverlapsMap(GameObject holder)
        {
            var ground = GameObject.Find("Ground");
            if (ground == null) return;

            var groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer == null) return;

            Bounds map = groundRenderer.bounds;

            // Плоскую карту растягиваем по высоте: иначе пересечение с
            // объектами, стоящими НА ней, не считается пересечением.
            map.Encapsulate(new Vector3(map.center.x, 50f, map.center.z));

            foreach (Transform pad in holder.transform)
            {
                var renderers = pad.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;

                var area = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) area.Encapsulate(renderers[i].bounds);

                if (!area.Intersects(map)) continue;

                Debug.LogError("[IsoRPG] Площадка «" + pad.name + "» залезла на карту: " +
                               "её край на Z " + area.max.z.ToString("0") +
                               ", а карта кончается на Z " + map.max.z.ToString("0") +
                               ". Экспонаты окажутся посреди игры, а помост будет " +
                               "мерцать на уровне земли.");
            }
        }
    }
}
