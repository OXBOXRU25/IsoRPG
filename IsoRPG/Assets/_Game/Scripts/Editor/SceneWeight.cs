using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Из чего складывается вес файла сцены.
    ///
    /// Повод: Arena.unity выросла с 47 до 151 МБ, и GitHub перестал её
    /// принимать — у него потолок 100 МБ на файл. Прежде чем что-то
    /// выносить, надо знать, ЧТО именно весит. Двоичную сцену не прочитать
    /// глазами, поэтому спрашиваем у самого редактора.
    ///
    /// Главный подозреваемый у любой сцены с террейном — сам TerrainData.
    /// Если он не сохранён отдельным ассетом, Unity кладёт его ВНУТРЬ сцены
    /// целиком: карту высот, карты текстур и карты подлеска. Для участка
    /// 600x600 это сотни мегабайт, и по инспектору этого не видно вовсе.
    /// </summary>
    public static class SceneWeight
    {
        public static void Report()
        {
            var scene = EditorSceneManager.GetActiveScene();

            Debug.Log("[IsoRPG] Вес сцены «" + scene.name + "»: разбор.");

            // --- Террейн: самое вероятное место, где прячутся мегабайты ---
            var terrains = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var t in terrains)
            {
                var td = t.terrainData;

                if (td == null)
                {
                    Debug.LogWarning("[IsoRPG] Террейн «" + t.name + "» без данных.");
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(td);

                string where = string.IsNullOrEmpty(path)
                    ? "ВНУТРИ СЦЕНЫ (отдельного файла нет — вот и вес)"
                    : ("файлом: " + path);

                int details = td.detailPrototypes != null ? td.detailPrototypes.Length : 0;
                int layers = td.terrainLayers != null ? td.terrainLayers.Length : 0;
                int trees = td.treeInstances != null ? td.treeInstances.Length : 0;

                Debug.Log("[IsoRPG] Террейн «" + t.name + "»: данные " + where +
                          "; карта высот " + td.heightmapResolution +
                          ", карта текстур " + td.alphamapResolution +
                          " x " + layers + " слоёв" +
                          ", карта подлеска " + td.detailResolution +
                          " x " + details + " видов" +
                          ", деревьев " + trees + ".");
            }

            // --- Объекты сцены: сколько их и кто размножился ---
            var roots = scene.GetRootGameObjects();
            var all = new List<Transform>();

            foreach (var r in roots)
                all.AddRange(r.GetComponentsInChildren<Transform>(true));

            Debug.Log("[IsoRPG] Объектов в сцене: " + all.Count +
                      ", корневых " + roots.Length + ".");

            // Имена вида «Grass (1)», «Grass (2)» схлопываем в «Grass».
            var groups = all
                .GroupBy(t => Strip(t.name))
                .Where(g => g.Count() > 20)
                .OrderByDescending(g => g.Count())
                .Take(12);

            foreach (var g in groups)
                Debug.Log("[IsoRPG]   " + g.Count() + " x «" + g.Key + "»");

            // --- Меши, живущие в сцене, а не в файлах ---
            //
            // Сетка, созданная кодом и не сохранённая ассетом, пишется в
            // сцену целиком. Одна такая на террейн — это десятки мегабайт.
            int orphanMeshes = 0;
            long orphanVerts = 0;

            foreach (var mf in Object.FindObjectsByType<MeshFilter>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var m = mf.sharedMesh;
                if (m == null) continue;
                if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))) continue;

                orphanMeshes++;
                orphanVerts += m.vertexCount;
            }

            if (orphanMeshes > 0)
                Debug.LogWarning("[IsoRPG] Мешей без файла (лежат в сцене): " +
                                 orphanMeshes + ", вершин суммарно " + orphanVerts + ".");
            else
                Debug.Log("[IsoRPG] Мешей без файла нет.");
        }

        /// <summary>
        /// Выключить или включить обратно тяжёлые слои сцены — траву, подстилку,
        /// лес. Для перебора причин падения: выключаем, а не сносим, чтобы
        /// вернуть было одним заданием, а не пересевом.
        /// </summary>
        public static void Heavy(bool active)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            Debug.Log("[IsoRPG] Корневые объекты сцены: " +
                      string.Join(", ", roots.Select(r => "«" + r.name + "»")));

            int touched = 0;

            foreach (var r in roots)
            {
                string n = r.name.ToLowerInvariant();

                bool heavy = n.Contains("meadow") || n.Contains("forest") ||
                             n.Contains("grass") || n.Contains("tree") ||
                             n.Contains("лес") || n.Contains("трав");

                if (!heavy) continue;

                r.SetActive(active);
                EditorUtility.SetDirty(r);
                touched++;

                Debug.Log("[IsoRPG]   «" + r.name + "» -> " +
                          (active ? "включён" : "ВЫКЛЮЧЕН"));
            }

            Debug.Log("[IsoRPG] Тяжёлых слоёв затронуто: " + touched + ".");
            EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>
        /// Оголить сцену: выключить ВСЁ, кроме героя, камеры, ввода и солнца.
        ///
        /// Перебор по одному стоит пересборки на каждый шаг. Быстрее срезать
        /// всё разом: если голая сцена закрывается чисто, виновник среди
        /// выключенного, и дальше он ищется делением пополам — три-четыре
        /// прогона вместо десятка. Если падает и голая — искать надо не в
        /// содержимом сцены, а в герое, звуке или сохранении.
        /// </summary>
        public static void Bare(bool bare)
        {
            string[] keep = { "player", "main camera", "eventsystem", "sun", "navagentrescue" };

            var scene = EditorSceneManager.GetActiveScene();
            int off = 0, on = 0;

            foreach (var r in scene.GetRootGameObjects())
            {
                string n = r.name.ToLowerInvariant();
                bool keepIt = keep.Any(k => n.Contains(k));

                if (keepIt) { r.SetActive(true); on++; continue; }

                r.SetActive(!bare);
                EditorUtility.SetDirty(r);
                off++;

                Debug.Log("[IsoRPG]   «" + r.name + "» -> " +
                          (bare ? "ВЫКЛЮЧЕН" : "включён"));
            }

            Debug.Log("[IsoRPG] Сцена " + (bare ? "оголена" : "восстановлена") +
                      ": оставлено " + on + ", затронуто " + off + ".");

            EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>
        /// Поменять местами стартовую сцену: арена первой или меню первым.
        ///
        /// Опыт на разделение: из меню игра закрывается чисто, из арены
        /// падает. Если запустить арену напрямую и падения не будет, значит
        /// виновато не её содержимое, а то, что переезжает из меню и
        /// переживает смену сцены.
        /// </summary>
        public static void ArenaFirst(bool arenaFirst)
        {
            var scenes = EditorBuildSettings.scenes.ToList();

            scenes.Sort((a, b) =>
            {
                bool aArena = a.path.Contains("Arena");
                bool bArena = b.path.Contains("Arena");
                if (aArena == bArena) return 0;
                if (arenaFirst) return aArena ? -1 : 1;
                return aArena ? 1 : -1;
            });

            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("[IsoRPG] Порядок сцен в сборке: " +
                      string.Join(" -> ", scenes.Select(s =>
                          System.IO.Path.GetFileNameWithoutExtension(s.path))));
        }

        /// <summary>
        /// Снести тяжёлое НАСОВСЕМ, а не выключить.
        ///
        /// Выключенный объект остаётся в файле сцены и всё равно создаётся
        /// при загрузке: гаснет только отрисовка и скрипты. Поэтому опыт
        /// «выключили — проверили» не отвечает на вопрос про вес файла и про
        /// время загрузки, и на вопрос про падение при выходе отвечает лишь
        /// наполовину. Здесь удаляем.
        ///
        /// Возврат — из git: сцена лежит в последнем коммите.
        /// </summary>
        /// <summary>Сносить только мусор (парад, битые ссылки), не трогая мир.</summary>
        public static bool Junk;

        public static void PurgeHeavy()
        {
            var scene = EditorSceneManager.GetActiveScene();

            int before = scene.GetRootGameObjects()
                              .Sum(r => r.GetComponentsInChildren<Transform>(true).Length);

            string[] doomed = Junk
                ? new[] { "парад", "визуал", "missing" }
                : new[] { "meadow", "forest", "grass", "tree", "cozy",
                          "парад", "визуал", "missing" };

            int killed = 0;

            foreach (var r in scene.GetRootGameObjects())
            {
                string n = r.name.ToLowerInvariant();
                if (!doomed.Any(d => n.Contains(d))) continue;

                int inside = r.GetComponentsInChildren<Transform>(true).Length;
                Debug.Log("[IsoRPG]   снесён «" + r.name + "» (объектов внутри " + inside + ")");

                Object.DestroyImmediate(r);
                killed++;
            }

            int after = scene.GetRootGameObjects()
                             .Sum(r => r.GetComponentsInChildren<Transform>(true).Length);

            Debug.Log("[IsoRPG] Снесено корней " + killed +
                      ". Объектов в сцене было " + before + ", стало " + after +
                      " (минус " + (before - after) + ").");

            EditorSceneManager.MarkAllScenesDirty();
        }

        private static string Strip(string name)
        {
            int i = name.IndexOf(" (");
            return i > 0 ? name.Substring(0, i) : name;
        }
    }
}
