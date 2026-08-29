using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Очередь заданий редактору: список действий в файле, который Unity
    /// выполняет сам при первой же перекомпиляции.
    ///
    /// Зачем. Ассистент правит файлы на диске и умеет собирать проект, но
    /// нажать пункт меню снаружи не может: редактор держит проект, а второй
    /// экземпляр упирается в блокировку Library. Из-за этого любая правка
    /// заканчивалась списком «нажми то, потом это» — за вечер таких списков
    /// набирается два десятка, и половина кругов уходит на них.
    ///
    /// Теперь ассистент кладёт рядом файл со строчкой «materials», а Unity,
    /// получив фокус и перекомпилировав скрипты, сам её выполняет и файл
    /// удаляет. Человеку остаётся то, ради чего он и нужен, — посмотреть
    /// результат.
    ///
    /// Файл: `_pending-tasks.txt` в корне проекта, рядом с Assets. По
    /// заданию на строку, пустые строки и строки с # пропускаются.
    ///
    /// Задания намеренно те же, что и пункты меню, — никакой особой логики
    /// здесь нет и быть не должно: очередь только нажимает то, что человек
    /// нажал бы руками.
    /// </summary>
    [InitializeOnLoad]
    public static class PendingTasks
    {
        private static string File_ => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName, "_pending-tasks.txt");

        /// <summary>Как часто заглядывать в файл, секунды.</summary>
        private const double Period = 1.0;

        private static double nextCheck;

        static PendingTasks()
        {
            EditorApplication.update += Tick;
        }

        /// <summary>
        /// Слежение по таймеру, а не разовая проверка при загрузке.
        ///
        /// Первая версия срабатывала только при перекомпиляции — то есть
        /// требовала переключиться в окно Unity. Теперь фокус не нужен
        /// вовсе: положил задание — оно выполнилось.
        /// </summary>
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;

            nextCheck = EditorApplication.timeSinceStartup + Period;

            if (System.IO.File.Exists(File_)) Run();
        }

        /// <summary>
        /// Выполнить очередь прямо сейчас, не дожидаясь тика.
        ///
        /// Нужно пакетному запуску: там нет цикла редактора, и таймер
        /// никогда не сработает.
        /// </summary>
        public static void RunNow()
        {
            if (System.IO.File.Exists(File_)) Run();
            else Debug.Log("[IsoRPG] Очередь пуста.");
        }

        private static void Run()
        {
            if (!System.IO.File.Exists(File_)) return;

            string[] lines;

            try
            {
                lines = System.IO.File.ReadAllLines(File_);
            }
            catch (IOException)
            {
                // Файл ещё пишется — попробуем на следующем заходе.
                return;
            }

            // Удаляем СРАЗУ, до выполнения.
            //
            // Иначе задание, роняющее исключение, останется в очереди и будет
            // падать при каждой перекомпиляции — редактор превратится в
            // мышеловку, из которой не выбраться без ручного удаления файла.
            try { System.IO.File.Delete(File_); } catch (IOException) { }

            // Подтягиваем правки скриптов и ассетов, сделанные снаружи.
            // Без этого задание выполнилось бы старым кодом: Unity замечает
            // изменения на диске только при получении фокуса, а вся затея
            // ради того, чтобы фокус был не нужен.
            AssetDatabase.Refresh();

            foreach (string raw in lines)
            {
                string task = raw.Trim();

                if (task.Length == 0 || task.StartsWith("#")) continue;

                // В Play разрешаем только то, что ничего не меняет.
                //
                // Сборка сцены в режиме Play выглядит успешной и молча
                // откатывается при остановке — это худший вид поломки. А вот
                // диагностика в Play наоборот нужнее всего: смотреть надо
                // ровно то, что человек видит на экране прямо сейчас.
                if (EditorApplication.isPlaying && !AllowedInPlay(task))
                {
                    Debug.LogWarning("[IsoRPG] Задание «" + task + "» пропущено: идёт Play.");
                    continue;
                }

                try
                {
                    Execute(task);
                }
                catch (Exception e)
                {
                    Debug.LogError("[IsoRPG] Задание «" + task + "» упало: " + e.Message);
                }
            }
        }

        /// <summary>Что можно делать, пока игра идёт.</summary>
        private static bool AllowedInPlay(string task)
        {
            string t = task.ToLowerInvariant();

            // Щуп стены в Play особенно ценен: карвящие препятствия режут
            // сетку только на ходу, и в покое их работы не видно вовсе.
            return t == "probe" || t == "stop" || t == "wall" || t.StartsWith("wall ");
        }

        private static void Execute(string task)
        {
            Debug.Log("[IsoRPG] Выполняю задание: " + task);

            // Задания с хвостом-аргументом разбираем до switch: «wall -1 30».
            string lowered = task.ToLowerInvariant();

            if (lowered.StartsWith("wall "))
            {
                WallProbe.Probe(task.Substring(5));
                return;
            }

            switch (lowered)
            {
                case "materials":
                    UrpMaterialFix.Fix();
                    break;

                case "showcase":
                    PackShowcase.Build();
                    break;

                case "showcase-clear":
                    PackShowcase.Clear();
                    break;

                case "dungeon":
                    DungeonProbe.Build();
                    break;

                case "dungeon-clear":
                    DungeonProbe.Clear();
                    break;

                case "incoming":
                    IncomingModel.Place();
                    break;

                case "model":
                    ModelShot.Shoot();
                    break;

                case "border":
                    WorldBorder.Build();
                    break;

                case "border-clear":
                    WorldBorder.Clear();
                    break;

                case "giants":
                    TreeSwap.ReplaceGiants();
                    break;

                case "cast":
                    SyntyCast.Build();
                    break;

                case "cast-clear":
                    SyntyCast.Clear();
                    break;

                case "treenav":
                    TreeNavFix.Fix();
                    break;

                case "nav":
                    NavProbe.RebakeAndReport();
                    break;

                case "weight":
                    SceneWeight.Report();
                    break;

                case "heavy-off":
                    SceneWeight.Heavy(false);
                    break;

                case "heavy-on":
                    SceneWeight.Heavy(true);
                    break;

                case "bare":
                    SceneWeight.Bare(true);
                    break;

                case "bare-off":
                    SceneWeight.Bare(false);
                    break;

                case "purge-heavy":
                    SceneWeight.Junk = false;
                    SceneWeight.PurgeHeavy();
                    break;

                case "purge-junk":
                    SceneWeight.Junk = true;
                    SceneWeight.PurgeHeavy();
                    break;

                // Демо-сцена биома Synty: смотрим, как её собрал автор, ДО
                // того как сеять своё. Числа автора — это готовый ответ на
                // вопрос про плотность, который иначе угадывается кругами.
                case "sky-probe":
                    SyntySky.Report();
                    break;

                // Как купол поставлен у самого автора: размер, высота,
                // материал. Подбирать эти числа наугад — три круга, а у
                // него они уже стоят.
                case "sky-demo":
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        "Assets/PolygonNatureBiomes/PNB_Enchanted_Forest/Scene/Demo_URP.unity",
                        UnityEditor.SceneManagement.OpenSceneMode.Single);
                    foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        string n = t.name.ToLowerInvariant();
                        if (!n.Contains("sky") && !n.Contains("dome")) continue;
                        var rr = t.GetComponent<Renderer>();
                        Debug.Log("[IsoRPG] ДЕМО «" + t.name + "»: позиция " +
                                  t.position + ", масштаб " + t.lossyScale +
                                  ", материал " +
                                  (rr == null || rr.sharedMaterial == null
                                      ? "нет" : rr.sharedMaterial.name +
                                        " / " + rr.sharedMaterial.shader.name));
                    }
                    SyntySky.Report();
                    break;

                case "sky-include":
                    SyntySky.IncludeShader();
                    SyntySky.Report();
                    break;

                case "synty-forest":
                    SyntyForest.Sow();
                    NavBake.Rebake();
                    break;

                case "pnb-sky":
                    PnbAnalyze.Sky();
                    break;

                case "minimap":
                    {
                        var pl = GameObject.Find("Player");
                        if (pl == null)
                            Debug.LogError("[IsoRPG] Героя в сцене нет — миникарту вешать не на кого.");
                        else if (pl.GetComponent<IsoRPG.UI.Minimap>() != null)
                            Debug.Log("[IsoRPG] Миникарта уже на герое.");
                        else
                        {
                            pl.AddComponent<IsoRPG.UI.Minimap>();
                            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                            Debug.Log("[IsoRPG] Миникарта с координатами возвращена на героя.");
                        }
                    }
                    break;

                case "pond-probe":
                    {
                        var terr = UnityEngine.Object.FindObjectsByType<Terrain>(
                            FindObjectsInactive.Exclude, FindObjectsSortMode.None)[0];
                        var c = SyntyWater.Centre;
                        float hc = terr.SampleHeight(new Vector3(c.x, 0f, c.y)) + terr.transform.position.y;
                        float ho = terr.SampleHeight(new Vector3(c.x + 30f, 0f, c.y)) + terr.transform.position.y;
                        Debug.Log("[IsoRPG] ЩУП ПРУДА: высота в центре " + hc.ToString("0.00") +
                                  " м, в 30 м рядом " + ho.ToString("0.00") + " м, разница " +
                                  (ho - hc).ToString("0.00") + " м (чаша должна быть глубже).");
                        var holder = GameObject.Find("Пруд Synty");
                        if (holder == null) { Debug.LogError("[IsoRPG] Держателя пруда в сцене НЕТ."); break; }
                        Debug.Log("[IsoRPG] Пруд: детей " + holder.transform.childCount);
                        foreach (Transform ch in holder.transform)
                        {
                            var rr = ch.GetComponentInChildren<Renderer>();
                            Debug.Log("[IsoRPG]   «" + ch.name + "» поз " + ch.position +
                                      " масштаб " + ch.lossyScale +
                                      (rr == null ? " БЕЗ РЕНДЕРЕРА"
                                       : " материал " + (rr.sharedMaterial == null ? "нет" : rr.sharedMaterial.name +
                                         " / " + rr.sharedMaterial.shader.name) +
                                         " размер " + rr.bounds.size.ToString("0.0")));
                        }
                    }
                    break;

                case "shaders-keep":
                    {
                        string[] keep = { "SyntyStudios/WaterShader", "SyntyStudios/WaterScrolling",
                                          "SyntyStudios/SkyboxUnlit", "SyntyStudios/VegitationShader" };
                        var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
                        var so = new SerializedObject(gs);
                        var list = so.FindProperty("m_AlwaysIncludedShaders");
                        int added = 0;
                        foreach (var name in keep)
                        {
                            var sh = Shader.Find(name);
                            if (sh == null) { Debug.LogWarning("[IsoRPG] Шейдера нет: " + name); continue; }
                            bool has = false;
                            for (int i = 0; i < list.arraySize; i++)
                                if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) has = true;
                            if (has) continue;
                            list.InsertArrayElementAtIndex(list.arraySize);
                            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
                            added++;
                            Debug.Log("[IsoRPG] В обязательные добавлен шейдер: " + name);
                        }
                        so.ApplyModifiedProperties();
                        AssetDatabase.SaveAssets();
                        Debug.Log("[IsoRPG] Обязательных шейдеров теперь " + list.arraySize + ", добавлено " + added + ".");
                    }
                    break;

                case "pond-shot":
                    SceneEye.Shot("pond", new Vector3(38f, 6f, -30f), 34f, 18f, 35f);
                    SceneEye.Shot("pond-near", new Vector3(38f, 5f, -30f), 18f, 12f, 120f);
                    break;

                case "pond":
                    SyntyWater.Build();
                    SyntyMeadow.Sow();
                    NavBake.Rebake();
                    break;

                case "relief":
                    TerrainRelief.Build();
                    SyntyMeadow.Sow();
                    NavBake.Rebake();
                    break;

                case "relief-flat":
                    TerrainRelief.Flatten();
                    NavBake.Rebake();
                    break;

                case "meadow-synty":
                    SyntyMeadow.Sow();
                    NavBake.Rebake();
                    break;

                case "meadow-synty-clear":
                    SyntyMeadow.Clear();
                    break;

                case "pnb-meadow":
                    PnbAnalyze.Meadow();
                    break;

                case "pnb-analyze":
                    PnbAnalyze.Run();
                    break;

                case "meadow-ground":
                    SyntyMeadow.Ground();
                    break;

                case "synty-ground":
                    SyntyForest.Ground();
                    break;

                case "synty-forest-clear":
                    SyntyForest.Clear();
                    break;

                case "synty-sky":
                    SyntySky.List();
                    SyntySky.Apply();
                    break;

                case "pnb-demo":
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        "Assets/PolygonNatureBiomes/PNB_Enchanted_Forest/Scene/Demo_URP.unity",
                        UnityEditor.SceneManagement.OpenSceneMode.Single);
                    SceneWeight.Report();
                    SceneEye.Shot("pnb-demo-wide", new Vector3(0f, 2f, 0f), 60f, 14f, 40f);
                    SceneEye.Shot("pnb-demo-near", new Vector3(0f, 1.5f, 0f), 18f, 10f, 40f);
                    break;

                case "trees":
                    TreeSwap.Swap();
                    break;

                case "village":
                    VillageProbe.Build();
                    break;

                case "village-clear":
                    VillageProbe.Clear();
                    break;

                case "postfx":
                    PostFxBuilder.Build();
                    break;

                case "menu":
                    MainMenuBuilder.Build();
                    break;

                case "arena":
                    ArenaBuilder.Build();
                    break;

                // Замер чужой композиции. Открываем демо-сцену набора,
                // снимаем кадр и вытаскиваем числа, а в конце возвращаемся
                // на арену — иначе пакетный запуск сохранит чужую сцену
                // поверх авторской.
                case "demo":
                {
                    const string demo =
                        "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Scene/Demo.unity";

                    if (System.IO.File.Exists(demo))
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                            demo, UnityEditor.SceneManagement.OpenSceneMode.Single);

                        SceneEye.Audit("Луг, демо-сцена Synty");
                        SceneEye.Shot("demo-meadow", Vector3.zero, 55f);
                        SceneEye.Shot("demo-meadow-close", Vector3.zero, 22f, 14f, 120f);

                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                            ArenaBuilder.ScenePath,
                            UnityEditor.SceneManagement.OpenSceneMode.Single);
                    }
                    else Debug.LogError("[IsoRPG] Нет демо-сцены луга: " + demo);

                    break;
                }

                case "convert":
                    OfficialConvert.Run();
                    break;

                case "textures":
                    MaterialRepair.Fix();
                    break;

                case "grove":
                    MeadowScatter.Grove();
                    break;

                case "meadow":
                    MeadowScatter.Build();
                    break;

                case "meadow-clear":
                    MeadowScatter.Clear();
                    break;

                case "synty":
                    SyntyConvert.Run();
                    break;

                case "tint":
                    MeadowTint.Run();
                    break;

                case "eye":
                    SceneEye.Audit("Наша арена");
                    SceneEye.Shot("arena", Vector3.zero, 45f);
                    break;

                // Опыт с точками на небе: два кадра неба, с лугом и без.
                // Если точки останутся на пустой арене — виноват не луг.
                case "dots":
                {
                    SceneEye.Shot("dots-with-meadow", new Vector3(0f, 12f, 0f), 6f, -28f, 40f);
                    MeadowScatter.Clear();
                    SceneEye.Shot("dots-no-meadow", new Vector3(0f, 12f, 0f), 6f, -28f, 40f);
                    break;
                }

                case "build":
                    GameBuilder.BuildGame();
                    break;

                case "extract":
                    SyntyBuildings.Extract();
                    break;

                case "room":
                    SyntyRoom.Build();
                    break;

                case "room-clear":
                    SyntyRoom.Clear();
                    break;

                case "module":
                    SyntyModule.Measure();
                    break;

                case "probe":
                    SceneProbe.Probe();
                    break;

                case "wall":
                    WallProbe.Probe();
                    break;

                case "anims":
                    AnimAudit.Run();
                    break;

                case "hero":
                    HeroCandidates.Shoot();
                    break;

                case "avatars":
                    AvatarFix.Fix();
                    break;

                case "sky":
                    SkyBuilder.Apply();
                    break;

                case "light":
                    LightProbe.Probe();
                    break;

                // Настройки их террейна и нашего — рядом. Две попытки перенести
                // траву дали белую землю, и обе были догадками. Смотрим, чем
                // террейны отличаются на самом деле.
                case "bf-grass":
                    BruteForceGrass.Apply();
                    break;

                case "bf-grass-off":
                    BruteForceGrass.Remove();
                    break;

                case "cozy-time":
                    CozyDayCycle.Apply();
                    break;

                case "cozy":
                    CozySky.Apply();
                    break;

                case "cozy-off":
                    CozySky.Remove();
                    break;

                case "living":
                    LivingShowcase.Build();
                    break;

                case "living-clear":
                    LivingShowcase.Clear();
                    break;

                case "purge":
                    OldPurge.Purge();
                    break;

                case "synty-chars":
                    SyntyCharacters.Build();
                    break;

                case "thin":
                    TreeSwap.Thin();
                    break;

                case "pick":
                    // Опознание выбранного: три похожих во весь рост.
                    HeroCandidates.Only = new[]
                    {
                        "SM_Env_Big_Tree_01",
                        "SM_Env_Tree_Round_01",
                        "SM_Env_Tree_Round_02",
                        "SM_Env_Tree_Round_03",
                        "SM_Env_Tree_Pine_Cluster_01",
                        "SM_Env_Tree_01",
                        "SM_Env_Tree_02",
                        "SM_Env_Tree_03",
                    };
                    HeroCandidates.Shoot();
                    HeroCandidates.Only = new string[0];
                    break;

                case "sheets":
                    PackContactSheet.Shoot();
                    break;

                // Запуск и остановка игры: снаружи их не сделать никак, а
                // нужны они постоянно — результат правки виден только в
                // движении.
                case "play":
                    if (!EditorApplication.isPlaying)
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                        EditorApplication.EnterPlaymode();
                    }
                    break;

                case "stop":
                    if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                    break;

                case "sandbox":
                    SandboxSceneBuilder.Build();
                    break;

                case "save":
                    UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                    Debug.Log("[IsoRPG] Сцена сохранена.");
                    break;

                // Проба леса: два набора в одном кадре плюс наш рыцарь
                // для масштаба. Снимок делаем тут же — ради него всё и
                // затевалось, а отдельным заданием его легко забыть.
                case "forest":
                    ForestProbe.Build();
                    // Два ракурса. Общий — почти с земли: так читаются силуэты,
                    // а высокий взгляд сплющивает кроны в пятна. Второй —
                    // игровой угол, тот самый, под которым это увидит игрок.
                    // Дистанция считана от ширины рядов (4 дерева с шагом 7 м),
                    // а не взята на глаз: прошлый кадр с 20 м поставил камеру
                    // внутрь кроны.
                    SceneEye.Shot("forest-probe", new Vector3(0f, 6f, 0f), 44f, 12f, 50f);
                    SceneEye.Shot("forest-probe-game", new Vector3(0f, 4f, 0f), 34f, 30f, 50f);

                    // Крупный план стыка: персонаж на фоне каждого набора.
                    // Стилевой разрыв читается здесь, а не между деревьями.
                    SceneEye.Shot("forest-hero-ours",     new Vector3(-3f, 1.5f, -5f), 9f, 8f, 230f);
                    SceneEye.Shot("forest-hero-triforge", new Vector3(-3f, 1.5f,  5f), 9f, 8f, 50f);
                    break;

                // Небо Beautiful Sky. Отдельным именем от "sky": то ставит
                // купол из наборов Synty, это — панорамный скайбокс, и путать
                // их нельзя, они лечат разные болячки.
                // Замена деревьев на TriForge прямо в открытой сцене плюс
                // новое небо. Одним заданием: небо и лес видно только вместе,
                // а два прогона редактора стоят минут.
                // Демо-сцены автора набора: как он сам расставил свой лес.
                // Снимаем по два кадра с каждой — общий и с высоты глаз, —
                // и обязательно возвращаемся на арену, иначе пакетный запуск
                // сохранит чужую сцену поверх авторской.
                // Плотность из демо-сцены автора — числами, а не на глаз.
                // Деревья и трава там нарисованы на террейне, а террейн в
                // пакетном режиме свою растительность не рисует: кадры выходят
                // пустым полем. Зато данные лежат в TerrainData и читаются.
                case "ffe-density":
                {
                    const string dir =
                        "Assets/TriForge Assets/Fantasy Forest Environment/Scenes/URP/";

                    foreach (var file in new[] { "DemoScene_Summer_URP.unity",
                                                 "DemoScene_Winter_URP.unity" })
                    {
                        string full = dir + file;
                        if (!System.IO.File.Exists(full)) continue;

                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                            full, UnityEditor.SceneManagement.OpenSceneMode.Single);

                        foreach (var terrain in UnityEngine.Object.FindObjectsByType<Terrain>(
                                     FindObjectsInactive.Include, FindObjectsSortMode.None))
                        {
                            var data = terrain.terrainData;
                            if (data == null) continue;

                            float area = data.size.x * data.size.z;      // м²
                            float ha = area / 10000f;

                            Debug.Log("[IsoRPG] " + file + " · террейн " +
                                      data.size.x.ToString("0") + " x " +
                                      data.size.z.ToString("0") + " м (" +
                                      ha.ToString("0.00") + " га)");

                            // ---- деревья ------------------------------------
                            var byProto = new Dictionary<int, int>();
                            foreach (var t in data.treeInstances)
                                byProto[t.prototypeIndex] =
                                    byProto.TryGetValue(t.prototypeIndex, out int n) ? n + 1 : 1;

                            Debug.Log("   деревьев всего " + data.treeInstances.Length +
                                      ", это " +
                                      (data.treeInstances.Length / Mathf.Max(area, 1f) * 100f)
                                          .ToString("0.0") + " шт на 100 м²");

                            var protos = data.treePrototypes;
                            foreach (var kv in byProto.OrderByDescending(k => k.Value))
                            {
                                string nm = kv.Key < protos.Length && protos[kv.Key].prefab != null
                                    ? protos[kv.Key].prefab.name : "?";
                                Debug.Log("      " + kv.Value + " x " + nm);
                            }

                            // ---- трава и мелочь -----------------------------
                            int layers = data.detailPrototypes.Length;
                            int res = data.detailResolution;

                            Debug.Log("   слоёв мелочи " + layers +
                                      ", сетка " + res + " x " + res);

                            for (int i = 0; i < layers; i++)
                            {
                                var map = data.GetDetailLayer(0, 0, res, res, i);
                                long sum = 0;
                                foreach (int v in map) sum += v;

                                var p = data.detailPrototypes[i];
                                string nm = p.prototype != null ? p.prototype.name
                                          : p.prototypeTexture != null ? p.prototypeTexture.name
                                          : "слой " + i;

                                Debug.Log("      " + nm + ": " + sum + " шт (" +
                                          (sum / Mathf.Max(area, 1f) * 100f).ToString("0.0") +
                                          " на 100 м²)");
                            }
                        }
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        ArenaBuilder.ScenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Single);
                    break;
                }

                case "ffe-demo":
                {
                    const string dir =
                        "Assets/TriForge Assets/Fantasy Forest Environment/Scenes/URP/";

                    var demos = new (string file, string shot)[]
                    {
                        ("DemoScene_Summer_URP.unity",          "ffe-summer"),
                        ("DemoScene_Autumn_URP.unity",          "ffe-autumn"),
                        ("DemoScene_Autumn_Overcast_URP.unity", "ffe-overcast"),
                        ("DemoScene_Winter_URP.unity",          "ffe-winter"),
                        ("demo_AssetGrid_URP.unity",            "ffe-grid"),
                    };

                    foreach (var (file, shot) in demos)
                    {
                        string full = dir + file;

                        if (!System.IO.File.Exists(full))
                        {
                            Debug.LogWarning("[IsoRPG] Нет демо-сцены " + full);
                            continue;
                        }

                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                            full, UnityEditor.SceneManagement.OpenSceneMode.Single);

                        SceneEye.Audit("Демо набора: " + file);

                        // Наводимся на середину того, что в сцене есть, а не
                        // на ноль координат: чужая сцена собрана вокруг своей
                        // точки, и съёмка из нуля дала пустое зелёное поле.
                        var bounds = new Bounds();
                        bool first = true;

                        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(
                                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        {
                            // Небо и земля не в счёт: их габарит в сотни метров
                            // утащит центр и раздует рамку.
                            if (r.bounds.size.magnitude > 400f) continue;

                            if (first) { bounds = r.bounds; first = false; }
                            else bounds.Encapsulate(r.bounds);
                        }

                        var at = first ? Vector3.zero
                                       : new Vector3(bounds.center.x, bounds.min.y + 3f,
                                                     bounds.center.z);

                        float span = first ? 60f : Mathf.Clamp(bounds.size.magnitude, 30f, 260f);

                        Debug.Log("[IsoRPG] " + file + ": центр " + at +
                                  ", размах " + span.ToString("0"));

                        SceneEye.Shot(shot, at, span * 0.35f, 10f, 35f);
                        SceneEye.Shot(shot + "-far", at, span * 0.75f, 24f, 35f);
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        ArenaBuilder.ScenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Single);
                    break;
                }

                // Открыть нашу сцену. В пакетном режиме её открывает
                // BatchRunner, а у живого редактора открыта та, на которой его
                // оставили, — и задание уходит в чужую сцену молча. Первый же
                // прогон через очередь так покрасил землю в демо-сцене набора.
                // Свет наш и свет автора набора — подряд, одним щупом.
                // Сравнивать по памяти бесполезно: там шесть чисел, и разница
                // в любом из них даёт «темно», а какое именно виновато — из
                // одного отчёта не видно.
                // Масштабы: что задумал автор набора и что у нас.
                // Спор «трава большая или герой маленький» решается только
                // числами: у автора в demo-сцене задана высота травы явно,
                // а у нас она выведена из масштаба, который я подобрал.
                // Демо-сцены шейдера травы Brute Force. Смотрим ДО того, как
                // тащить его к себе: у шейдерной травы свой вид, и решать по
                // описанию нельзя — она может не сойтись с нашим лесом.
                case "grass-demo":
                {
                    const string dir = "Assets/BruteForce-GrassShader/Scenes/URP/";

                    foreach (var (file, shot) in new (string, string)[]
                    {
                        ("01BruteForceGrassURP.unity", "bf-grass-main"),
                        ("04TerrainSetupURP.unity",    "bf-grass-terrain"),
                    })
                    {
                        string full = dir + file;

                        if (!System.IO.File.Exists(full))
                        {
                            Debug.LogWarning("[IsoRPG] Нет сцены " + full);
                            continue;
                        }

                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                            full, UnityEditor.SceneManagement.OpenSceneMode.Single);

                        var bounds = new Bounds();
                        bool first = true;

                        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(
                                     FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        {
                            if (r.bounds.size.magnitude > 400f) continue;
                            if (first) { bounds = r.bounds; first = false; }
                            else bounds.Encapsulate(r.bounds);
                        }

                        var at = first ? Vector3.zero
                                       : new Vector3(bounds.center.x, bounds.min.y + 1.5f,
                                                     bounds.center.z);

                        SceneEye.Shot(shot, at, 14f, 8f, 35f);
                        SceneEye.Shot(shot + "-far", at, 40f, 20f, 35f);
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        ArenaBuilder.ScenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Single);
                    break;
                }

                case "scale":
                {
                    Debug.Log("=== РАЗМЕРЫ У АВТОРА (demo-сцена) ===");

                    const string demo =
                        "Assets/TriForge Assets/Fantasy Forest Environment/Scenes/URP/" +
                        "DemoScene_Summer_URP.unity";

                    if (System.IO.File.Exists(demo))
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                            demo, UnityEditor.SceneManagement.OpenSceneMode.Single);

                        foreach (var t in UnityEngine.Object.FindObjectsByType<Terrain>(
                                     FindObjectsInactive.Include, FindObjectsSortMode.None))
                        {
                            var d = t.terrainData;

                            foreach (var p in d.detailPrototypes)
                                Debug.Log("   мелочь «" +
                                          (p.prototype != null ? p.prototype.name : "?") +
                                          "»: высота " + p.minHeight.ToString("0.00") + "–" +
                                          p.maxHeight.ToString("0.00") + ", ширина " +
                                          p.minWidth.ToString("0.00") + "–" +
                                          p.maxWidth.ToString("0.00"));

                            foreach (var p in d.treePrototypes)
                                Debug.Log("   дерево на террейне: " +
                                          (p.prefab != null ? p.prefab.name : "?"));
                        }
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        ArenaBuilder.ScenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Single);

                    Debug.Log("=== РАЗМЕРЫ У НАС (в сцене, метры) ===");

                    var seen = new HashSet<string>();

                    foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(
                                 FindObjectsInactive.Exclude))
                    {
                        string n = go.name;

                        bool interesting = n.StartsWith("P_FFE_") ||
                                           n.Contains("Chr_") || n.Contains("Player");

                        if (!interesting || !seen.Add(n)) continue;

                        var rs = go.GetComponentsInChildren<Renderer>();
                        if (rs.Length == 0) continue;

                        var b = rs[0].bounds;
                        foreach (var r in rs) b.Encapsulate(r.bounds);

                        Debug.Log("   " + n + ": высота " + b.size.y.ToString("0.00") +
                                  " м, масштаб " + go.transform.localScale.x.ToString("0.00"));
                    }
                    break;
                }

                // Что на самом деле рисует небо. Спор «Enviro работает или нет»
                // решается не глазом: объект в сцене может стоять, а система
                // не запускаться — тогда небо рисует старый скайбокс из
                // RenderSettings, и картинка не меняется вовсе.
                case "enviro-define-off":
                    EnviroRenderFeature.RemoveDefine();
                    break;

                case "enviro-define":
                    EnviroRenderFeature.AddDefine();
                    break;

                case "enviro-feature":
                    EnviroRenderFeature.Add();
                    break;

                case "enviro-feature-off":
                    EnviroRenderFeature.RemoveFeature();
                    break;

                case "enviro-diag":
                {
                    var sky = RenderSettings.skybox;
                    Debug.Log("[IsoRPG] RenderSettings.skybox = " +
                              (sky == null ? "НЕТ" : sky.name + "  (шейдер " +
                               (sky.shader != null ? sky.shader.name : "?") + ")"));

                    var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                        FindObjectsInactive.Include);

                    int found = 0;

                    foreach (var m in all)
                    {
                        if (m == null) continue;
                        var t = m.GetType();
                        if (!t.FullName.StartsWith("Enviro")) continue;

                        found++;
                        Debug.Log("   компонент " + t.Name + " на «" + m.gameObject.name +
                                  "», объект активен " + m.gameObject.activeInHierarchy +
                                  ", компонент включён " + m.enabled);

                        // У менеджера смотрим, назначена ли конфигурация:
                        // без неё система стоит и ничего не делает.
                        foreach (var fld in t.GetFields())
                        {
                            if (!fld.Name.ToLower().Contains("config") &&
                                !fld.Name.ToLower().Contains("profile")) continue;

                            var v = fld.GetValue(m);
                            Debug.Log("      " + fld.Name + " = " +
                                      (v == null ? "НЕ НАЗНАЧЕНО" : v.ToString()));
                        }
                    }

                    if (found == 0)
                        Debug.LogWarning("[IsoRPG] Компонентов Enviro в сцене НЕТ.");

                    break;
                }

                case "enviro":
                    EnviroSetup.Apply();
                    break;

                case "enviro-off":
                    EnviroSetup.Remove();
                    break;

                case "daylight":
                    DaylightSetup.Apply();
                    break;

                case "light-compare":
                {
                    const string demo =
                        "Assets/TriForge Assets/Fantasy Forest Environment/Scenes/URP/" +
                        "DemoScene_Summer_URP.unity";

                    if (System.IO.File.Exists(demo))
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                            demo, UnityEditor.SceneManagement.OpenSceneMode.Single);

                        Debug.Log("=== ЭТАЛОН: демо-сцена автора ===");
                        LightProbe.Probe();
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        ArenaBuilder.ScenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Single);

                    Debug.Log("=== НАША АРЕНА ===");
                    LightProbe.Probe();
                    break;
                }

                case "terrain-diag":
                    TerrainBuilder.Diag();
                    break;

                case "terrain":
                    TerrainBuilder.Build();
                    break;

                // Что реально нарисовано на игроке.
                //
                // «Две модели одна в другой» глазами не разложить: части
                // модульного персонажа выглядят как отдельные модели, а
                // погашенный старый визуал — как их продолжение. Нужен список
                // с путями: по нему сразу видно, чьи меши включены.
                case "hero-num":
                    SyntyHeroSwap.Apply();
                    break;

                case "parade":
                    HeroParade.Build();
                    NavBake.Rebake();
                    break;

                case "parade-off":
                    HeroParade.ClearMenu();
                    break;

                case "one":
                    HeroShowcase.One();
                    SceneEye.Shot("one-hero", new Vector3(0f, 0.95f, 0f), 3.4f, 10f, 50f);
                    break;

                case "rogues":
                    HeroShowcase.Rogues();
                    SceneEye.Shot("rogues", new Vector3(0f, 1f, 0f), 6f, 12f, 50f);
                    break;

                case "heroes":
                    HeroShowcase.Build();
                    SceneEye.Shot("heroes", new Vector3(0f, 1f, 0f), 14f, 22f, 50f);
                    SceneEye.Shot("heroes-close", new Vector3(0f, 1f, 0f), 6f, 12f, 50f);
                    break;

                case "capsule":
                    PlayerCapsule.Apply();
                    break;

                case "open":
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        ArenaBuilder.ScenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Single);
                    Debug.Log("[IsoRPG] Открыта арена.");
                    break;

                case "ground-probe":
                    GroundSkin.Probe();
                    break;

                case "ground-skin":
                    GroundSkin.Apply();
                    break;

                case "floor":
                    MeadowScatter.BuildFloor();
                    break;

                case "floor-clear":
                    MeadowScatter.ClearFloor();
                    break;

                case "forest-diag":
                    ForestDiag.Run();
                    break;

                case "forest-wind":
                    ForestSwap.Wind();
                    break;

                case "forest-straight":
                    ForestSwap.Straighten();
                    break;

                case "forest-swap":
                    ForestSwap.Swap();
                    BeautifulSky.Apply();
                    NavBake.Rebake();
                    break;

                case "sky-bs":
                    BeautifulSky.ApplyMenu();
                    break;

                default:
                    Debug.LogWarning("[IsoRPG] Не знаю задания «" + task + "».");
                    break;
            }
        }
    }
}
