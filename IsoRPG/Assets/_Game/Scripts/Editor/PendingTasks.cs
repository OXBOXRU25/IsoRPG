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

                case "music":
                    {
                        var host = GameObject.Find("MusicPlaylist");
                        if (host == null)
                        {
                            host = new GameObject("MusicPlaylist");
                            host.AddComponent<IsoRPG.Audio.MusicPlaylist>();
                            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                            Debug.Log("[IsoRPG] Фоновая музыка посажена в сцену.");
                        }
                        else Debug.Log("[IsoRPG] Фоновая музыка уже в сцене.");
                        var clips = Resources.LoadAll<AudioClip>("Music");
                        Debug.Log("[IsoRPG] Дорожек в Resources/Music: " + (clips == null ? 0 : clips.Length));
                    }
                    break;

                case "hud":
                    {
                        var pl = GameObject.Find("Player");
                        if (pl == null) { Debug.LogError("[IsoRPG] Героя нет."); break; }

                        // Чего не хватает арене из того, что вешал строитель
                        // песочницы. Ставим только отсутствующее: повторный
                        // компонент Unity молча не добавит, а лишний вызов
                        // Setup затёр бы настроенное.
                        int added = 0;
                        if (pl.GetComponent<IsoRPG.UI.SettingsWindow>() == null)
                        { pl.AddComponent<IsoRPG.UI.SettingsWindow>(); added++;
                          Debug.Log("[IsoRPG] Добавлено окно настроек."); }
                        if (pl.GetComponent<IsoRPG.UI.HudBar>() == null)
                        { pl.AddComponent<IsoRPG.UI.HudBar>(); added++;
                          Debug.Log("[IsoRPG] Добавлена панель кнопок (иконка настроек в ней)."); }
                        if (pl.GetComponent<IsoRPG.UI.Tooltip>() == null)
                        { pl.AddComponent<IsoRPG.UI.Tooltip>(); added++;
                          Debug.Log("[IsoRPG] Добавлены подсказки."); }
                        if (pl.GetComponent<IsoRPG.UI.QuestJournal>() == null)
                        { pl.AddComponent<IsoRPG.UI.QuestJournal>(); added++;
                          Debug.Log("[IsoRPG] Добавлен журнал заданий."); }

                        Debug.Log("[IsoRPG] Интерфейс героя: добавлено компонентов " + added + ".");
                        if (added > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                    }
                    break;

                case "audio":
                    {
                        int added = 0;

                        // Звуковой узел: банк звуков и фон места. Без него
                        // Sfx не получает банк и игра беззвучна целиком.
                        var node = GameObject.Find("Audio");
                        if (node == null)
                        {
                            node = new GameObject("Audio");
                            var setup = node.AddComponent<IsoRPG.Audio.AudioSetup>();
                            var bank = SoundBankBuilder.Load();
                            if (bank == null)
                                Debug.LogError("[IsoRPG] Банк звуков не собран — прогони «Собрать банк звуков».");
                            setup.Setup(bank, null);
                            EditorUtility.SetDirty(setup);

                            var amb = new GameObject("Ambience");
                            amb.transform.SetParent(node.transform, false);
                            amb.AddComponent<IsoRPG.Audio.AmbienceLoop>();

                            added++;
                            Debug.Log("[IsoRPG] Звуковой узел собран: банк " +
                                      (bank == null ? "НЕ найден" : "на месте") + ", фон места добавлен.");
                        }
                        else Debug.Log("[IsoRPG] Звуковой узел уже в сцене.");

                        var pl = GameObject.Find("Player");
                        if (pl != null)
                        {
                            if (pl.GetComponent<IsoRPG.Audio.FootstepPlayer>() == null)
                            { pl.AddComponent<IsoRPG.Audio.FootstepPlayer>(); added++;
                              Debug.Log("[IsoRPG] Шаги героя добавлены."); }

                            // Слушатель на герое, а не на камере: в изометрии
                            // камера в двадцати метрах, и всё рядом с героем
                            // доходило бы с громкостью в десятую долю.
                            if (pl.GetComponent<AudioListener>() == null)
                            {
                                foreach (var old in UnityEngine.Object.FindObjectsByType<AudioListener>(
                                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                                    UnityEngine.Object.DestroyImmediate(old);

                                pl.AddComponent<AudioListener>(); added++;
                                Debug.Log("[IsoRPG] Слушатель звука переставлен на героя.");
                            }
                        }

                        Debug.Log("[IsoRPG] Звук: добавлено " + added + " составляющих.");
                        if (added > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                    }
                    break;

                case "terrain-repair":
                    {
                        var t = UnityEngine.Object.FindObjectsByType<Terrain>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();

                        if (t == null) { Debug.LogError("[IsoRPG] Террейна в сцене нет."); break; }

                        if (t.terrainData != null)
                        { Debug.Log("[IsoRPG] Данные террейна на месте, чинить нечего."); break; }

                        // Пересоздаём карту высот теми же числами, что были
                        // замерены щупом до поломки: участок 600x600 м,
                        // карта высот 513, карта текстур и подлеска 1024.
                        var data = new TerrainData
                        {
                            heightmapResolution = 513,
                            alphamapResolution = 1024,
                            baseMapResolution = 1024,
                        };
                        data.SetDetailResolution(1024, 16);
                        data.size = new Vector3(600f, 100f, 600f);

                        const string path = "Assets/_Game/Art/Materials/ArenaTerrain.asset";
                        AssetDatabase.DeleteAsset(path);
                        AssetDatabase.CreateAsset(data, path);
                        AssetDatabase.SaveAssets();

                        t.terrainData = data;

                        var col = t.GetComponent<TerrainCollider>();
                        if (col == null) col = t.gameObject.AddComponent<TerrainCollider>();
                        col.terrainData = data;

                        t.transform.position = new Vector3(-300f, -2.5f, -300f);

                        EditorUtility.SetDirty(t);
                        EditorUtility.SetDirty(col);
                        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                        Debug.Log("[IsoRPG] Карта высот пересоздана: 600x600 м, высота 100, " +
                                  "карта высот 513, карта текстур 1024. Дальше нужен «relief».");
                    }
                    break;

                case "pond-shot":
                    SceneEye.Shot("pond", new Vector3(-46f, 2f, 34f), 70f, 22f, 35f);
                    SceneEye.Shot("pond-near", new Vector3(20f, 5f, -16f), 40f, 20f, 120f);
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

                case "sidekick":
                    HeroShowcase.Sidekicks();
                    SceneEye.Shot("sidekick", new Vector3(0f, 1f, 0f), 6f, 12f, 50f);
                    break;

                case "anims-native":
                    SidekickAnimations.Apply();
                    break;

                case "wolves":
                    WolfPack.Build();
                    break;

                case "hug":
                {
                    // Прижать героя к грунту. Сетка навигации лежит выше
                    // земли, и агент честно ставит персонажа на неё — отсюда
                    // «висит в воздухе».
                    var hugged = GameObject.FindGameObjectWithTag("Player");

                    if (hugged == null)
                    {
                        Debug.LogError("[IsoRPG] Героя в сцене нет — прижимать некого.");
                        break;
                    }

                    if (hugged.GetComponent<IsoRPG.World.GroundHug>() == null)
                        hugged.AddComponent<IsoRPG.World.GroundHug>();

                    // Без этого правка не доживёт до сохранения: сцену
                    // грязной помечает не добавление компонента, а мы сами.
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Герою добавлено прижатие к грунту (GroundHug).");
                    break;
                }

                case "mipcover":
                {
                    // Сохранение покрытия альфы в мип-уровнях.
                    //
                    // У растительности с отсечением по альфе тонкие лепестки
                    // и стебли живут на грани порога. Мип-уровни усредняют
                    // альфу, и одна и та же деталь на разных расстояниях то
                    // проходит порог, то нет — цветок исчезает, когда к нему
                    // подходишь. Галочка пересчитывает уровни так, чтобы доля
                    // пикселей выше порога сохранялась.
                    //
                    // Правим ОДНУ текстуру, а не все разом: правило, выведенное
                    // на одном материале и применённое ко всему набору, даёт
                    // не одну проверку, а тысячу поломок.
                    string[] mipTextures =
                    {
                        "Assets/TriForge Assets/Fantasy Forest Environment/Textures/T_FFE_Grassset01.tga",
                    };

                    foreach (var texPath in mipTextures)
                    {
                        var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;

                        if (imp == null)
                        {
                            Debug.LogError("[IsoRPG] Текстуры нет: " + texPath);
                            continue;
                        }

                        Debug.Log("[IsoRPG] " + System.IO.Path.GetFileName(texPath) +
                                  " было: мипы " + imp.mipmapEnabled +
                                  ", покрытие " + imp.mipMapsPreserveCoverage +
                                  ", порог " + imp.alphaTestReferenceValue.ToString("0.00"));

                        imp.mipMapsPreserveCoverage = true;
                        imp.alphaTestReferenceValue = 0.5f;
                        imp.SaveAndReimport();

                        var check = AssetImporter.GetAtPath(texPath) as TextureImporter;

                        Debug.Log("[IsoRPG] " + System.IO.Path.GetFileName(texPath) +
                                  " стало: покрытие " + check.mipMapsPreserveCoverage +
                                  ", порог " + check.alphaTestReferenceValue.ToString("0.00"));
                    }

                    break;
                }

                case "sit":
                {
                    // Посадка растительности на грунт в рантайме.
                    //
                    // В редакторе габариты после поворота приходят от прежнего
                    // состояния, и посадка считается по устаревшим числам —
                    // куст висит краем при верной формуле. В игре к первому
                    // кадру всё пересчитано, поэтому сажаем там.
                    var meadow = GameObject.Find("Луг Synty");

                    if (meadow == null)
                    {
                        Debug.LogError("[IsoRPG] Держателя «Луг Synty» в сцене нет.");
                        break;
                    }

                    if (meadow.GetComponent<IsoRPG.World.GroundSitter>() == null)
                        meadow.AddComponent<IsoRPG.World.GroundSitter>();

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] На «Луг Synty» повешена посадка на грунт (GroundSitter), " +
                              "растений под ним " + meadow.transform.childCount + ".");
                    break;
                }

                case "grass-shot":
                    GrassShot.Shoot();
                    break;

                case "conform":
                {
                    // Изгиб травы по рельефу: отдать шейдеру карту высот и
                    // включить прижатие ТОЛЬКО траве. Дереву и кусту изгиб
                    // не нужен — у них ствол, который должен стоять прямо.
                    var terr = UnityEngine.Object.FindObjectsByType<Terrain>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                    if (terr.Length == 0)
                    {
                        Debug.LogError("[IsoRPG] Террейна нет.");
                        break;
                    }

                    if (terr[0].GetComponent<IsoRPG.World.TerrainConform>() == null)
                        terr[0].gameObject.AddComponent<IsoRPG.World.TerrainConform>();

                    int on = 0;

                    foreach (var guid in AssetDatabase.FindAssets("t:Material Grass",
                             new[] { "Assets/PolygonNatureBiomes" }))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                        if (mat == null || !mat.HasProperty("_ConformStrength")) continue;

                        mat.SetFloat("_ConformStrength", 1f);
                        EditorUtility.SetDirty(mat);
                        on++;

                        Debug.Log("[IsoRPG] Изгиб включён: " + System.IO.Path.GetFileName(path));
                    }

                    AssetDatabase.SaveAssets();
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Изгиб по рельефу: материалов травы включено " + on + ".");
                    break;
                }

                case "grass-probe":
                {
                    var meadowGo = GameObject.Find("Луг Synty");
                    if (meadowGo == null) { Debug.LogError("[IsoRPG] Луга нет."); break; }

                    for (int i = 0; i < Mathf.Min(4, meadowGo.transform.childCount); i++)
                    {
                        var pl = meadowGo.transform.GetChild(i);
                        var mf = pl.GetComponentInChildren<MeshFilter>();

                        string meshInfo = "меша нет";

                        if (mf != null && mf.sharedMesh != null)
                        {
                            var b = mf.sharedMesh.bounds;
                            meshInfo = "меш «" + mf.sharedMesh.name + "», локальные границы центр " +
                                       b.center.ToString("0.00") + " размер " + b.size.ToString("0.00");
                        }

                        Debug.Log("[IsoRPG] Куст " + i + " «" + pl.name + "»: позиция " +
                                  pl.position.ToString("0.00") +
                                  ", статика " + GameObjectUtility.GetStaticEditorFlags(pl.gameObject) +
                                  ", масштаб " + pl.lossyScale.ToString("0.00") + ". " + meshInfo);
                    }
                    break;
                }

                case "nobatch":
                {
                    // Снять с травы ТОЛЬКО объединение мешей.
                    //
                    // Статический батчинг склеивает кусты в общий меш и
                    // переводит их вершины в мировые координаты. После этого
                    // у куста нет собственной матрицы, и шейдер, считающий
                    // изгиб относительно центра объекта, берёт центр всего
                    // батча — трава уезжает в небо на высоту холма.
                    //
                    // Остальные флаги статики (свет, окклюзия) не трогаем:
                    // они на изгиб не влияют, а пользу дают.
                    var grassHolder = GameObject.Find("Луг Synty");

                    if (grassHolder == null)
                    {
                        Debug.LogError("[IsoRPG] Луга нет.");
                        break;
                    }

                    int cleared = 0;

                    foreach (var tr in grassHolder.GetComponentsInChildren<Transform>(true))
                    {
                        var flags = GameObjectUtility.GetStaticEditorFlags(tr.gameObject);

                        if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;

                        GameObjectUtility.SetStaticEditorFlags(
                            tr.gameObject, flags & ~StaticEditorFlags.BatchingStatic);

                        cleared++;
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Объединение мешей снято с " + cleared +
                              " узлов травы — теперь у каждого куста своя матрица.");
                    break;
                }

                case "grass-gap":
                {
                    // Где траве не хватает изгиба.
                    //
                    // Изгиб ограничен сверху, иначе ошибка расчёта разбрасывает
                    // кусты по небу. Значит куст, под которым перепад земли
                    // больше предела, отработает не полностью и краем повиснет.
                    // Считаем перепад под габаритами каждого куста и печатаем
                    // худшие места по координатам — вместо «поищи сам глазами».
                    var gapHolder = GameObject.Find("Луг Synty");
                    var gapTerr = UnityEngine.Object.FindObjectsByType<Terrain>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                    if (gapHolder == null || gapTerr.Length == 0)
                    {
                        Debug.LogError("[IsoRPG] Нет луга или террейна.");
                        break;
                    }

                    var gapT = gapTerr[0];
                    const float limit = 8f;

                    int over = 0;
                    float worstDrop = 0f;
                    var worstAt = Vector3.zero;
                    var tops = new System.Collections.Generic.List<(float drop, Vector3 at, string name)>();

                    foreach (Transform pl in gapHolder.transform)
                    {
                        var rr = pl.GetComponentsInChildren<Renderer>(true);
                        if (rr.Length == 0) continue;

                        var bb = rr[0].bounds;
                        foreach (var r in rr) bb.Encapsulate(r.bounds);

                        // Перепад земли под пятном куста: разница между самой
                        // высокой и самой низкой точкой по его углам и центру.
                        float hi = float.MinValue, lo = float.MaxValue;

                        for (int sx = -1; sx <= 1; sx++)
                        {
                            for (int sz = -1; sz <= 1; sz++)
                            {
                                var p2 = new Vector3(
                                    bb.center.x + sx * bb.extents.x,
                                    0f,
                                    bb.center.z + sz * bb.extents.z);

                                float hh = gapT.SampleHeight(p2) + gapT.transform.position.y;

                                if (hh > hi) hi = hh;
                                if (hh < lo) lo = hh;
                            }
                        }

                        float drop = hi - lo;

                        tops.Add((drop, pl.position, pl.name));

                        if (drop > limit) over++;

                        if (drop > worstDrop) { worstDrop = drop; worstAt = pl.position; }
                    }

                    tops.Sort((x, y2) => y2.drop.CompareTo(x.drop));

                    Debug.Log("[IsoRPG] Перепад под кустами: всего " + tops.Count +
                              ", больше предела " + limit + " м — " + over +
                              ", наибольший " + worstDrop.ToString("0.0") + " м в точке (" +
                              worstAt.x.ToString("0") + ", " + worstAt.z.ToString("0") + ").");

                    for (int i = 0; i < Mathf.Min(6, tops.Count); i++)
                        Debug.Log("[IsoRPG]   перепад " + tops[i].drop.ToString("0.0") +
                                  " м у «" + tops[i].name + "» в (" +
                                  tops[i].at.x.ToString("0") + ", " + tops[i].at.z.ToString("0") + ")");

                    break;
                }

                case "grass-here":
                {
                    // Что растёт в конкретной точке и включён ли у него изгиб.
                    //
                    // «Где-то стало хорошо, а тут нет» почти всегда значит, что
                    // правка досталась не всем: у растений разных наборов свои
                    // материалы и свои шейдеры.
                    var here = new Vector2(17f, 4f);
                    const float radius = 12f;

                    int found = 0;

                    foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        var p3 = mr.transform.position;

                        if (Vector2.Distance(new Vector2(p3.x, p3.z), here) > radius) continue;
                        if (mr.sharedMaterial == null) continue;

                        var mat = mr.sharedMaterial;

                        bool has = mat.HasProperty("_ConformStrength");
                        float val = has ? mat.GetFloat("_ConformStrength") : -1f;

                        // Полный путь в иерархии: если трава окажется вложена
                        // в дерево или куст, она унаследует их трансформ, и
                        // «висит» будет означать совсем другое.
                        string chain = mr.name;
                        var up = mr.transform.parent;
                        while (up != null) { chain = up.name + " / " + chain; up = up.parent; }

                        Debug.Log("[IsoRPG] Путь: " + chain);

                        Debug.Log("[IsoRPG] В точке: «" + mr.name + "» на (" +
                                  p3.x.ToString("0") + ", " + p3.z.ToString("0") +
                                  "), материал «" + mat.name + "», шейдер «" + mat.shader.name +
                                  "», изгиб " + (has ? val.ToString("0.0") : "СВОЙСТВА НЕТ"));

                        if (++found >= 10) break;
                    }

                    if (found == 0) Debug.Log("[IsoRPG] В точке ничего не нашлось.");
                    break;
                }

                case "grass-overlap":
                {
                    // Не сидят ли кусты друг в друге.
                    //
                    // Догадка Павлона: одна трава наложилась на другую. Если
                    // два широких куста стоят в метре, их листья прошивают
                    // друг друга, и на склоне это читается как «торчит из
                    // воздуха» — хотя каждый сидит на земле правильно.
                    var ovHolder = GameObject.Find("Луг Synty");
                    if (ovHolder == null) { Debug.LogError("[IsoRPG] Луга нет."); break; }

                    var pts = new System.Collections.Generic.List<(Vector3 p, float w, string n)>();

                    foreach (Transform pl in ovHolder.transform)
                    {
                        var rr = pl.GetComponentsInChildren<Renderer>(true);
                        if (rr.Length == 0) continue;

                        var bb = rr[0].bounds;
                        foreach (var r in rr) bb.Encapsulate(r.bounds);

                        pts.Add((pl.position, Mathf.Max(bb.size.x, bb.size.z), pl.name));
                    }

                    int close = 0, deep = 0;
                    float worstOv = 0f;
                    var worstP = Vector3.zero;

                    for (int i = 0; i < pts.Count; i++)
                    {
                        for (int j = i + 1; j < pts.Count; j++)
                        {
                            float d = Vector2.Distance(
                                new Vector2(pts[i].p.x, pts[i].p.z),
                                new Vector2(pts[j].p.x, pts[j].p.z));

                            float sum = (pts[i].w + pts[j].w) * 0.5f;

                            if (d > sum) continue;

                            close++;

                            // Насколько глубоко один влез в другой, в долях.
                            float ov = 1f - d / Mathf.Max(0.01f, sum);

                            if (ov > 0.7f) deep++;

                            if (ov > worstOv) { worstOv = ov; worstP = pts[i].p; }
                        }
                    }

                    Debug.Log("[IsoRPG] Наложение кустов: пар, где габариты пересекаются — " + close +
                              ", из них глубже 70% — " + deep +
                              ". Худшее " + (worstOv * 100f).ToString("0") + "% в точке (" +
                              worstP.x.ToString("0") + ", " + worstP.z.ToString("0") +
                              "). Всего кустов " + pts.Count + ".");
                    break;
                }

                case "nav-hole":
                    NavHoleProbe.Run();
                    break;

                case "grip-probe":
                    GripProbe.Build();
                    SceneEye.Shot("grip", new Vector3(0f, 1f, 0f), 5f, 10f, 40f);
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
