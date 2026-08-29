using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Как автор набора сажает траву: то, что прошлый разбор не мерил.
    ///
    /// Плотность, размах масштаба и утопление с демо-сцены уже сняты и в
    /// нашем посеве стоят. А три вопроса, из-за которых трава «висела»,
    /// «тонула» и «торчала кончиками», остались без ответа:
    ///
    /// 1. КАКОЙ У АВТОРА РЕЛЬЕФ. Куст четыре с половиной метра в поперечнике
    ///    ложится на ровное место и не ложится на склон никак. Если демо
    ///    почти плоское, то авторские масштабы верны только для плоского, и
    ///    наш потолок — не самоуправство, а расплата за холмы.
    /// 2. НАКЛОНЯЕТ ЛИ ОН КУСТЫ по склону, или ставит строго вертикально.
    /// 3. ПЕРЕСЕКАЮТСЯ ЛИ у него кусты. Мы ввели разрежение по догадке, и
    ///    луг после него поредел. Если у автора кусты входят друг в друга —
    ///    разрежение лишнее, а редкость луга объясняется им.
    /// </summary>
    public static class GrassNorm
    {
        private const string Scene =
            "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Scene/Demo.unity";

        [MenuItem("Tools/IsoRPG/Щуп: норматив посева травы", priority = 54)]
        public static void Measure()
        {
            if (!File.Exists(Scene))
            {
                Debug.LogError("[IsoRPG] Демо-сцены луга нет: " + Scene);
                return;
            }

            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);

            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            // --- 1. Рельеф автора -------------------------------------------
            if (terrain == null)
            {
                Debug.LogWarning("[IsoRPG] В демо луга террейна нет — рельеф не измерить.");
            }
            else
            {
                var data = terrain.terrainData;
                var steeps = new List<float>();

                for (int i = 0; i < 60; i++)
                    for (int j = 0; j < 60; j++)
                        steeps.Add(data.GetSteepness(i / 59f, j / 59f));

                steeps.Sort();

                Debug.Log("[IsoRPG] АВТОРСКИЙ РЕЛЬЕФ: участок " +
                          data.size.x.ToString("0") + " м, перепад высот " +
                          data.size.y.ToString("0") + " м, крутизна средняя " +
                          steeps.Average().ToString("0.0") + "°, наибольшая " +
                          steeps.Last().ToString("0.0") + "°, половина карты положе " +
                          steeps[steeps.Count / 2].ToString("0.0") +
                          "°, ровнее 5° — " +
                          (steeps.Count(s => s < 5f) * 100f / steeps.Count).ToString("0") + "%.");
            }

            // --- 2 и 3. Наклон и пересечения --------------------------------
            var bushes = new List<(Vector2 at, float w, float tilt, float steep)>();

            foreach (var go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (go.name.IndexOf("Grass", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // Только корни префабов: внутри лежат узлы уровней детализации.
                if (go.transform.parent != null &&
                    go.transform.parent.name.IndexOf(
                        "Grass", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

                var rs = go.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;

                var box = rs[0].bounds;
                foreach (var r in rs) box.Encapsulate(r.bounds);

                float w = Mathf.Max(box.size.x, box.size.z);
                if (w < 0.05f) continue;

                float tilt = Vector3.Angle(go.transform.up, Vector3.up);

                float steep = 0f;

                if (terrain != null)
                {
                    float u = Mathf.Clamp01((go.transform.position.x - terrain.transform.position.x) /
                                            terrain.terrainData.size.x);
                    float v = Mathf.Clamp01((go.transform.position.z - terrain.transform.position.z) /
                                            terrain.terrainData.size.z);

                    steep = terrain.terrainData.GetSteepness(u, v);
                }

                bushes.Add((new Vector2(go.transform.position.x, go.transform.position.z),
                            w, tilt, steep));
            }

            if (bushes.Count == 0)
            {
                Debug.LogWarning("[IsoRPG] В демо луга кустов травы не нашлось.");
                return;
            }

            var tilts = bushes.Select(b => b.tilt).OrderBy(t => t).ToArray();

            Debug.Log("[IsoRPG] АВТОРСКИЙ НАКЛОН: кустов " + bushes.Count +
                      ", наклонён от вертикали в среднем на " +
                      tilts.Average().ToString("0.0") + "°, наибольший " +
                      tilts.Last().ToString("0.0") +
                      "°, строго вертикальных (меньше 1°) — " +
                      (tilts.Count(t => t < 1f) * 100f / tilts.Length).ToString("0") + "%.");

            // Пересечения: считаем по парам, но не все со всеми — сетка по
            // ячейкам, иначе на десятке тысяч кустов щуп будет думать минуты.
            var grid = new Dictionary<(int, int), List<int>>();
            const float Cell = 6f;

            for (int i = 0; i < bushes.Count; i++)
            {
                var key = ((int)(bushes[i].at.x / Cell), (int)(bushes[i].at.y / Cell));

                if (!grid.TryGetValue(key, out var cell)) grid[key] = cell = new List<int>();
                cell.Add(i);
            }

            int pairs = 0, deep = 0;
            float worst = 0f;

            for (int i = 0; i < bushes.Count; i++)
            {
                var (at, w, _, _) = bushes[i];
                var key = ((int)(at.x / Cell), (int)(at.y / Cell));

                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!grid.TryGetValue((key.Item1 + dx, key.Item2 + dy), out var cell)) continue;

                    foreach (int j in cell)
                    {
                        if (j <= i) continue;

                        float need = (w + bushes[j].w) * 0.5f;
                        float d = Vector2.Distance(at, bushes[j].at);

                        if (d >= need) continue;

                        pairs++;

                        float share = 1f - d / need;
                        if (share > 0.7f) deep++;
                        if (share > worst) worst = share;
                    }
                }
            }

            Debug.Log("[IsoRPG] АВТОРСКИЕ ПЕРЕСЕЧЕНИЯ: пар, входящих друг в друга — " +
                      pairs + " на " + bushes.Count + " кустов (" +
                      (pairs * 1f / bushes.Count).ToString("0.00") +
                      " на куст), глубже 70% — " + deep +
                      ", наибольшее " + (worst * 100f).ToString("0") + "%.");

            // РАЗМЕРЫ ПО ВИДАМ — не края, а распределение.
            //
            // Прошлый разбор снял «масштаб 0.60–2.92» и на этом остановился,
            // а посев разыгрывал число между краями РАВНОМЕРНО. У автора
            // кустов предельного размера единицы, у нас предельным вышел
            // каждый второй — луг встал стеной выше героя. Поэтому берём
            // середину и девятую десятую, и высоту в метрах: с ней сразу
            // видно, во сколько ростов человека вымахал куст.
            var byKind = new Dictionary<string, List<(float scale, float h)>>();

            foreach (var go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // Берём ВСЁ, что автор расставил: камни и плодовые деревья
                // прошлый фильтр пропустил, а размеры мы им задаём из тех же
                // краёв диапазона и с тем же равномерным розыгрышем.
                if (!go.name.StartsWith("SM_Env", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (go.transform.parent != null &&
                    go.transform.parent.name.StartsWith("SM_Env",
                        System.StringComparison.OrdinalIgnoreCase)) continue;

                var rs = go.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;

                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);

                // Имя без хвоста Unity вида «(1)».
                string kind = go.name.Split('(')[0].Trim();

                if (!byKind.TryGetValue(kind, out var list))
                    byKind[kind] = list = new List<(float, float)>();

                list.Add((go.transform.localScale.x, b.size.y));
            }

            foreach (var pair in byKind.OrderByDescending(p => p.Value.Count).Take(40))
            {
                var scales = pair.Value.Select(v => v.scale).OrderBy(s => s).ToArray();
                var heights = pair.Value.Select(v => v.h).OrderBy(h => h).ToArray();

                if (scales.Length < 4) continue;

                Debug.Log("[IsoRPG] РАЗМЕР " + pair.Key + ": штук " + scales.Length +
                          ", масштаб 10% " + scales[scales.Length / 10].ToString("0.00") +
                          ", середина " + scales[scales.Length / 2].ToString("0.00") +
                          ", 90% " + scales[scales.Length * 9 / 10].ToString("0.00") +
                          ", предел " + scales.Last().ToString("0.00") +
                          "; высота середины " + heights[heights.Length / 2].ToString("0.0") +
                          " м, 90% — " + heights[heights.Length * 9 / 10].ToString("0.0") +
                          " м (герой 1.8).");
            }

            // Плотность считаем по ЗАНЯТОЙ площади, а не по всей карте.
            //
            // Автор засевает не весь участок: на демо есть и голые склоны, и
            // вода, и дороги. Поделив на всю карту, получишь красивое число,
            // которое ничего не значит. Считаем клетки 10 × 10 м, в которых
            // трава есть, и делим на них.
            var cells = new HashSet<(int, int)>();

            foreach (var b in bushes)
                cells.Add(((int)Mathf.Floor(b.at.x / 10f), (int)Mathf.Floor(b.at.y / 10f)));

            float occupied = cells.Count * 100f;

            Debug.Log("[IsoRPG] АВТОРСКАЯ ПЛОТНОСТЬ: кустов " + bushes.Count +
                      " на " + cells.Count + " клетках 10×10 м (" +
                      occupied.ToString("0") + " м² засеяно), это " +
                      (bushes.Count * 100f / occupied).ToString("0.0") +
                      " на 100 м² там, где он вообще сеет.");

            // На какой крутизне автор вообще ставит траву.
            if (terrain != null)
            {
                var steeps = bushes.Select(b => b.steep).OrderBy(s => s).ToArray();

                Debug.Log("[IsoRPG] АВТОР СТАВИТ ТРАВУ: крутизна места в среднем " +
                          steeps.Average().ToString("0.0") + "°, наибольшая " +
                          steeps.Last().ToString("0.0") +
                          "°, на склонах круче 15° — " +
                          (steeps.Count(s => s > 15f) * 100f / steeps.Length).ToString("0") + "%.");
            }
        }
    }
}
