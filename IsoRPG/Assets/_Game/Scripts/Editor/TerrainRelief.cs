using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Рельеф террейна по стандартам Synty.
    ///
    /// <b>Числа сняты с их собранных сцен</b>, а не подобраны:
    /// перепад высот держится в 10–17% от стороны участка, средняя крутизна
    /// 13–15°, размер форм 8–14 метров. Обрывы у автора делаются НЕ рельефом,
    /// а моделями (SM_Env_Dirt_Cliff_01..12) — поэтому здесь мы лепим только
    /// пологие валы, без стен.
    ///
    /// Берём середину вилки: перепад 12.5% от игровой площадки (160 м) —
    /// это 20 метров, формы по 12 метров.
    ///
    /// <b>Три слоя шума, а не один.</b> Один слой даёт правильную по числам,
    /// но неживую волну — одинаковые холмы через равные промежутки. Крупный
    /// слой задаёт общий наклон местности, средний лепит холмы, мелкий
    /// снимает «пластилиновость» с их склонов.
    /// </summary>
    public static class TerrainRelief
    {
        /// <summary>Перепад высот, метров. 12.5% от игровой площадки в 160 м.</summary>
        private const float Range = 20f;

        /// <summary>Размер основных форм, метров — от гребня до гребня.</summary>
        private const float Feature = 17f;

        /// <summary>Радиус ровной площадки вокруг начала координат, метров.</summary>
        private const float FlatRadius = 14f;

        /// <summary>Ширина сглаживания на краю ровной площадки, метров.</summary>
        private const float FlatFade = 10f;

        /// <summary>Где стоит доминанта, в долях карты от угла.</summary>
        private const float DomU = 0.34f, DomV = 0.7f;

        /// <summary>
        /// Ширина доминанты в долях карты. Было 0.09 (около 55 м) при весе
        /// 0.42 — и наибольшая крутизна выросла с 37° до 47°. Герой и
        /// навигация берут примерно до 45: часть карты стала стеной, по
        /// которой не подняться. Доминанта нужна как ориентир на горизонте,
        /// а не как преграда, поэтому делаем её шире и ниже — тот же объём,
        /// растянутый на вдвое большее пятно.
        /// </summary>
        private const float DomSigma = 0.16f;

        /// <summary>Насколько доминанта выше прочего, в долях общего перепада.</summary>
        private const float DomWeight = 0.26f;

        /// <summary>Запас низины вокруг водоёма сверх его чаши, доля радиуса.</summary>
        private const float BasinMargin = 1.35f;

        /// <summary>На сколько метров низина ниже окружающей земли.</summary>
        private const float BasinDrop = 1.6f;

        public static void Build()
        {
            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — лепить не на чем.");
                return;
            }

            var data = terrain.terrainData;
            int res = data.heightmapResolution;
            float side = data.size.x;

            // Частота слоя = сколько раз форма укладывается по стороне.
            float fBig = side / (Feature * 3.5f);   // общий наклон местности
            float fMid = side / Feature;            // сами холмы

            // Мелкий слой был задан как `Feature * 0.9` — то есть 15.3 м
            // против 17 у среднего. Два слоя почти одного размера не дают
            // шероховатости, они дублируют друг друга, и вся карта выходит
            // одинаковыми буграми под одеялом. Настоящая мелкая форма —
            // шесть метров, размер куста, а не холма.
            float fFine = side / (Feature * 0.35f); // шероховатость склонов

            // Слой плоскостей. Отдельный крупный шум решает, где местность
            // успокаивается: там гасятся холмы и остаётся почти ровное
            // место. Нужны они не для красоты — на карте не было ни одного
            // ровного куска, и человеку негде встать, а строению негде
            // сесть без повисших углов.
            float fFlat = side / (Feature * 5f);

            // Смещения, чтобы слои не совпадали гребнями: совпавшие гребни
            // дают правильные по числам, но неправдоподобно ровные валы.
            const float ox = 137.3f, oy = 411.7f;

            var h = new float[res, res];

            float min = float.MaxValue, max = float.MinValue;

            for (int y = 0; y < res; y++)
            {
                float v = (float)y / (res - 1);

                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);

                    // Насколько здесь «успокоено»: 0 — обычные холмы,
                    // 1 — ровное место. Порог подобран так, чтобы ровным
                    // выходило около 40% площади: при них средняя крутизна
                    // по карте падает с 15° до примерно 10° сама, и ни один
                    // холм при этом не приходится сплющивать.
                    float plateau = Mathf.PerlinNoise(u * fFlat + ox * 5f, v * fFlat + oy * 5f);
                    // Порог был 0.42–0.62 и дал среднюю крутизну 11.1° при
                    // цели 9–10°. Опускаем нижнюю границу: ровным становится
                    // больше площади, а холмы по-прежнему не трогаем.
                    float calm = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 0.58f, plateau));

                    float big = Mathf.PerlinNoise(u * fBig + ox, v * fBig + oy);
                    float mid = Mathf.PerlinNoise(u * fMid + ox * 2f, v * fMid + oy * 2f);
                    float fine = Mathf.PerlinNoise(u * fFine + ox * 3f, v * fFine + oy * 3f);

                    // На ровных местах глушим холмы и шероховатость, но не
                    // общий наклон: иначе плоскости встают блинами и видно,
                    // что их поставили, а не что местность такая.
                    float n =
                        big * 0.74f +
                        mid * 0.22f * (1f - calm * 0.85f) +
                        fine * 0.04f * (1f - calm * 0.6f);

                    // Доминанта. По кругу шла гряда одинаковых холмов, и
                    // глазу не за что зацепиться — непонятно, где ты стоишь.
                    // Одна возвышенность заметно выше прочих даёт ориентир.
                    float du = u - DomU, dv = v - DomV;
                    float dom = Mathf.Exp(-(du * du + dv * dv) / (2f * DomSigma * DomSigma));

                    n += dom * DomWeight;

                    h[y, x] = n;

                    if (n < min) min = n;
                    if (n > max) max = n;
                }
            }

            // Приводим к заданному перепаду. Высота карты у террейна своя,
            // поэтому считаем долю от неё, а не абсолютные метры.
            float span = Mathf.Max(0.0001f, max - min);
            float amp = Range / data.size.y;

            // Центр площадки в долях карты — там стоит герой.
            float cx = (0f - terrain.transform.position.x) / side;
            float cz = (0f - terrain.transform.position.z) / data.size.z;

            float flatR = FlatRadius / side;
            float fadeR = (FlatRadius + FlatFade) / side;

            // Высота ровной площадки — та, что получилась в её центре.
            int ci = Mathf.Clamp(Mathf.RoundToInt(cz * (res - 1)), 0, res - 1);
            int cj = Mathf.Clamp(Mathf.RoundToInt(cx * (res - 1)), 0, res - 1);
            float centre = (h[ci, cj] - min) / span;

            // Готовим низины под каждый водоём: где центр, докуда ровно,
            // докуда сглаживание и на какой высоте дно.
            int pondCount = SyntyWater.Ponds.Length;

            var basins = new float[pondCount];
            var basinU = new float[pondCount];
            var basinV = new float[pondCount];
            var basinIn = new float[pondCount];
            var basinOut = new float[pondCount];

            for (int p = 0; p < pondCount; p++)
            {
                var pond = SyntyWater.Ponds[p];

                basinU[p] = (pond.Centre.x - terrain.transform.position.x) / side;
                basinV[p] = (pond.Centre.y - terrain.transform.position.z) / data.size.z;
                basinIn[p] = pond.Bowl / side;
                basinOut[p] = pond.Bowl * BasinMargin / side;

                int pi = Mathf.Clamp(Mathf.RoundToInt(basinV[p] * (res - 1)), 0, res - 1);
                int pj = Mathf.Clamp(Mathf.RoundToInt(basinU[p] * (res - 1)), 0, res - 1);

                // Дно низины — высота в центре водоёма минус запас. Range
                // здесь потому, что value живёт в долях перепада, а не в
                // метрах.
                basins[p] = Mathf.Max(0f, (h[pi, pj] - min) / span - BasinDrop / Range);
            }

            for (int y = 0; y < res; y++)
            {
                float v = (float)y / (res - 1);

                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);

                    float value = (h[y, x] - min) / span;

                    // Ровная площадка под героем: постройки и стартовая
                    // сцена на склоне повисают углами, и это видно сразу.
                    float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cz) * (v - cz));

                    if (d < fadeR)
                    {
                        float k = d <= flatR ? 0f
                            : Mathf.SmoothStep(0f, 1f, (d - flatR) / (fadeR - flatR));

                        value = Mathf.Lerp(centre, value, k);
                    }

                    // Низины под водоёмы.
                    //
                    // Раньше рельеф лепился вслепую, а чашу вырезали потом —
                    // и пруд садился на склон холма: вода лежала на боку
                    // возвышенности, дальний угол глади вылезал из-под земли.
                    // Теперь место готовится заранее: вокруг каждого водоёма
                    // земля выполаживается и опускается, и заданию `pond`
                    // остаётся копать чашу в ровном месте.
                    for (int p = 0; p < basins.Length; p++)
                    {
                        float pd = Mathf.Sqrt((u - basinU[p]) * (u - basinU[p]) +
                                              (v - basinV[p]) * (v - basinV[p]));

                        if (pd >= basinOut[p]) continue;

                        float k = pd <= basinIn[p] ? 0f
                            : Mathf.SmoothStep(0f, 1f, (pd - basinIn[p]) / (basinOut[p] - basinIn[p]));

                        value = Mathf.Lerp(basins[p], value, k);
                    }

                    h[y, x] = value * amp;
                }
            }

            data.SetHeights(0, 0, h);
            EditorUtility.SetDirty(data);

            Snap(terrain);
            Report(terrain);
            EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>
        /// Поднять на поверхность всех, кто стоял на плоской земле.
        ///
        /// Рельеф меняет высоту грунта, а объекты остаются на прежней —
        /// и герой оказывается ПОД землёй. Провалившегося персонажа
        /// заказчик увидел первым же кадром, и это правильный симптом:
        /// «вылепил рельеф» и «мир остался рабочим» — разные утверждения.
        ///
        /// Двигаем всех с навигационным агентом (герой и монстры) и все
        /// корневые объекты, кроме служебных: света, камеры, неба и самого
        /// террейна — им высота грунта безразлична.
        /// </summary>
        private static void Snap(Terrain terrain)
        {
            var scene = EditorSceneManager.GetActiveScene();
            int moved = 0;

            string[] skip = { "sun", "camera", "eventsystem", "небо", "terrain",
                              "ground", "wind", "луг", "лес" };

            foreach (var root in scene.GetRootGameObjects())
            {
                string n = root.name.ToLowerInvariant();
                if (skip.Any(s => n.Contains(s))) continue;

                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    bool stands = t.GetComponent<UnityEngine.AI.NavMeshAgent>() != null
                                  || t.parent == null || t.parent == root.transform;

                    if (!stands) continue;

                    var p = t.position;
                    float ground = terrain.SampleHeight(p) + terrain.transform.position.y;

                    // Двигаем только тех, кто оказался НИЖЕ земли или
                    // висит над ней выше полуметра.
                    if (p.y > ground - 0.05f && p.y < ground + 0.5f) continue;

                    t.position = new Vector3(p.x, ground, p.z);
                    EditorUtility.SetDirty(t);
                    moved++;
                }
            }

            Debug.Log("[IsoRPG] Поднято на поверхность объектов: " + moved + ".");
        }

        /// <summary>Плоский лист обратно — если рельеф не понравится.</summary>
        public static void Flatten()
        {
            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null) return;

            var data = terrain.terrainData;
            int res = data.heightmapResolution;

            data.SetHeights(0, 0, new float[res, res]);
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Террейн выровнен в плоский лист.");
        }

        /// <summary>Отчитываемся теми же числами, которыми мерили автора.</summary>
        private static void Report(Terrain terrain)
        {
            var data = terrain.terrainData;
            int res = data.heightmapResolution;
            float[,] h = data.GetHeights(0, 0, res, res);

            float min = 1f, max = 0f;

            foreach (float v in h)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }

            float slopeSum = 0f, slopeMax = 0f;
            int taken = 0, steep = 0;
            var steepAt = Vector2.zero;

            for (int i = 1; i < 64; i++)
            {
                for (int j = 1; j < 64; j++)
                {
                    float s = data.GetSteepness(i / 64f, j / 64f);
                    slopeSum += s;

                    if (s > slopeMax)
                    {
                        slopeMax = s;

                        // Запоминаем ГДЕ, а не только сколько.
                        //
                        // Я дважды подряд менял рельеф, пытаясь угадать
                        // источник крутизны: сперва решил, что виновата
                        // доминанта, сделал её положе — и наибольшая даже
                        // выросла. Одно число «48°» не говорит ничего, а
                        // координата сразу показывает, чей это склон: борт
                        // выкопанной чаши, край карты или сама доминанта.
                        steepAt = new Vector2(
                            terrain.transform.position.x + i / 64f * data.size.x,
                            terrain.transform.position.z + j / 64f * data.size.z);
                    }

                    // Крутых мест считаем долю: одна отвесная точка на
                    // карте — мелочь, а пятая часть площади — это уже
                    // непроходимый мир.
                    if (s > 45f) steep++;

                    taken++;
                }
            }

            Debug.Log("[IsoRPG] Рельеф вылеплен: перепад " +
                      ((max - min) * data.size.y).ToString("0.0") + " м, " +
                      "крутизна средняя " + (slopeSum / taken).ToString("0.0") +
                      "°, наибольшая " + slopeMax.ToString("0.0") +
                      "° в точке (" + steepAt.x.ToString("0") + ", " + steepAt.y.ToString("0") +
                      "). Круче 45° (непроходимо): " +
                      (100f * steep / taken).ToString("0.0") + "% замеров. " +
                      "Ориентир Synty: перепад 10–17% стороны, крутизна 13–15°.");
        }
    }
}
