using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Переводит арену с плоского листа на террейн.
    ///
    /// Зачем менять то, что работало. Плоский лист честно держал механику:
    /// один объект, ни швов, ни забот. Но у него нет трёх вещей, без которых
    /// локация не станет местом, а останется полем:
    ///
    ///   • <b>Слои текстур.</b> На террейне их рисуют кистью друг поверх
    ///     друга — трава, подстилка, грязь, дорога. Тропинка это не модель и
    ///     не декаль, а прокрашенная полоса; на листе с одной текстурой её
    ///     сделать нечем.
    ///   • <b>Рельеф.</b> Ровная плоскость читается как макет. Даже метровые
    ///     перепады дают горизонту жизнь и прячут край карты.
    ///   • <b>Трава без объектов.</b> Сейчас её 46 тысяч штук, каждая —
    ///     GameObject со своим трансформом. Террейн рисует то же самое как
    ///     detail-слой, пачками и почти бесплатно.
    ///
    /// Размер берём от VisualSize арены, а не от игрового Size: видимая земля
    /// должна уходить за границу игровой площадки, иначе виден обрыв.
    ///
    /// Что НЕ переносим: деревья. Они уже стоят объектами, с ними работают
    /// навигация и замена набора; Terrain Trees добавили бы второй способ
    /// хранить одно и то же. Переведём отдельно, если понадобится дальность.
    /// </summary>
    public static class TerrainBuilder
    {
        private const string DataPath = "Assets/_Game/Art/Materials/ArenaTerrain.asset";

        private const string Textures =
            "Assets/TriForge Assets/Fantasy Forest Environment/Textures/Terrain/";

        private const string Foliage =
            "Assets/TriForge Assets/Fantasy Forest Environment/Prefabs/Ground Foliage/";

        /// <summary>Сторона террейна, метров. Совпадает с видимой землёй арены.</summary>
        private const float Side = 600f;

        /// <summary>
        /// Полная высота карты, метров. Это не высота холмов, а потолок:
        /// значения карты нормированы от 0 до 1 и умножаются на него, поэтому
        /// большой потолок при малых значениях даёт грубые ступеньки.
        /// Сорок метров при холмах в три-четыре — разумный запас.
        /// </summary>
        private const float Height = 40f;

        /// <summary>
        /// Базовый уровень карты высот, доля от потолка. Ровно на этой высоте
        /// лежит середина арены, поэтому на неё же опускается объект: иначе
        /// «ноль» сцены оказывается под землёй.
        /// </summary>
        private const float Floor = 0.06f;

        /// <summary>Разрешение карты высот. 513 на 600 метров — метр на клетку.</summary>
        private const int HeightRes = 513;

        /// <summary>Разрешение карты текстур: вчетверо мельче, этого хватает.</summary>
        private const int SplatRes = 1024;

        /// <summary>Сетка мелочи. 1024 — как в демо-сцене автора.</summary>
        private const int DetailRes = 1024;

        /// <summary>Размер участка мелочи. Меньше — точнее подгрузка, больше — дешевле.</summary>
        private const int DetailPatch = 16;

        /// <summary>
        /// Слои земли по порядку: первый — основа, дальше то, что рисуют поверх.
        /// Тайлинг у каждого свой, в метрах: у травы рисунок мелкий, у дороги
        /// крупный, и один общий размер плитки испортил бы оба.
        /// </summary>
        private static readonly (string file, float tile)[] Layers =
        {
            ("T_FFE_Grass01.tga",       2.5f),
            ("T_FFE_Forestfloor01.tga", 3.0f),
            ("T_FFE_Dirt01.tga",        3.5f),
            // Тропа грунтовая, а не мощёная. T_FFE_Road01 — это брусчатка;
            // в лесу она читается как городская мостовая, откуда ни возьмись.
            // Dirt2 даёт вытоптанную землю, то есть ровно тропу.
            ("T_FFE_Dirt2.tga",         3.0f),
        };

        /// <summary>Трава для detail-слоёв: те же префабы, что сеяли объектами.</summary>
        private static readonly string[] Grass =
        {
            "P_FFE_Grass_Short_01", "P_FFE_Grass_Short_02",
            "P_FFE_Grass_01", "P_FFE_Grass_02",
            "P_FFE_Flowers_Blue", "P_FFE_Flowers_White", "P_FFE_Flower_Yellow",
        };

        /// <summary>
        /// Щуп: что реально лежит в слоях террейна и в его картах.
        ///
        /// Нужен потому, что раскраска считается формулой, а видна только в
        /// игре: ошибка в одном индексе даёт поле, залитое дорогой, и по коду
        /// это не отличить от правильного. Печатаем средние веса — они сразу
        /// говорят, какой слой съел карту.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Террейн: что в слоях", priority = 16)]
        public static void Diag()
        {
            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
                                .FirstOrDefault();

            if (terrain == null) { Debug.LogWarning("[IsoRPG] Террейна в сцене нет."); return; }

            var data = terrain.terrainData;

            Debug.Log("[IsoRPG] Террейн " + data.size.x + " x " + data.size.z +
                      " м, материал " + (terrain.materialTemplate != null
                          ? terrain.materialTemplate.shader.name : "НЕТ"));

            for (int i = 0; i < data.terrainLayers.Length; i++)
            {
                var l = data.terrainLayers[i];
                Debug.Log("   слой " + i + ": " +
                          (l != null && l.diffuseTexture != null ? l.diffuseTexture.name : "ПУСТО") +
                          ", плитка " + (l != null ? l.tileSize.x : 0f) + " м");
            }

            int res = data.alphamapResolution;
            var maps = data.GetAlphamaps(0, 0, res, res);
            int n = data.terrainLayers.Length;

            for (int i = 0; i < n; i++)
            {
                double sum = 0;
                for (int y = 0; y < res; y += 4)
                    for (int x = 0; x < res; x += 4)
                        sum += maps[y, x, i];

                int cells = ((res + 3) / 4) * ((res + 3) / 4);
                Debug.Log("   доля слоя " + i + ": " + (sum / cells * 100.0).ToString("0.0") + "%");
            }

            for (int i = 0; i < data.detailPrototypes.Length; i++)
            {
                var layer = data.GetDetailLayer(0, 0, data.detailResolution,
                                                data.detailResolution, i);
                long total = 0;
                foreach (int v in layer) total += v;

                var p = data.detailPrototypes[i];
                Debug.Log("   мелочь " + i + " (" +
                          (p.prototype != null ? p.prototype.name : "?") + "): " + total + " шт");
            }
        }

        [MenuItem("Tools/IsoRPG/Террейн: собрать вместо плоской земли", priority = 15)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play террейн не сохранится.");
                return;
            }

            var data = CreateData();
            Shape(data);
            Paint(data);
            SowDetails(data);

            AssetDatabase.SaveAssets();

            // Ставим объект. Террейн растёт от своего угла, поэтому смещаем
            // на половину стороны — иначе центр карты окажется в углу арены.
            var old = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
                            .FirstOrDefault();

            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "ArenaTerrain";
            // Опускаем на высоту базового уровня рельефа. Карта высот
            // нормирована, и «пол» в 0.06 от потолка в 40 метров это 2.4 м
            // над нулём. Оставленный на нуле террейн поднимал землю выше
            // точки появления героя — и он оказывался внутри холма.
            go.transform.position = new Vector3(-Side / 2f, -Floor * Height - 0.05f, -Side / 2f);
            go.isStatic = true;

            var terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 400f;
            terrain.detailObjectDistance = 120f;
            terrain.detailObjectDensity = 1f;
            // Материал задаём явно. Оставленный по умолчанию, террейн берёт
            // встроенный шейдер, который в URP не компилируется, — и всё поле
            // становится розовым. Розовая земля в 600 метров выглядит как
            // катастрофа, а лечится одной строкой.
            terrain.materialTemplate = TerrainMaterial();

            RemoveOldGround();

            NavBake.Rebake();
            MarkDirty();

            Debug.Log("[IsoRPG] Террейн собран: " + Side + " x " + Side +
                      " м, слоёв земли " + data.terrainLayers.Length +
                      ", видов мелочи " + data.detailPrototypes.Length + ".");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Материал террейна под наш конвейер.
        ///
        /// Имя шейдера ищем по нескольким вариантам: у URP он назывался
        /// по-разному в разных версиях, и жёсткая строка ломается при
        /// обновлении пакета молча — розовым полем, а не ошибкой.
        /// </summary>
        private static Material TerrainMaterial()
        {
            const string path = "Assets/_Game/Art/Materials/M_ArenaTerrain.mat";

            string[] names =
            {
                "Universal Render Pipeline/Terrain/Lit",
                "Universal Render Pipeline/Terrain/Lit (Add Pass)",
                "Nature/Terrain/Standard",
            };

            Shader shader = null;

            foreach (var n in names)
            {
                shader = Shader.Find(n);
                if (shader != null) { Debug.Log("[IsoRPG] Шейдер террейна: " + n); break; }
            }

            if (shader == null)
            {
                Debug.LogWarning("[IsoRPG] Не нашёл шейдер террейна — поле будет розовым.");
                return null;
            }

            AssetDatabase.DeleteAsset(path);
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);

            return material;
        }

        private static TerrainData CreateData()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            AssetDatabase.DeleteAsset(DataPath);

            var data = new TerrainData
            {
                heightmapResolution = HeightRes,
                baseMapResolution = 1024,
            };

            data.SetDetailResolution(DetailRes, DetailPatch);
            data.size = new Vector3(Side, Height, Side);

            AssetDatabase.CreateAsset(data, DataPath);
            return data;
        }

        /// <summary>
        /// Рельеф: пологие холмы из трёх слоёв шума.
        ///
        /// В центре карты нарочно ровно. Там дерутся, там стоят постройки и
        /// работает навигация — уклон под ногами боя мешает и ломает попадания
        /// по земле. Холмы поднимаются к краям, где они и нужны: закрывают
        /// горизонт и прячут границу площадки.
        /// </summary>
        private static void Shape(TerrainData data)
        {
            int res = data.heightmapResolution;
            var map = new float[res, res];

            // Базовая высота: небольшой подъём над нулём, чтобы можно было и
            // вкопать русло, и насыпать холм. Вынесена в поле класса, потому
            // что на неё же опускается сам объект террейна.
            const float floor = Floor;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);
                    float v = (float)y / (res - 1);

                    // Расстояние от центра в долях половины стороны.
                    float dx = (u - 0.5f) * 2f;
                    float dy = (v - 0.5f) * 2f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // Ровное ядро радиусом в четверть карты, дальше подъём.
                    float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1f, r));

                    float n1 = Mathf.PerlinNoise(u * 3.1f + 11f, v * 3.1f + 7f);
                    float n2 = Mathf.PerlinNoise(u * 7.7f + 31f, v * 7.7f + 3f);
                    float n3 = Mathf.PerlinNoise(u * 17f + 71f, v * 17f + 23f);

                    float hills = n1 * 0.62f + n2 * 0.28f + n3 * 0.10f;

                    map[y, x] = floor + hills * rim * 0.22f + (n3 - 0.5f) * 0.004f;
                }
            }

            data.SetHeights(0, 0, map);
        }

        /// <summary>
        /// Раскраска: трава по всей площади, подстилка под холмами, грязь
        /// пятнами и дорога — извилистая тропа через всю карту.
        ///
        /// Тропа считается синусоидой, а не рисуется точками: так она проходит
        /// через центр (там, где ходят) и остаётся плавной при любом размере
        /// карты. Ширина берётся с запасом, потому что края растушёвываются.
        /// </summary>
        private static void Paint(TerrainData data)
        {
            var layers = new List<TerrainLayer>();

            foreach (var (file, tile) in Layers)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + file);

                if (tex == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет текстуры земли " + Textures + file);
                    continue;
                }

                string path = "Assets/_Game/Art/Materials/TL_" +
                              Path.GetFileNameWithoutExtension(file) + ".terrainlayer";

                AssetDatabase.DeleteAsset(path);

                var layer = new TerrainLayer
                {
                    diffuseTexture = tex,
                    tileSize = new Vector2(tile, tile),
                    tileOffset = Vector2.zero,
                };

                AssetDatabase.CreateAsset(layer, path);
                layers.Add(layer);
            }

            if (layers.Count == 0)
            {
                Debug.LogWarning("[IsoRPG] Ни одного слоя земли — раскраска пропущена.");
                return;
            }

            data.terrainLayers = layers.ToArray();
            data.alphamapResolution = SplatRes;

            int res = data.alphamapResolution;
            int n = layers.Count;
            var maps = new float[res, res, n];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);
                    float v = (float)y / (res - 1);

                    // Внимание к осям: в alphamap первый индекс — по Z карты,
                    // второй — по X. Перепутать их значит получить раскраску,
                    // повёрнутую на девяносто градусов относительно рельефа.
                    float grass = 1f;
                    float litter = Mathf.PerlinNoise(u * 4.3f + 5f, v * 4.3f + 19f);
                    float dirt = Mathf.PerlinNoise(u * 9.1f + 41f, v * 9.1f + 13f);

                    litter = Band(litter, 0.45f, 0.75f);
                    dirt = Band(dirt, 0.62f, 0.82f) * 0.7f;

                    // Тропа: синусоида по X, идущая через всю карту.
                    float trailCenter = 0.5f + 0.13f * Mathf.Sin(v * Mathf.PI * 2.2f);
                    float road = 1f - Band(Mathf.Abs(u - trailCenter), 0.012f, 0.032f);

                    // Вторая тропа, поперёк — развилка у центра.
                    float trail2 = 0.5f + 0.10f * Mathf.Sin(u * Mathf.PI * 1.7f + 1.2f);
                    road = Mathf.Max(road, 1f - Band(Mathf.Abs(v - trail2), 0.010f, 0.028f));

                    var w = new float[n];
                    w[0] = grass;
                    if (n > 1) w[1] = litter;
                    if (n > 2) w[2] = dirt;
                    if (n > 3) w[3] = road * 1.6f;

                    float sum = 0f;
                    foreach (float f in w) sum += f;
                    if (sum <= 0.0001f) { w[0] = 1f; sum = 1f; }

                    for (int i = 0; i < n; i++) maps[y, x, i] = w[i] / sum;
                }
            }

            data.SetAlphamaps(0, 0, maps);
        }

        /// <summary>
        /// Мелочь: трава и цветы как detail-слои.
        ///
        /// Плотность взята с замера демо-сцены автора: у него травы двух видов
        /// 19.5 и 22.4 штуки на 100 м², цветов около двух. Это соотношение и
        /// переносим, а не абсолютные числа — площадь у нас другая.
        ///
        /// На тропе травы нет: она читается тропой ровно потому, что вытоптана.
        /// </summary>
        /// <summary>
        /// Плавная ступенька от 0 до 1 между a и b.
        ///
        /// Своя, потому что <c>Mathf.SmoothStep</c> в Unity делает НЕ то, что
        /// одноимённая функция в шейдерах: она интерполирует между двумя
        /// значениями, а не нормирует третье. То есть SmoothStep(0.012, 0.032, x)
        /// возвращает число около 0.03, а не 0 или 1 — и «1 минус это» даёт
        /// почти единицу ВЕЗДЕ. Так дорога заняла 57% карты вместо двух
        /// процентов тропы. Ошибка тихая: код читается правильно.
        /// </summary>
        private static float Band(float x, float a, float b) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        private static void SowDetails(TerrainData data)
        {
            var protos = new List<DetailPrototype>();

            foreach (var name in Grass)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Foliage + name + ".Prefab")
                          ?? AssetDatabase.LoadAssetAtPath<GameObject>(Foliage + name + ".prefab");

                if (prefab == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет префаба мелочи " + name);
                    continue;
                }

                protos.Add(new DetailPrototype
                {
                    prototype = prefab,
                    usePrototypeMesh = true,
                    useInstancing = true,
                    renderMode = DetailRenderMode.VertexLit,
                    minWidth = 0.8f,
                    maxWidth = 1.4f,
                    minHeight = 0.8f,
                    maxHeight = 1.5f,
                    noiseSpread = 0.35f,
                    healthyColor = Color.white,
                    dryColor = Color.white,
                });
            }

            if (protos.Count == 0) return;

            data.detailPrototypes = protos.ToArray();

            int res = data.detailResolution;

            // Сколько штук на клетку сетки. Клетка = Side / res метров,
            // отсюда и пересчёт из «на 100 м²» в плотность слоя.
            float cell = Side / res;              // метров на клетку
            float cellArea = cell * cell;         // м² на клетку

            for (int i = 0; i < protos.Count; i++)
            {
                // Первые четыре — трава, остальные цветы: у них плотность
                // на порядок ниже, иначе поле превращается в клумбу.
                float per100 = i < 4 ? 11f : 0.7f;
                float perCell = per100 / 100f * cellArea;

                var layer = new int[res, res];
                var random = new System.Random(9001 + i * 7919);

                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float u = (float)x / (res - 1);
                        float v = (float)y / (res - 1);

                        // Пятнами, как и всё остальное: ровный ковёр читается
                        // как газон, а не как лесная поляна.
                        float density = Mathf.PerlinNoise(u * 6.5f + i * 17f,
                                                          v * 6.5f + i * 29f);

                        if (density < 0.42f) continue;

                        // Тропу не засеваем.
                        float trailCenter = 0.5f + 0.13f * Mathf.Sin(v * Mathf.PI * 2.2f);
                        if (Mathf.Abs(u - trailCenter) < 0.02f) continue;

                        float trail2 = 0.5f + 0.10f * Mathf.Sin(u * Mathf.PI * 1.7f + 1.2f);
                        if (Mathf.Abs(v - trail2) < 0.018f) continue;

                        // Плотность на клетку у нас меньше единицы: клетка
                        // сетки 0.59 м, а травы 11 штук на 100 м² — это 0.04
                        // на клетку. Округление превращало это в ноль, и поле
                        // осталось голым при заполненных слоях. Поэтому не
                        // округляем, а бросаем жребий: доля становится
                        // вероятностью поставить одну штуку.
                        float chance = perCell * (density * 1.6f);
                        int count = chance >= 1f
                            ? Mathf.RoundToInt(chance)
                            : (random.NextDouble() < chance ? 1 : 0);

                        if (count > 0) layer[y, x] = count;
                    }
                }

                data.SetDetailLayer(0, 0, i, layer);
            }
        }

        /// <summary>
        /// Убирает плоский лист и рассыпанную объектами траву.
        ///
        /// Обязательно и сразу: лист висит на той же высоте, что террейн, и
        /// вдвоём они дают мерцание граней (z-fighting) по всему полю. А трава
        /// объектами теперь дублирует detail-слой — и по виду, и по цене.
        /// </summary>
        private static void RemoveOldGround()
        {
            int removed = 0;

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (go == null) continue;

                if (go.name == "GroundSheet" || go.name == "ForestFloor")
                {
                    Object.DestroyImmediate(go);
                    removed++;
                    continue;
                }

                // Коллайдер прежней земли гасим отдельно.
                //
                // Объект «Ground» — остаток плоского листа: сам лист мы
                // убрали, а коробка-коллайдер под ним осталась и продолжала
                // изображать пол. Навигация печётся по коллайдерам, поэтому
                // агент ходил по этой невидимой плоскости, а не по рельефу —
                // расхождение доходило до пяти с половиной метров, и персонаж
                // уезжал в холм по пояс. Ошибок при этом нет: обе поверхности
                // существуют, просто игрок ходит не по той.
                if (go.name == "Ground")
                {
                    foreach (var box in go.GetComponents<BoxCollider>())
                    {
                        if (!box.enabled) continue;
                        box.enabled = false;
                        removed++;
                        Debug.Log("[IsoRPG] Выключен коллайдер прежней земли на «" +
                                  go.name + "».");
                    }
                }
            }

            Debug.Log("[IsoRPG] Старая земля убрана: объектов " + removed + ".");
        }

        private static void MarkDirty()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
