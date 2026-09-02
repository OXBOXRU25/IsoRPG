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

                case "water-fix":
                {
                    // Перевести воду на URP с сохранением вида.
                    //
                    // Родной SyntyStudios/WaterShader написан под встроенный
                    // конвейер и в URP не рисуется ВООБЩЕ — не пурпурный, а
                    // невидимый, поэтому пропажу воды долго принимали за
                    // потерю объектов. Собираем URP-материал: прозрачный,
                    // с текстурой и цветом из родного.
                    var src = AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Materials/Water_Lake_01.mat");

                    var lit = Shader.Find("Universal Render Pipeline/Lit");

                    if (lit == null)
                    {
                        Debug.LogError("[IsoRPG] Шейдера URP/Lit нет.");
                        break;
                    }

                    var wet = new Material(lit) { name = "Water_Lake_URP" };

                    // Переносим то, что несёт вид: текстуру и цвет. Имена
                    // свойств у старых шейдеров свои, поэтому пробуем оба.
                    Texture tex = null;

                    if (src != null)
                    {
                        foreach (var key in new[] { "_MainTex", "_BaseMap", "_Albedo", "_MainTexture" })
                            if (src.HasProperty(key) && src.GetTexture(key) != null)
                            {
                                tex = src.GetTexture(key);
                                break;
                            }
                    }

                    if (tex != null) wet.SetTexture("_BaseMap", tex);

                    Color tint = new Color(0.16f, 0.47f, 0.44f, 0.78f);

                    if (src != null)
                    {
                        foreach (var key in new[] { "_BaseColor", "_Color", "_Tint" })
                            if (src.HasProperty(key))
                            {
                                var c = src.GetColor(key);
                                if (c.maxColorComponent > 0.02f) { tint = c; tint.a = 0.78f; }
                                break;
                            }
                    }

                    wet.SetColor("_BaseColor", tint);

                    // Прозрачность: вода без неё выглядит краской.
                    wet.SetFloat("_Surface", 1f);
                    wet.SetFloat("_Blend", 0f);
                    wet.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    wet.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    wet.SetFloat("_ZWrite", 0f);
                    wet.SetFloat("_Smoothness", 0.92f);
                    wet.SetFloat("_Metallic", 0.1f);
                    wet.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    wet.DisableKeyword("_ALPHATEST_ON");
                    wet.renderQueue = 3000;

                    AssetDatabase.CreateAsset(wet, "Assets/_Game/Art/Materials/Water_Lake_URP.mat");
                    AssetDatabase.SaveAssets();

                    int fixedCount = 0;

                    foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        var mf3 = mr.GetComponent<MeshFilter>();

                        if (mf3 == null || mf3.sharedMesh == null) continue;
                        string meshName = mf3.sharedMesh.name.ToLowerInvariant();

                        if (!meshName.Contains("water")) continue;

                        // «Water» в имени меша — ещё не гладь.
                        //
                        // Мельничное колесо называется WaterWheel, и первый
                        // заход выкрасил его материалом воды: прозрачное
                        // бирюзовое колесо посреди реки. Отбор по одному
                        // слову ловит всё, что рядом с водой стоит, а не
                        // саму воду.
                        if (meshName.Contains("wheel") || meshName.Contains("mill") ||
                            meshName.Contains("well") || meshName.Contains("bucket") ||
                            meshName.Contains("barrel") || meshName.Contains("pump"))
                            continue;

                        mr.sharedMaterial = wet;
                        fixedCount++;
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Вода переведена на URP: объектов " + fixedCount +
                              ", текстура " + (tex != null ? tex.name : "НЕ НАШЛАСЬ") +
                              ", цвет " + tint + ", прозрачность 0.78.");
                    break;
                }

                case "water-mat":
                {
                    // Подменить материал глади на заведомо рабочий.
                    //
                    // Вода в сцене есть, включена, на правильном слое и выше
                    // дна — а её не видно. Остаётся шейдер. Проверяем в лоб:
                    // ставим стандартный материал URP. Появится плоскость —
                    // виноват водный шейдер набора.
                    var lit = Shader.Find("Universal Render Pipeline/Lit");

                    if (lit == null)
                    {
                        Debug.LogError("[IsoRPG] Шейдера URP/Lit нет.");
                        break;
                    }

                    var probe = new Material(lit) { name = "ЩУП_ВОДА" };
                    probe.SetColor("_BaseColor", new Color(0.1f, 0.45f, 0.75f, 1f));

                    AssetDatabase.CreateAsset(probe, "Assets/_Game/Art/Materials/ЩУП_ВОДА.mat");
                    AssetDatabase.SaveAssets();

                    int swapped = 0;

                    foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        var mf2 = mr.GetComponent<MeshFilter>();

                        if (mf2 == null || mf2.sharedMesh == null) continue;
                        if (!mf2.sharedMesh.name.ToLowerInvariant().Contains("water")) continue;

                        mr.sharedMaterial = probe;
                        swapped++;
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                    Debug.Log("[IsoRPG] Материал глади подменён у " + swapped + " объектов.");
                    break;
                }

                case "world-check":
                {
                    // Контрольный список целости мира.
                    //
                    // Ставить ПОСЛЕДНЕЙ строкой в каждый прогон. Журнал
                    // печатает тот же код, который делал работу, и говорит
                    // лишь то, что код дошёл до строки. Этот щуп ЧИТАЕТ
                    // состояние сцены и потому ловит потерю в том же
                    // прогоне, а не через час на кадре заказчика.
                    var wcTerr = UnityEngine.Object.FindObjectsByType<Terrain>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);

                    int layers = wcTerr.Length > 0 ? wcTerr[0].terrainData.terrainLayers.Length : 0;

                    var wcPlayer = GameObject.FindGameObjectWithTag("Player");
                    var wcMeadow = GameObject.Find("Луг Synty");

                    int plants = wcMeadow != null ? wcMeadow.transform.childCount : 0;

                    // Вода: ищем по материалу и по имени модели набора, а не
                    // по русскому имени объекта — оно может быть любым.
                    int waters = 0, lillies = 0, wolves = 0;

                    foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        // Имя ОБЪЕКТА в сцене может быть любым, а имя меша
                        // приходит из файла модели и врать не станет.
                        var mf = mr.GetComponent<MeshFilter>();
                        var meshName = mf != null && mf.sharedMesh != null
                            ? mf.sharedMesh.name.ToLowerInvariant()
                            : mr.name.ToLowerInvariant();

                        if (meshName.Contains("water"))
                        {
                            waters++;

                            // Печатаем каждый водный объект подробно: «есть в
                            // сцене» и «видно в игре» — разные утверждения.
                            // Выключенный объект или потерянный материал
                            // выглядят одинаково: воды нет.
                            var mat = mr.sharedMaterial;

                            Debug.Log("[IsoRPG] водный объект «" + mr.name + "» на " +
                                      mr.transform.position.ToString("0.0") +
                                      ", включён " + mr.gameObject.activeInHierarchy +
                                      ", рендерер " + mr.enabled +
                                      ", материал " + (mat != null ? mat.name : "НЕТ") +
                                      ", шейдер " + (mat != null && mat.shader != null ? mat.shader.name : "НЕТ") +
                                      ", масштаб " + mr.transform.lossyScale.ToString("0.00"));

                            // Главная проверка: где земля относительно глади.
                            // Если грунт выше воды, гладь просто закопана —
                            // и выглядит это как «водоёмов нет».
                            if (wcTerr.Length > 0)
                            {
                                float ground = wcTerr[0].SampleHeight(mr.transform.position) +
                                               wcTerr[0].transform.position.y;

                                float diff = ground - mr.transform.position.y;

                                Debug.Log("[IsoRPG]    земля в центре " + ground.ToString("0.00") +
                                          " м, гладь " + mr.transform.position.y.ToString("0.00") +
                                          " м, " + (diff > 0f
                                              ? "ЗЕМЛЯ НАД ВОДОЙ на " + diff.ToString("0.00") + " м — гладь закопана"
                                              : "вода выше дна на " + (-diff).ToString("0.00") + " м"));

                                var camMain = Camera.main;

                                Debug.Log("[IsoRPG]    слой воды " + mr.gameObject.layer +
                                          " («" + LayerMask.LayerToName(mr.gameObject.layer) + "»)" +
                                          ", камера рисует этот слой: " +
                                          (camMain != null
                                              ? ((camMain.cullingMask & (1 << mr.gameObject.layer)) != 0).ToString()
                                              : "камеры нет") +
                                          ", очередь материала " +
                                          (mr.sharedMaterial != null ? mr.sharedMaterial.renderQueue.ToString() : "?"));
                            }
                        }
                        else if (meshName.Contains("lill")) lillies++;
                    }

                    foreach (var ag in UnityEngine.Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (ag.name.ToLowerInvariant().Contains("wolf") ||
                            ag.name.ToLowerInvariant().Contains("волк")) wolves++;

                    bool nav = UnityEngine.AI.NavMesh.CalculateTriangulation().indices.Length > 0;

                    string line = "[IsoRPG] МИР: террейн " + wcTerr.Length +
                                  ", слоёв земли " + layers +
                                  ", воды " + waters +
                                  ", кувшинок " + lillies +
                                  ", растений " + plants +
                                  ", волков " + wolves +
                                  ", герой " + (wcPlayer != null ? "есть" : "НЕТ") +
                                  ", навигация " + (nav ? "есть" : "НЕТ") + ".";

                    // Ругаемся, если пропало то, без чего мир неполон.
                    if (wcTerr.Length == 0 || layers == 0 || waters == 0 ||
                        plants == 0 || wcPlayer == null || !nav)
                        Debug.LogError(line + " ЧЕГО-ТО НЕ ХВАТАЕТ.");
                    else
                        Debug.Log(line);

                    // Боевая комплектность. Заведено 31.08.2026, после того
                    // как одну и ту же болезнь пришлось лечить дважды: сперва
                    // у героя не оказалось компонента ввода (клик не выделял
                    // никого), потом у мобов — обработчика смерти (убитый моб
                    // продолжал бить). Оба раза сцена собиралась без единой
                    // ошибки, и оба раза дефект нашёлся только в игре.
                    //
                    // Причина общая: список «из чего состоит боец» жил в
                    // строителе песочницы, а новые задания (`wolves`, `boars`,
                    // `npc`) писались отдельно и повторяли его по памяти —
                    // каждое брало то, что нужно прямо сейчас. Проверка ниже
                    // ловит такую недостачу в том же прогоне.
                    int wcMobs = 0, wcNoDeath = 0;

                    foreach (var t in UnityEngine.Object.FindObjectsByType<IsoRPG.Combat.Targetable>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (t.Faction == IsoRPG.Combat.Faction.Player) continue;
                        if (t.GetComponent<IsoRPG.Combat.Health>() == null) continue;

                        wcMobs++;
                        if (t.GetComponent<IsoRPG.Combat.DeathHandler>() == null) wcNoDeath++;
                    }

                    bool wcRouter = wcPlayer != null &&
                                    wcPlayer.GetComponent<IsoRPG.Player.PlayerInputRouter>() != null;

                    bool wcDeathScreen = wcPlayer != null &&
                                         wcPlayer.GetComponent<IsoRPG.UI.DeathScreen>() != null;

                    string battle = "[IsoRPG] БОЙ: существ " + wcMobs +
                                    ", без обработки смерти " + wcNoDeath +
                                    ", клик по миру у героя " + (wcRouter ? "есть" : "НЕТ") +
                                    ", экран смерти " + (wcDeathScreen ? "есть" : "НЕТ") + ".";

                    if (wcNoDeath > 0 || !wcRouter || !wcDeathScreen)
                        Debug.LogError(battle + " ПРОГОНИ «player-kit» И «mob-kit».");
                    else
                        Debug.Log(battle);

                    break;
                }

                case "water-check":
                {
                    int found = 0;

                    foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        var n = mr.name.ToLowerInvariant();

                        if (!n.Contains("water") && !n.Contains("pond") &&
                            !n.Contains("lill") && !n.Contains("вод")) continue;

                        Debug.Log("[IsoRPG] Вода: «" + mr.name + "» на " +
                                  mr.transform.position.ToString("0.0") +
                                  ", включён " + mr.gameObject.activeInHierarchy);
                        if (++found >= 8) break;
                    }

                    if (found == 0) Debug.LogError("[IsoRPG] ВОДЫ В СЦЕНЕ НЕТ ВООБЩЕ.");
                    else Debug.Log("[IsoRPG] Водных объектов найдено (первые): " + found);

                    break;
                }

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

                case "phys-probe":
                    PhysicsProbe.Report();
                    break;

                case "phys-census":
                    PhysicsProbe.Census();
                    break;

                case "hero-physics":
                    HeroPhysics.On();
                    break;

                case "hero-physics-off":
                    HeroPhysics.Off();
                    break;

                case "creature-probe":
                    CreatureProbe.Report();
                    break;

                case "near-probe":
                    CreatureProbe.Near();
                    break;

                case "hero-probe":
                    CreatureProbe.Hero();
                    break;

                case "no-avoidance":
                    NoAvoidance.Apply();
                    break;

                case "slot-icons":
                    SpriteImport.Apply();
                    break;

                case "anim-probe":
                    CreatureProbe.Animators();
                    break;

                case "npc-probe":
                    CreatureProbe.Npc();
                    break;

                case "sounds":
                    SoundBankBuilder.Build();
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

                // Весь игровой набор герою: выбор цели, бой, сумка,
                // характеристики, квесты, окна. Задание «hud» доносило только
                // интерфейс — а клик по миру не читал на арене НИКТО, отсюда
                // «не выделяются ни мобы, ни NPC».
                case "player-kit":
                    PlayerKit.Apply();
                    break;

                // Смерть, оглушение, полоска и возрождение мобам. Без этого
                // убитый моб продолжает бить: обнулить здоровье некому мало,
                // выключать бой и ИИ у трупа умеет только DeathHandler.
                case "mob-kit":
                    MobKit.Apply();
                    break;

                // Огонь и дым в очаге лагеря. Снимается заданием `campfire-off`.
                case "campfire":
                    CampFire.Apply();
                    break;

                case "campfire-off":
                    CampFire.Clear();
                    break;

                // Варианты пламени в ряд у лагеря: выбрать подходящее одним
                // кадром, а не пятью сборками подряд. СНИМАТЬ СРАЗУ ПОСЛЕ
                // кадра — щуп в боевом мире читается заказчиком как баг, и
                // читается справедливо.
                case "fire-row":
                    CampFire.Row();
                    break;

                // Эффекты на URP: вернуть частицам цвет и прозрачность.
                case "fx-urp":
                    FxMaterials.Apply();
                    break;

                // Витрина лошадей на выбор. В отдельной сцене, которая не
                // сохраняется: в мир заказчика щупы не ставим.
                case "horses-show":
                    HorseShow.Build();
                    break;

                // Десять мастей лошади Synty — выбирать масть можно только
                // увидев их все.
                case "horse-skins":
                    HorseShow.SyntySkins();
                    break;

                // Оживление боя: серия ударов, вздрагивание, жесты
                // способностей, уклонение — герою и зверям.
                case "combat-anims":
                    HeroCombatAnimations.Build();
                    HeroCombatAnimations.Beasts();
                    break;

                // Способности заново плюс книга героя: без второго шага новый
                // приём лежит ассетом, но на панель не попадает.
                case "abilities":
                    RogueAbilitiesBuilder.Build();
                    PlayerKit.Apply();
                    break;

                // Предметы, добыча, квесты и справочник — одним ходом.
                // Порядок обязателен: квест ссылается на предмет, справочник
                // собирается по обоим, и прогон по частям даёт квест с пустой
                // ссылкой — молча, без единой ошибки.
                case "items":
                    ItemsBuilder.Build();

                    // Иконки: сперва разметить PNG как спрайты, потом
                    // связать. Текстура, не размеченная спрайтом, не
                    // находится по пути МОЛЧА — предмет остаётся с пустым
                    // квадратом, и выглядит это как «иконку не нарисовали».
                    IconBinder.PrepareSprites("Assets/_Game/Art/UI/Icons/Items");
                    IconBinder.PrepareSprites("Assets/_Game/Art/UI/Icons/Abilities");
                    IconBinder.Bind();

                    QuestBuilder.Build();
                    DatabaseBuilder.Build();
                    break;

                // Кадр лагеря из редактора: дозорный, лошадь, костёр.
                // Частицы огня в редакторе не проигрываются — пламя на таком
                // кадре не появится, его смотреть только в игре.
                case "camp-shot":
                    SceneEye.Shot("camp", new Vector3(52.0f, 0.8f, -28.0f), 11f, 22f, 205f);
                    break;

                // Кто из живности стоит не на земле и кого не видно.
                // Заказчик 01.09.2026: «мелкие кабаны провалились под землю и
                // бьют оттуда», причём он их НЕ убивал — значит дело не в
                // погружении трупа, а в самой расстановке.
                case "mob-height":
                {
                    var mhTerrain = UnityEngine.Object.FindObjectsByType<Terrain>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

                    if (mhTerrain == null) { Debug.LogError("[IsoRPG] Террейна нет."); break; }

                    int under = 0, hidden = 0, total = 0;

                    foreach (var t in UnityEngine.Object.FindObjectsByType<IsoRPG.Combat.Targetable>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (t.Faction == IsoRPG.Combat.Faction.Player) continue;

                        total++;

                        Vector3 at = t.transform.position;
                        float ground = mhTerrain.SampleHeight(at) + mhTerrain.transform.position.y;
                        float diff = at.y - ground;

                        // Видно ли вообще: выключенный объект или выключенные
                        // рендереры дают «невидимого противника, который бьёт».
                        var parts = t.GetComponentsInChildren<Renderer>(true);
                        int on = 0;
                        foreach (var r in parts) if (r.enabled && r.gameObject.activeInHierarchy) on++;

                        bool invisible = !t.gameObject.activeInHierarchy || on == 0;
                        if (invisible) hidden++;

                        if (diff < -0.5f) under++;

                        if (diff < -0.5f || diff > 2f || invisible)
                            Debug.Log("[IsoRPG]   «" + t.DisplayName + "» (" + t.name + ") в " +
                                      at.ToString("0.0") + ", земля " + ground.ToString("0.00") +
                                      ", отклонение " + diff.ToString("0.00") + " м" +
                                      ", видимых частей " + on + " из " + parts.Length +
                                      (t.gameObject.activeInHierarchy ? "" : ", ОБЪЕКТ ВЫКЛЮЧЕН"));
                    }

                    Debug.Log("[IsoRPG] Живности " + total + ", под землёй " + under +
                              ", невидимых " + hidden + ".");

                    if (under > 0 || hidden > 0)
                        Debug.LogError("[IsoRPG] Есть живность не на месте — см. строки выше.");

                    break;
                }

                // Вторая лошадь — у лагеря дозорного, модель Synty, масть 3.
                case "camp-horse":
                    CampHorse.Build();
                    MobKit.Apply();
                    break;

                // Щуп очага: где в сцене костровище и котелок. Замер до
                // правки — ставить огонь «примерно туда» значит потом ловить
                // пламя, висящее в метре от дров.
                case "fire-probe":
                {
                    var npcSpot = Vector3.zero;

                    foreach (var t in UnityEngine.Object.FindObjectsByType<IsoRPG.Combat.Targetable>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (t.DisplayName == "Дозорный") npcSpot = t.transform.position;

                    Debug.Log("[IsoRPG] Дозорный стоит в " + npcSpot.ToString("0.0"));

                    int found = 0;

                    foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (mf.sharedMesh == null) continue;

                        string mesh = mf.sharedMesh.name.ToLowerInvariant();

                        bool interesting = mesh.Contains("fire") || mesh.Contains("camp") ||
                                           mesh.Contains("pot") || mesh.Contains("cauldron") ||
                                           mesh.Contains("log") || mesh.Contains("tripod") ||
                                           mesh.Contains("tent");

                        if (!interesting) continue;

                        float away = npcSpot == Vector3.zero
                            ? 0f
                            : Vector3.Distance(mf.transform.position, npcSpot);

                        // Только лагерь, а не весь мир: дрова и палатки
                        // разбросаны по всей карте автора.
                        if (npcSpot != Vector3.zero && away > 25f) continue;

                        Debug.Log("[IsoRPG]   «" + mf.gameObject.name + "» меш «" + mf.sharedMesh.name +
                                  "» в " + mf.transform.position.ToString("0.00") +
                                  ", до Дозорного " + away.ToString("0.0") + " м" +
                                  ", габарит " + mf.sharedMesh.bounds.size.ToString("0.00"));
                        found++;
                    }

                    Debug.Log("[IsoRPG] Кандидатов у лагеря: " + found + ".");
                    break;
                }

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

                case "nav-bake":
                    // Только перепечь сетку. Нужна отдельно, чтобы дойти до
                    // приставленной сцены автора: без неё щелчок туда не
                    // работает — агент ходит по сетке, а не по земле.
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

                    // Полный набор существу сразу: смерть, оглушение, полоска,
                    // возрождение. Без этого убитый моб продолжает бить.
                    MobKit.Apply();
                    break;

                case "boars":
                    BoarPack.Build();

                    // Полный набор существу сразу: смерть, оглушение, полоска,
                    // возрождение. Без этого убитый моб продолжает бить.
                    MobKit.Apply();
                    break;

                case "topdown-shot":
                    SceneEye.TopDown("topdown", Vector3.zero, 215f);
                    break;

                case "topdown-marked":
                {
                    // Кадр сверху с яркими метками на известных прудах —
                    // чтобы читать зоны, нарисованные поверх снимка, по
                    // настоящим координатам, а не на глаз.
                    var lit = Shader.Find("Universal Render Pipeline/Lit");
                    var pts = new (Vector2 pos, Color color, string label)[]
                    {
                        (new Vector2( 20f, -16f), Color.red,     "P1"),
                        (new Vector2(-46f,  34f), Color.yellow,  "P2"),
                        (new Vector2( 60f,  58f), Color.cyan,    "P3"),
                        (new Vector2(-62f, -66f), Color.white,  "P4"),
                    };

                    var marks = new GameObject("МЕТКИ_ПРУДОВ");

                    foreach (var p in pts)
                    {
                        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        pillar.name = "Метка " + p.label;
                        pillar.transform.SetParent(marks.transform, false);
                        pillar.transform.position = new Vector3(p.pos.x, 30f, p.pos.y);
                        pillar.transform.localScale = new Vector3(6f, 60f, 6f);

                        var mr = pillar.GetComponent<MeshRenderer>();
                        if (lit != null)
                        {
                            var m = new Material(lit);
                            m.SetColor("_BaseColor", p.color);
                            m.SetFloat("_Smoothness", 0f);
                            mr.sharedMaterial = m;
                        }
                    }

                    SceneEye.TopDown("topdown-marked", Vector3.zero, 215f);

                    UnityEngine.Object.DestroyImmediate(marks);   // сцену меткам не пачкаем

                    Debug.Log("[IsoRPG] Кадр с метками прудов готов, метки из сцены убраны.");
                    break;
                }

                case "mushroom-boss":
                    MushroomBossPack.Build();
                    break;

                case "mipcover-veg":
                    MipCoverVeg.Apply();
                    break;

                case "voice-kit":
                    VoiceKit.Apply();
                    break;

                case "wind-kit":
                    WindKit.Apply();
                    break;

                case "lod-compare":
                    LodCompare.Run();
                    break;

                case "lod-keep":
                    LodKeep.Apply();
                    break;

                case "idle-kit":
                    IdleKit.Apply();
                    break;

                // Полные наборы анимаций кабанам и волкам — без пересборки
                // стай: пересборка вернула бы им уступание дороги, которое
                // мы сняли заданием no-avoidance.
                case "beast-anims":
                    BeastAnims.Apply();
                    break;

                // Щуп: что за анимации у существ на сцене и сколько их.
                case "beast-probe":
                    BeastProbe.Run();
                    break;

                case "anim-log":
                    AnimLog.On();
                    break;

                case "anim-log-off":
                    AnimLog.Off();
                    break;

                case "mob-anim-probe":
                    MobAnimProbe.Run();
                    break;

                case "boar-boss":
                    BoarBossPack.Build();

                    // Полный набор существу сразу: смерть, оглушение, полоска,
                    // возрождение. Без этого убитый моб продолжает бить.
                    MobKit.Apply();
                    break;

                case "npc":
                    NpcPack.Build();

                    // Полный набор существу сразу: смерть, оглушение, полоска,
                    // возрождение. Без этого убитый моб продолжает бить.
                    MobKit.Apply();
                    break;

                case "tent-obstacle":
                {
                    // Кабаны гоняются за героем и забираются НА палатку —
                    // навигация считает её ткань такой же проходимой
                    // поверхностью, как землю, потому что коллайдер палатки
                    // запёкся в сетку как обычная статика. Переставать
                    // двигать точки кабанов бесполезно: дело не в месте
                    // спавна, а в погоне — агент идёт к герою напрямую,
                    // где бы тот ни встал. Правильный фикс — вырезать
                    // палатку из сетки препятствием, чтобы путь просто
                    // обходил её.
                    int marked = 0;

                    foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (mf.sharedMesh == null) continue;
                        if (mf.sharedMesh.name.IndexOf("tent", System.StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        var go = mf.gameObject;

                        if (go.GetComponent<UnityEngine.AI.NavMeshObstacle>() == null)
                        {
                            var obstacle = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                            obstacle.carving = true;
                            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                        }

                        EditorUtility.SetDirty(go);
                        marked++;

                        Debug.Log("[IsoRPG] Палатка помечена препятствием: «" + go.name + "» на " +
                                  go.transform.position.ToString("0.0") + ".");
                    }

                    if (marked == 0)
                    {
                        Debug.LogWarning("[IsoRPG] Палатки по имени меша не нашлось вовсе.");
                    }
                    else
                    {
                        NavBake.Rebake();
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                    break;
                }

                case "hero-spawn":
                {
                    // Точка спавна — по координатам Павлона с карты сверху.
                    // По имени «Player», а не по компоненту ввода: тот
                    // висит на герое только в игре, а в редакторе, где
                    // работает это задание, его ещё нет. Тот же приём, что
                    // и в SyntyHeroSwap.
                    var heroGo = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                                     .FirstOrDefault(g => g.name == "Player");

                    if (heroGo == null)
                    {
                        Debug.LogError("[IsoRPG] Героя в сцене нет — переносить некого.");
                        break;
                    }

                    var terrain = UnityEngine.Object.FindObjectsByType<Terrain>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

                    if (terrain == null)
                    {
                        Debug.LogError("[IsoRPG] Террейна нет — переносить некуда.");
                        break;
                    }

                    Vector2 spot = new Vector2(40f, 28f);

                    float y = terrain.SampleHeight(new Vector3(spot.x, 0f, spot.y)) +
                              terrain.transform.position.y;

                    // Не agent.Warp(): в режиме редактора (не Play) навигация
                    // не крутится, и Warp молча не двигает трансформ — герой
                    // так и остался на старом месте, хотя лог рапортовал
                    // успех. Ставим transform.position напрямую, как у
                    // волков/кабанов/лошади.
                    Vector3 to = new Vector3(spot.x, y, spot.y);

                    if (UnityEngine.AI.NavMesh.SamplePosition(to, out var hit, 8f,
                            UnityEngine.AI.NavMesh.AllAreas))
                    {
                        to = hit.position;
                    }

                    heroGo.transform.position = to;

                    // Разворот на вид с пруда и лошади сразу в кадре —
                    // Павлон прислал такой скриншот. Угол — среднее между
                    // направлением на пруд (20,-16) и на лошадь (36,30) от
                    // точки спавна, а не подобран на глаз.
                    heroGo.transform.rotation = Quaternion.Euler(0f, 217f, 0f);

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Герой перенесён к точке 3 (лошадь): " +
                              heroGo.transform.position.ToString("0.0") + ", развёрнут на 217°.");
                    break;
                }

                case "horse":
                    // Обеих лошадей теперь ставит CampHorse: прежний отдельный
                    // сборщик HorsePack давал тот же набор компонентов, но
                    // расходился в мелочах, и его лошадь то отталкивалась, то
                    // вставала без контроллера анимации.
                    CampHorse.Build();

                    // Полный набор существу сразу: смерть, оглушение, полоска,
                    // возрождение. Без этого убитый моб продолжает бить.
                    MobKit.Apply();
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

                case "pine-probe":
                    GrassShot.Pines();
                    break;

                case "giants-shot":
                    GrassShot.Giants();
                    break;

                case "enchanted-demo":
                    GrassShot.EnchantedDemo();
                    break;

                case "meadow-demo":
                    GrassShot.MeadowDemo();
                    break;

                case "big-trees":
                    BigTrees.Plant();
                    NavBake.Rebake();
                    break;

                case "trees-shot":
                    BigTrees.Shots();
                    break;

                case "tree-norm":
                    TreeNorm.Measure();
                    break;

                case "grass-norm":
                    GrassNorm.Measure();
                    break;

                case "veg-reimport":
                {
                    // Пересобрать шейдер растительности начисто.
                    //
                    // Наша правка меняет постоянный буфер материала, а
                    // скомпилированные варианты Unity держит в кеше. Старый
                    // вариант со старой раскладкой поверх новых данных даёт
                    // ровно то, что мы видели: плоские кислотные цвета,
                    // разные от прогона к прогону.
                    const string veg = "Assets/PolygonNatureBiomes/PNB_Core/" +
                                       "Shaders/SyntyStudios_VegitationShader.shader";

                    AssetDatabase.ImportAsset(veg, ImportAssetOptions.ForceUpdate |
                                                   ImportAssetOptions.ForceSynchronousImport);

                    Debug.Log("[IsoRPG] Шейдер растительности переимпортирован.");
                    break;
                }

                case "grass-shot":
                    GrassShot.Shoot();
                    break;

                case "author-mats":
                    AuthorMats.Report();
                    break;

                case "author-repair":
                    AuthorRepair.Fix();
                    break;

                case "sky-reimport":
                {
                    // Взять небо автора заново из его сцены.
                    //
                    // Своё я испортил переводом материалов, и чинить починку
                    // — тот самый круг, за который меня уже отругали. Проще и
                    // честнее: снести испорченную группу и перенести её из
                    // демо-сцены заново, ничего по дороге не «улучшая».
                    const string demo =
                        "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Scene/Demo.unity";

                    var spoiled = UnityEngine.Object.FindObjectsByType<GameObject>(
                                      FindObjectsInactive.Include)
                                  .Where(g => g.name == "Background_Sky").ToArray();

                    foreach (var s in spoiled) UnityEngine.Object.DestroyImmediate(s);

                    var host = UnityEngine.Object.FindObjectsByType<GameObject>(
                                   FindObjectsInactive.Include)
                               .FirstOrDefault(g => g.name == "МИР АВТОРА");

                    if (host == null)
                    {
                        Debug.LogError("[IsoRPG] В сцене нет «МИР АВТОРА» — некуда ставить небо.");
                        break;
                    }

                    var src = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        demo, UnityEditor.SceneManagement.OpenSceneMode.Additive);

                    GameObject fresh = null;

                    foreach (var root in src.GetRootGameObjects())
                    {
                        if (root.name == "Background_Sky") { fresh = root; break; }

                        var found = root.GetComponentsInChildren<Transform>(true)
                                        .FirstOrDefault(t => t.name == "Background_Sky");

                        if (found != null) { fresh = found.gameObject; break; }
                    }

                    if (fresh == null)
                    {
                        Debug.LogError("[IsoRPG] В демо-сцене нет Background_Sky.");
                        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(src, true);
                        break;
                    }

                    // Копия, а не перенос: исходную сцену набора трогать нельзя.
                    var copy = UnityEngine.Object.Instantiate(fresh);
                    copy.name = "Background_Sky";
                    copy.transform.SetParent(host.transform, true);
                    copy.transform.position = fresh.transform.position;

                    int parts = copy.GetComponentsInChildren<Renderer>(true).Length;

                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(src, true);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Небо автора перенесено заново: снесено испорченных " +
                              spoiled.Length + ", частей в новом " + parts +
                              ". Материалы НЕ трогались.");
                    break;
                }

                case "water-shader-build":
                {
                    // Собрать шейдер воды из шаблона набора.
                    //
                    // Главный шейдер лежит файлом `.watershader` — это не
                    // шейдер, а ЗАГОТОВКА с подстановками вида %shader_name%.
                    // Настоящий шейдер из неё делает импортёр набора при
                    // импорте файла. Когда набор ставился, проект не
                    // компилировался — импортёра не существовало, и шейдер
                    // не собрался вовсе. Отсюда розовая вода: материал
                    // ссылался на то, чего нет.
                    const string tpl = "Assets/StylizedWater2/Shaders/" +
                                       "StylizedWater2_Standard.watershader";

                    if (!System.IO.File.Exists(tpl))
                    {
                        Debug.LogError("[IsoRPG] Заготовки шейдера нет: " + tpl);
                        break;
                    }

                    AssetDatabase.ImportAsset(tpl, ImportAssetOptions.ForceUpdate |
                                                   ImportAssetOptions.ForceSynchronousImport);
                    AssetDatabase.Refresh();

                    // Проверяем РЕЗУЛЬТАТ, а не факт вызова: импортёр может
                    // отработать вхолостую и промолчать.
                    var made = AssetDatabase.LoadAssetAtPath<Shader>(tpl);

                    if (made == null)
                    {
                        Debug.LogError("[IsoRPG] Импортёр отработал, но шейдера не появилось. " +
                                       "Значит заготовка ему не по зубам на этой версии движка.");
                        break;
                    }

                    Debug.Log("[IsoRPG] Шейдер воды собран: «" + made.name +
                              "», поддерживается движком: " + made.isSupported);
                    break;
                }

                case "sw2-mat":
                {
                    // Первая проверка Stylized Water 2 1.7.0: материал
                    // набора на ОДИН пруд (малый, у (20, −16)), не на всю
                    // воду сцены. Цель — узнать, доживёт ли шейдер до
                    // СОБРАННОЙ игры, а не подменить всё сразу и потом
                    // разбираться, что из этого не работало никогда.
                    var preset = AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/StylizedWater2/Materials/StylizedWater2_Smooth.mat");

                    if (preset == null)
                    {
                        Debug.LogError("[IsoRPG] Эталонного материала SW2 нет по пути.");
                        break;
                    }

                    if (preset.shader == null || !preset.shader.isSupported)
                    {
                        Debug.LogError("[IsoRPG] Шейдер SW2 не поддерживается конвейером — " +
                                       "прогони «water-shader-build» перед этим заданием.");
                        break;
                    }

                    var mine = new Material(preset) { name = "Water_Pond_SW2" };

                    string matPath = "Assets/_Game/Art/Materials/Water_Pond_SW2.mat";
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (existing != null) AssetDatabase.DeleteAsset(matPath);

                    AssetDatabase.CreateAsset(mine, matPath);
                    AssetDatabase.SaveAssets();

                    Vector3 target = new Vector3(20f, 0f, -16f);
                    MeshRenderer picked = null;
                    float bestDist = float.MaxValue;

                    foreach (var mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        var mf = mr.GetComponent<MeshFilter>();
                        if (mf == null || mf.sharedMesh == null) continue;

                        string meshName = mf.sharedMesh.name.ToLowerInvariant();
                        if (!meshName.Contains("water")) continue;
                        if (meshName.Contains("wheel") || meshName.Contains("mill") ||
                            meshName.Contains("well") || meshName.Contains("bucket") ||
                            meshName.Contains("barrel") || meshName.Contains("pump"))
                            continue;

                        var p = mr.transform.position;
                        float d = new Vector2(p.x - target.x, p.z - target.z).sqrMagnitude;
                        if (d < bestDist) { bestDist = d; picked = mr; }
                    }

                    if (picked == null)
                    {
                        Debug.LogError("[IsoRPG] Пруда у (20, −16) не нашлось — воды в сцене нет вовсе.");
                        break;
                    }

                    picked.sharedMaterial = mine;
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] SW2 поставлен на «" + picked.gameObject.name + "» на " +
                              picked.transform.position.ToString("0.0") + " (расстояние до цели " +
                              Mathf.Sqrt(bestDist).ToString("0.0") + " м). Остальная вода не тронута.");
                    break;
                }

                case "water-find":
                {
                    // Найти ВСЮ воду сцены и сказать, чем она покрашена.
                    //
                    // Замена нашла 21 объект из 22, и оставшийся рисуется
                    // розовым — значит его не узнали ни по материалу, ни по
                    // мешу. Вместо того чтобы гадать, печатаем всё, что
                    // похоже на воду, с именем меша и состоянием шейдера.
                    int shown = 0;

                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "нет меша";
                        var m = r.sharedMaterial;

                        bool looksWater =
                            mesh.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mesh.IndexOf("lake", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mesh.IndexOf("pond", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            r.gameObject.name.IndexOf("lake", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            r.gameObject.name.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            r.gameObject.name.IndexOf("pond", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (m != null && m.name.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0);

                        // Плюс всё, что вовсе не рисуется: розовое пятно
                        // именно так и выглядит изнутри.
                        bool broken = m == null || m.shader == null || !m.shader.isSupported;

                        if (!looksWater && !broken) continue;

                        Debug.Log("[IsoRPG] ВОДА? «" + r.gameObject.name + "», меш «" + mesh +
                                  "», материал «" + (m != null ? m.name : "НЕТ") +
                                  "», шейдер «" + (m != null && m.shader != null ? m.shader.name : "НЕТ") +
                                  "», рисуется: " + (m != null && m.shader != null && m.shader.isSupported) +
                                  ", размер " + r.bounds.size.x.ToString("0") + " x " +
                                  r.bounds.size.z.ToString("0") + " м");
                        shown++;
                    }

                    Debug.Log("[IsoRPG] Всего подозрительных на воду объектов: " + shown);
                    break;
                }


                case "renderer-clean":
                {
                    // Вычистить БИТЫЕ узлы из конвейера отрисовки.
                    //
                    // Я добавил в отрисовщик узел подводного вида от набора,
                    // потом набор удалил — и ссылка осталась висеть пустой.
                    // Из-за неё конвейер URP не создаётся вовсе: в журнале
                    // игры NullReferenceException в его конструкторе, картинка
                    // не рисуется, и поверх чёрного экрана виден только
                    // интерфейс. Снаружи это выглядит как «игра не
                    // запустилась», хотя звук идёт и мир живёт.
                    var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;

                    if (pipeline == null)
                    {
                        Debug.LogError("[IsoRPG] Конвейер URP не найден.");
                        break;
                    }

                    var pso = new SerializedObject(pipeline);
                    var datas = pso.FindProperty("m_RendererDataList");

                    int removed = 0;

                    for (int i = 0; datas != null && i < datas.arraySize; i++)
                    {
                        var data = datas.GetArrayElementAtIndex(i).objectReferenceValue
                                   as UnityEngine.Rendering.Universal.ScriptableRendererData;

                        if (data == null) continue;

                        var dso = new SerializedObject(data);
                        var feats = dso.FindProperty("m_RendererFeatures");
                        var map = dso.FindProperty("m_RendererFeatureMap");

                        for (int k = feats.arraySize - 1; k >= 0; k--)
                        {
                            if (feats.GetArrayElementAtIndex(k).objectReferenceValue != null) continue;

                            feats.DeleteArrayElementAtIndex(k);
                            if (map != null && k < map.arraySize) map.DeleteArrayElementAtIndex(k);

                            removed++;
                            Debug.Log("[IsoRPG] Убран битый узел из «" + data.name + "».");
                        }

                        dso.ApplyModifiedProperties();
                        EditorUtility.SetDirty(data);
                    }

                    AssetDatabase.SaveAssets();

                    Debug.Log("[IsoRPG] Конвейер вычищен: битых узлов убрано " + removed + ".");
                    break;
                }

                case "water-back":
                {
                    // Вернуть нашу воду: набор Stylized Water 2 версии 1.6.9
                    // в Unity 6.5 не работает вовсе — не собираются ни его
                    // проходы отрисовки, ни сам шейдер. Пруд пропал, а на
                    // миникарте вода стала розовой: так выглядит материал,
                    // чей шейдер не поддерживается конвейером.
                    var ours = AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/_Game/Art/Materials/Water_Lake_URP.mat");

                    if (ours == null)
                    {
                        Debug.LogError("[IsoRPG] Нашего материала воды нет — вернуть нечем.");
                        break;
                    }

                    int back = 0;

                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        // Частицы не красим никогда: у них материал — часть
                        // самого эффекта. Брызги «FX_Water_Movement_01»
                        // попадали сюда по слову Water в имени и становились
                        // плоским пятном цвета глади. Исключение по ТИПУ
                        // рендерера, а не по имени: имён у эффектов воды
                        // много, а тип один.
                        if (r is ParticleSystemRenderer) continue;

                        var mats = r.sharedMaterials;
                        bool touched = false;

                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] == null) continue;
                            // Отбор тот же, что у замены: по материалу, мешу
                            // и имени объекта, с исключением колеса мельницы.
                            // Иначе откат находит меньше, чем испортила
                            // замена, — и часть воды остаётся розовой.
                            var mfb = r.GetComponent<MeshFilter>();
                            string meshB = mfb != null && mfb.sharedMesh != null
                                ? mfb.sharedMesh.name.ToLowerInvariant() : string.Empty;
                            string nameB = r.gameObject.name.ToLowerInvariant();

                            bool notWaterB = nameB.Contains("wheel") || nameB.Contains("mill") ||
                                             nameB.Contains("well") || nameB.Contains("prop");

                            bool isWaterB =
                                mats[i].name.Contains("Water_Pond_SW2") ||
                                mats[i].name.Contains("Stylized") ||
                                (!notWaterB && (meshB.Contains("water") || nameB.Contains("lake") ||
                                                nameB.Contains("pond") || nameB.Contains("water")));

                            if (!isWaterB) continue;

                            mats[i] = ours;
                            touched = true;
                        }

                        if (!touched) continue;

                        r.sharedMaterials = mats;
                        back++;
                    }

                    int camsOn = 0;

                    foreach (var uw in UnityEngine.Object
                                 .FindObjectsByType<IsoRPG.Cameras.Underwater>(
                                     FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        uw.enabled = true;
                        camsOn++;
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Вода возвращена на наш материал: объектов " + back +
                              ", подводный туман включён на " + camsOn + " камерах.");
                    break;
                }



                case "tree-capsules":
                {
                    // Деревьям — ствол вместо кроны, кустам — ничего.
                    //
                    // Замер 03.09.2026: 975 деревьев и кустов, у всех
                    // MeshCollider, ширина физики от 0.5 до 11 метров при
                    // середине 2.1. Камера, обходящая препятствия, ловит луч
                    // в этой кроне за несколько метров от ствола и дёргается
                    // посреди чистого места — из-за этого обход и был
                    // выключен 01.09.2026.
                    //
                    // Разводим намерения: НАВИГАЦИЯ решает, где можно стоять,
                    // ФИЗИКА — во что упираешься. Сквозь листву проходят и
                    // герой, и камера, ствол не пускает ни того ни другого.
                    //
                    // Заодно снимаем 975 сеточных коллайдеров: в ММО каждый
                    // из них считается при каждом запросе физики, а капсула
                    // стоит несравнимо дешевле.
                    int toCapsule = 0, stripped = 0;

                    foreach (var mc in UnityEngine.Object.FindObjectsByType<MeshCollider>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (mc == null) continue;

                        string n = mc.gameObject.name;

                        bool bush = n.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool tree = n.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (!bush && !tree) continue;

                        var b = mc.bounds;
                        var go = mc.gameObject;

                        UnityEngine.Object.DestroyImmediate(mc);

                        // Куст остаётся без физики вовсе: он ниже пояса и
                        // мешает больше, чем держит.
                        if (bush) { stripped++; continue; }

                        var cap = go.AddComponent<CapsuleCollider>();

                        // Ствол — примерно шестая часть кроны в поперечнике,
                        // и не тоньше двадцати сантиметров: у молодых деревьев
                        // крона узкая, доля от неё дала бы ниточку.
                        float trunk = Mathf.Max(Mathf.Max(b.size.x, b.size.z) * 0.06f, 0.2f);

                        // Считаем в локальных единицах: у деревьев масштаб
                        // разный, а коллайдер живёт в системе объекта.
                        var scale = go.transform.lossyScale;
                        float sxz = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
                        float sy = Mathf.Max(0.0001f, Mathf.Abs(scale.y));

                        cap.radius = trunk / sxz;
                        cap.height = b.size.y / sy;
                        cap.center = new Vector3(0f, cap.height * 0.5f, 0f);
                        cap.direction = 1;   // вдоль Y

                        toCapsule++;
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Физика растительности: деревьям поставлено капсул " +
                              toCapsule + ", у кустов снята вовсе " + stripped +
                              ". Сеточных коллайдеров стало меньше на " + (toCapsule + stripped) + ".");
                    break;
                }

                case "tree-collider-probe":
                {
                    // Чем закрыты деревья: стволом или кроной.
                    //
                    // От этого зависит, можно ли вернуть камере обход
                    // препятствий. Павлон выключил его 01.09.2026 — «камера
                    // прыгает на полный зум за деревьями», — и 03.09.2026,
                    // разобрав кадры из WoW, понял механику: там камера
                    // скользит по стволу, придвигаясь к герою. Значит дело
                    // было не в механизме, а в том, ЧТО он находит: коллайдер
                    // на всю крону ловит луч в нескольких метрах от ствола, и
                    // камера дёргается посреди чистого места.
                    //
                    // Считаем по коллайдерам, а не по именам: у дерева важна
                    // не порода, а то, какой формы у него физика и насколько
                    // она шире ствола.
                    var kinds = new Dictionary<string, int>();
                    var widths = new List<float>();

                    int trees = 0;

                    foreach (var col in UnityEngine.Object.FindObjectsByType<Collider>(
                                 FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (col == null) continue;

                        string n = col.gameObject.name;

                        if (n.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) < 0 &&
                            n.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) < 0) continue;

                        trees++;

                        string kind = col.GetType().Name;
                        kinds.TryGetValue(kind, out int had);
                        kinds[kind] = had + 1;

                        var b = col.bounds;
                        widths.Add(Mathf.Max(b.size.x, b.size.z));
                    }

                    widths.Sort();

                    Debug.Log("[IsoRPG] Деревья и кусты с физикой: " + trees + ".");

                    foreach (var pair in kinds.OrderByDescending(p => p.Value))
                        Debug.Log("[IsoRPG] коллайдер " + pair.Key + ": " + pair.Value + " шт.");

                    if (widths.Count > 0)
                    {
                        Debug.Log("[IsoRPG] Ширина физики, м: наименьшая " +
                                  widths[0].ToString("0.0") + ", середина " +
                                  widths[widths.Count / 2].ToString("0.0") + ", наибольшая " +
                                  widths[widths.Count - 1].ToString("0.0") +
                                  ". Ствол — это меньше метра; всё, что шире, есть крона.");
                    }

                    break;
                }

                case "hero-mat-probe":
                {
                    // Чем покрашен герой и умеет ли это прозрачность.
                    //
                    // Растворение при близкой камере я написал по правилам URP
                    // Lit: _Surface, _Blend, ключевое слово прозрачности. У
                    // героя же материалы на собственном Shader Graph от Synty,
                    // и таких свойств у него может не быть вовсе — тогда мои
                    // SetFloat молча ничего не делают, а персонаж исчезает
                    // рывком, что Павлон и увидел 03.09.2026.
                    //
                    // Печатаем ровно то, что есть: имя шейдера и его свойства.
                    // По списку и решим, чем растворять — альфой, обрезкой по
                    // порогу или узором.
                    var hero = GameObject.FindGameObjectWithTag("Player")
                               ?? UnityEngine.Object.FindObjectsByType<IsoRPG.Player.PlayerMotor>(
                                      FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                                  .Select(m => m.gameObject).FirstOrDefault();

                    if (hero == null)
                    {
                        Debug.LogError("[IsoRPG] Героя в сцене не нашёл — смотреть нечего.");
                        break;
                    }

                    var seenShaders = new HashSet<string>();

                    foreach (var r in hero.GetComponentsInChildren<Renderer>(true))
                    {
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null || m.shader == null) continue;
                            if (!seenShaders.Add(m.shader.name)) continue;

                            var props = new List<string>();

                            for (int i = 0; i < m.shader.GetPropertyCount(); i++)
                                props.Add(m.shader.GetPropertyName(i));

                            Debug.Log("[IsoRPG] герой: материал «" + m.name + "», шейдер «" +
                                      m.shader.name + "», свойств " + props.Count);

                            Debug.Log("[IsoRPG] свойства: " + string.Join(", ", props));
                        }
                    }

                    Debug.Log("[IsoRPG] Материалы героя: разных шейдеров " + seenShaders.Count + ".");
                    break;
                }

                case "water-depth-author":
                case "water-depth-ours":
                {
                    // Глубина воды: у автора и у нас.
                    //
                    // Павлон 03.09.2026 про наш пруд: «цвет её меняется ближе
                    // к островку на более тёмный, это что-то типа тени, надо
                    // посмотреть как у автора». Тени там нет: авторский шейдер
                    // красит воду по ГЛУБИНЕ — сколько её под точкой, столько
                    // и темноты. Значит вопрос не к шейдеру, а к чаше, и
                    // отвечается он замером, а не рассуждением.
                    //
                    // Меряем сеткой лучей вниз от самой глади: где дно, там и
                    // глубина. Ровно тот же щуп по обеим сценам — иначе числа
                    // не сравнить.
                    bool author = lowered == "water-depth-author";

                    if (author)
                    {
                        const string demo = "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Scene/Demo.unity";

                        if (!System.IO.File.Exists(demo))
                        {
                            Debug.LogError("[IsoRPG] Демо-сцены автора нет: " + demo);
                            break;
                        }

                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                            demo, UnityEditor.SceneManagement.OpenSceneMode.Single);
                    }

                    int ponds = 0;

                    foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                                 FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (r == null || r.sharedMaterial == null) continue;

                        var sh = r.sharedMaterial.shader;
                        bool isWater = sh != null &&
                            sh.name.IndexOf("WaterShader", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (!isWater) continue;

                        var b = r.bounds;

                        // Мелкие звенья ручья пропускаем: интересует водоём.
                        if (b.size.x < 8f && b.size.z < 8f) continue;

                        float deepest = 0f, sum = 0f;
                        int hits = 0;

                        // Сетка пять на пять по глади. Пускаем от самой
                        // поверхности вниз и не считаем промахи: за краем
                        // прямоугольника воды может не быть вовсе.
                        for (int ix = 1; ix <= 5; ix++)
                        for (int iz = 1; iz <= 5; iz++)
                        {
                            var from = new Vector3(
                                Mathf.Lerp(b.min.x, b.max.x, ix / 6f),
                                b.max.y - 0.05f,
                                Mathf.Lerp(b.min.z, b.max.z, iz / 6f));

                            if (!Physics.Raycast(from, Vector3.down, out var hit, 60f,
                                                 ~0, QueryTriggerInteraction.Ignore))
                                continue;

                            float depth = from.y - hit.point.y;
                            if (depth <= 0.01f) continue;

                            hits++;
                            sum += depth;
                            if (depth > deepest) deepest = depth;
                        }

                        ponds++;

                        Debug.Log("[IsoRPG] водоём «" + r.gameObject.name + "» " +
                                  b.size.x.ToString("0") + " x " + b.size.z.ToString("0") + " м: " +
                                  (hits > 0
                                      ? "средняя глубина " + (sum / hits).ToString("0.00") +
                                        " м, наибольшая " + deepest.ToString("0.00") + " м, замеров " + hits
                                      : "дна под гладью не нашлось"));
                    }

                    Debug.Log("[IsoRPG] Глубина воды (" + (author ? "сцена АВТОРА" : "наш мир") +
                              "): водоёмов измерено " + ponds + ".");
                    break;
                }

                case "portraits-import":
                {
                    // Настроить импорт нарисованных портретов.
                    //
                    // Павлон 03.09.2026 отдал девять новых портретов — герой,
                    // НПС, звери, конь. Исходники по 2048x2048 и пять
                    // мегабайт: для картинки, которая рисуется в интерфейсе
                    // на 60–120 точек, это в шестнадцать раз больше, чем
                    // нужно. Девять таких съели бы под сорок мегабайт памяти
                    // ради того, чего не видно.
                    //
                    // Мипмапы интерфейсу тоже не нужны: спрайт всегда рисуется
                    // в плоскости экрана, а уменьшенные копии его только
                    // мылят.
                    const string folder = "Assets/_Game/Resources/UI/Portraits";
                    const int cap = 512;

                    int touched = 0, seen = 0;

                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

                        if (importer == null) continue;

                        seen++;
                        bool dirty = false;

                        if (importer.textureType != TextureImporterType.Sprite)
                        {
                            importer.textureType = TextureImporterType.Sprite;
                            dirty = true;
                        }

                        if (importer.spriteImportMode != SpriteImportMode.Single)
                        {
                            importer.spriteImportMode = SpriteImportMode.Single;
                            dirty = true;
                        }

                        if (!importer.alphaIsTransparency)
                        {
                            importer.alphaIsTransparency = true;
                            dirty = true;
                        }

                        if (importer.mipmapEnabled)
                        {
                            importer.mipmapEnabled = false;
                            dirty = true;
                        }

                        if (importer.maxTextureSize > cap)
                        {
                            importer.maxTextureSize = cap;
                            dirty = true;
                        }

                        if (!dirty) continue;

                        importer.SaveAndReimport();
                        touched++;
                    }

                    Debug.Log("[IsoRPG] Портреты: просмотрено " + seen +
                              ", поправлен импорт у " + touched + " (потолок " + cap +
                              ", мипмапы сняты).");
                    break;
                }

                case "rock-probe":
                {
                    // Видит ли камера скалы, в которые проваливается.
                    //
                    // Павлон 03.09.2026 спрыгнул с горы в точке (121, 142) и
                    // получил кадр изнутри камня. Защита камеры отбирает
                    // препятствия СЛОЕМ и стреляет лучом от героя — то и
                    // другое уже чинили. Прежде чем чинить в третий раз,
                    // проверяем то, чего никто не проверял: а есть ли у этих
                    // скал коллайдер вообще. Луч не видит того, чего нет в
                    // физике, и выглядит это ровно как «защита не работает».
                    //
                    // Мелкий декор мы намеренно лишали коллайдеров по размеру
                    // (порог 0.9 м) — если под ту же гребёнку попали скалы,
                    // причина здесь.
                    var at = new Vector3(121f, 0f, 142f);
                    const float Around = 20f;

                    int big = 0, withCollider = 0;
                    var report = new List<string>();

                    foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                                 FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (r == null) continue;

                        var b = r.bounds;

                        // Рядом по горизонтали и крупное: скалы, а не трава.
                        if (Mathf.Abs(b.center.x - at.x) > Around) continue;
                        if (Mathf.Abs(b.center.z - at.z) > Around) continue;
                        if (b.size.y < 2f && b.size.x < 2f) continue;

                        big++;

                        // Коллайдер ищем и ВВЕРХ по иерархии, а не только на
                        // себе и в детях. У префабов набора рендерер сидит в
                        // дочернем узле уровня детализации, а коллайдер — на
                        // корне; первая версия щупа этого не знала и выдала
                        // «крупных 113, с коллайдером 7», обвинив в бесплотности
                        // деревья, у которых физика на месте.
                        var col = r.GetComponentInParent<Collider>();
                        if (col == null) col = r.GetComponentInChildren<Collider>();

                        if (col != null) withCollider++;

                        report.Add("«" + r.gameObject.name + "» слой " +
                                   LayerMask.LayerToName(r.gameObject.layer) +
                                   ", размер " + b.size.x.ToString("0.0") + " x " +
                                   b.size.y.ToString("0.0") + " x " + b.size.z.ToString("0.0") +
                                   ", коллайдер " + (col == null ? "НЕТ" : col.GetType().Name));
                    }

                    foreach (var line in report.Take(25))
                        Debug.Log("[IsoRPG] скала: " + line);

                    Debug.Log("[IsoRPG] Скалы у точки (" + at.x + ", " + at.z + "): крупных объектов " +
                              big + ", из них с коллайдером " + withCollider + ".");
                    break;
                }

                case "shader-check":
                {
                    // Собирается ли шейдер воды — до сборки игры.
                    //
                    // Полный билд стоит пять минут, а ответ на вопрос «лёг ли
                    // патч» лежит в редакторе и достаётся за секунды: Unity
                    // хранит сообщения компилятора по каждому шейдеру.
                    //
                    // И это ровно тот случай, где зелёный ответ надо уметь
                    // отличить от пустого: если шейдер не найден, сообщений
                    // тоже ноль — поэтому сначала печатаем, нашли ли его.
                    string path = "Assets/PolygonNatureBiomes/PNB_Core/Shaders/" +
                                  "SyntyStudios_WaterShader.shader";

                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);

                    if (shader == null)
                    {
                        Debug.LogError("[IsoRPG] Шейдера воды нет по пути " + path);
                        break;
                    }

                    int errors = ShaderUtil.GetShaderMessageCount(shader);

                    Debug.Log("[IsoRPG] Шейдер «" + shader.name + "» найден, сообщений компилятора " +
                              errors + ", ошибок " + (ShaderUtil.ShaderHasError(shader) ? "ЕСТЬ" : "нет") + ".");

                    if (errors > 0)
                    {
                        foreach (var m in ShaderUtil.GetShaderMessages(shader).Take(6))
                            Debug.Log("[IsoRPG] шейдер: " + m.severity + " — " + m.message +
                                      " (строка " + m.line + ")");
                    }

                    break;
                }

                case "water-lake-author":
                {
                    // Перевести НАШ пруд на авторскую воду.
                    //
                    // Задание «water-restore» вернуло авторские материалы там,
                    // где было с чем сверяться — у объектов из префабов
                    // набора. Пруд в это не попал и попасть не мог: мы его
                    // построили сами заданием «pond», источника у него нет.
                    // Павлон 03.09.2026 увидел это первым: «в этом озере
                    // по-моему осталась наша старая вода».
                    //
                    // Ставим ровно тот материал, которым автор кроет ОЗЁРА, —
                    // у него их два вида, озеро и река, и мешать их незачем:
                    // разница видна на воде.
                    //
                    // Делать это можно только после того, как патч шейдера
                    // подтверждён в собранной игре: с несобираемым шейдером
                    // вода не розовеет, а исчезает, и пруд пропал бы так же,
                    // как пропали ручьи.
                    var lakeMat = AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Materials/Water_Lake_01.mat");

                    if (lakeMat == null)
                    {
                        Debug.LogError("[IsoRPG] Нет авторского материала озера — переводить нечем.");
                        break;
                    }

                    int moved = 0;

                    foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (r == null) continue;

                        var mats = r.sharedMaterials;
                        bool touched = false;

                        for (int i = 0; i < mats.Length; i++)
                        {
                            if (mats[i] == null) continue;
                            if (!mats[i].name.Contains("Water_Lake_URP")) continue;

                            mats[i] = lakeMat;
                            touched = true;
                        }

                        if (!touched) continue;

                        r.sharedMaterials = mats;
                        moved++;

                        Debug.Log("[IsoRPG] пруд на авторскую воду: " + r.gameObject.name);
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Пруд переведён на авторский материал озера: объектов " + moved + ".");
                    break;
                }

                case "water-restore":
                {
                    // Вернуть воде авторские материалы.
                    //
                    // Решение Павлона 03.09.2026 после замера: у автора вода
                    // трёх видов — озеро, река и базовая, — а мы залили все
                    // три материалом озера. Река потому и читалась как пруд,
                    // растёкшийся по склону.
                    //
                    // Подменять было незачем: авторский шейдер
                    // «SyntyStudios/WaterShader» несёт тег
                    // RenderPipeline=UniversalPipeline, то есть работает в
                    // нашем конвейере. Замена была лечением болезни, которой
                    // не было, и стоила нам авторского течения.
                    //
                    // Возвращаем не по списку имён, а по расхождению с
                    // префабом: что автор поставил, то и ставим обратно.
                    // Ограничиваем водой, иначе откатится и намеренное
                    // разнообразие травы с деревьями — там расхождений пять
                    // тысяч, и они наша работа, а не порча.
                    int back = 0;
                    var kinds = new Dictionary<string, int>();

                    foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (r == null) continue;

                        var source = PrefabUtility.GetCorrespondingObjectFromSource(r);
                        if (source == null) continue;

                        var mine = r.sharedMaterials;
                        var orig = source.sharedMaterials;

                        bool touched = false;

                        for (int i = 0; i < mine.Length && i < orig.Length; i++)
                        {
                            if (mine[i] == orig[i] || orig[i] == null) continue;

                            // Только вода: у автора все её материалы названы
                            // с этого слова, а трава и деревья — нет.
                            if (orig[i].name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) < 0)
                                continue;

                            string key = (mine[i] != null ? mine[i].name : "(пусто)") +
                                         "  ->  " + orig[i].name;
                            kinds.TryGetValue(key, out int had);
                            kinds[key] = had + 1;

                            mine[i] = orig[i];
                            touched = true;
                        }

                        if (!touched) continue;

                        r.sharedMaterials = mine;
                        back++;
                    }

                    foreach (var pair in kinds)
                        Debug.Log("[IsoRPG] вернул воде x" + pair.Value + ": " + pair.Key);

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Вода: авторский материал возвращён " + back +
                              " объектам. Проверять в СОБРАННОЙ игре: шейдер набора " +
                              "должен доехать до билда, иначе вода станет розовой.");
                    break;
                }

                case "fx-restore":
                {
                    // Вернуть частицам их авторский материал.
                    //
                    // Частицы мы не красим НИКОГДА, и это правило для всего
                    // класса, а не для найденных брызг. У частицы материал —
                    // часть самого эффекта: аддитивный, полупрозрачный, с
                    // вершинным цветом. Любой материал поверхности вместо
                    // него превращает эффект в плоское пятно, и именно это
                    // Павлон увидел у ручья: «текстура брызг стала закрашена
                    // в цвет воды».
                    //
                    // Поймало их перекраской воды по куску имени: в
                    // «FX_Water_Movement_01» есть слово Water. Отбор по имени
                    // подводил уже трижды, поэтому и здесь мы не перечисляем
                    // имена, а спрашиваем префаб: что автор поставил, то и
                    // возвращаем.
                    int back = 0, kinds = 0;
                    var names = new HashSet<string>();

                    foreach (var r in UnityEngine.Object.FindObjectsByType<ParticleSystemRenderer>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (r == null) continue;

                        var source = PrefabUtility.GetCorrespondingObjectFromSource(r);
                        if (source == null) continue;

                        var mine = r.sharedMaterials;
                        var orig = source.sharedMaterials;

                        bool touched = false;

                        for (int i = 0; i < mine.Length && i < orig.Length; i++)
                        {
                            if (mine[i] == orig[i]) continue;

                            if (names.Add(r.gameObject.name + ": " +
                                          (mine[i] != null ? mine[i].name : "(пусто)") + " -> " +
                                          (orig[i] != null ? orig[i].name : "(пусто)")))
                                kinds++;

                            mine[i] = orig[i];
                            touched = true;
                        }

                        if (!touched) continue;

                        r.sharedMaterials = mine;
                        back++;
                    }

                    foreach (var n in names) Debug.Log("[IsoRPG] вернул частицам: " + n);

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Частицы: возвращён авторский материал у " + back +
                              " эффектов, видов подмены " + kinds + ".");
                    break;
                }

                case "mat-overrides":
                {
                    // Где наш материал лёг поверх авторского.
                    //
                    // Павлон 03.09.2026, увидев перекрашенные брызги: «а если
                    // ты какие-то родные материалы затирал?» Вопрос честный, и
                    // отвечать на него памятью нельзя: перекраска шла отбором
                    // по куску имени, а он ловит не только то, что задумано.
                    //
                    // Спрашиваем не себя, а Unity: у объекта, поставленного из
                    // префаба, есть источник, и разница между материалом
                    // экземпляра и материалом источника — это ровно наша
                    // правка, кем бы она ни была сделана. Список расхождений и
                    // есть ответ.
                    var swaps = new Dictionary<string, int>();
                    int checkedCount = 0, changed = 0;

                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (r == null) continue;

                        var source = PrefabUtility.GetCorrespondingObjectFromSource(r);
                        if (source == null) continue;   // не из префаба, сравнивать не с чем

                        checkedCount++;

                        var mine = r.sharedMaterials;
                        var orig = source.sharedMaterials;

                        for (int i = 0; i < mine.Length && i < orig.Length; i++)
                        {
                            if (mine[i] == orig[i]) continue;

                            string was = orig[i] != null ? orig[i].name : "(пусто)";
                            string now = mine[i] != null ? mine[i].name : "(пусто)";

                            string key = was + "  ->  " + now;
                            swaps.TryGetValue(key, out int had);
                            swaps[key] = had + 1;

                            changed++;
                        }
                    }

                    Debug.Log("[IsoRPG] Материалы: сверено с префабами " + checkedCount +
                              " объектов, расходятся " + changed + ".");

                    foreach (var pair in swaps.OrderByDescending(p => p.Value))
                        Debug.Log("[IsoRPG] подмена x" + pair.Value + ": " + pair.Key);

                    break;
                }

                case "water-probe":
                {
                    // Кого игра считает водой — и почему экран синеет на суше.
                    //
                    // Павлон 03.09.2026: у ручья пена закрашена цветом воды, а
                    // при повороте камеры экран заливает синим, хотя герой
                    // стоит на берегу. Обе жалобы проверяются числами, а не
                    // рассуждением, поэтому печатаем ровно два факта: чем
                    // покрашен каждый водный объект и какие из них накрывают
                    // точку, где стоял герой.
                    //
                    // Подводный вид считает погружением попадание камеры в
                    // габаритную КОРОБКУ рендерера. У наклонного ручья и у
                    // плоской пены, лежащей на камнях, эта коробка не равна
                    // воде: она прямоугольна, тянется вверх до истока и
                    // накрывает берег. Если догадка верна, ниже будет видно
                    // объект, чья коробка накрывает героя, стоящего посуху.
                    var where = new Vector2(-2f, 126f);   // координаты с кадра Павлона

                    int total = 0, caughtCount = 0, overHero = 0;

                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (r == null) continue;

                        var mat = r.sharedMaterial;

                        string matName = mat != null ? mat.name : "(нет материала)";
                        string goName = r.gameObject.name;

                        bool looksWater =
                            matName.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            goName.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            goName.IndexOf("Foam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            goName.IndexOf("Splash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            goName.IndexOf("River", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (!looksWater) continue;

                        total++;

                        // Ровно тот отбор, которым пользуется подводный вид.
                        bool caught = mat != null && mat.name.Contains("Water_Lake");
                        if (caught) caughtCount++;

                        var b = r.bounds;

                        bool covers = where.x >= b.min.x && where.x <= b.max.x &&
                                      where.y >= b.min.z && where.y <= b.max.z;

                        if (covers && caught) overHero++;

                        // Печатаем всё, но помечаем виновных: список водных
                        // объектов у автора длинный, а важны в нём двое —
                        // покрашенные не тем и накрывающие сушу.
                        Debug.Log("[IsoRPG] вода: «" + goName + "» материал «" + matName +
                                  "» коробка " + b.size.x.ToString("0.0") + " x " +
                                  b.size.y.ToString("0.0") + " x " + b.size.z.ToString("0.0") +
                                  " м, верх Y " + b.max.y.ToString("0.00") +
                                  (caught ? " | ЛОВИТ подводный вид" : "") +
                                  (covers ? " | НАКРЫВАЕТ точку героя" : "") +
                                  (r.gameObject.activeInHierarchy ? "" : " | выключен"));
                    }

                    Debug.Log("[IsoRPG] Вода, итог: похожих на воду объектов " + total +
                              ", из них ловит подводный вид " + caughtCount +
                              ", накрывают точку героя (" + where.x + ", " + where.y + ") — " +
                              overHero + ".");
                    break;
                }

                case "underwater":
                {
                    // Вода: двусторонняя гладь и подводный вид.
                    //
                    // Гладь — плоскость гранями вверх, снизу она не рисуется
                    // вовсе, и пруд исчезает целиком. Снимаем отсечение
                    // граней у материала и вешаем на камеру подводный туман.
                    int faces = 0;

                    foreach (var guid in AssetDatabase.FindAssets("t:Material Water"))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                        if (mat == null || !mat.HasProperty("_Cull")) continue;

                        // Вся вода, а не только озеро.
                        //
                        // Здесь стояло «имя содержит Water_Lake», и пока вся
                        // вода была залита нашим материалом, этого хватало.
                        // После возврата авторских материалов под отбор
                        // перестали попадать река и базовая вода: их гладь
                        // осталась односторонней, и при низком угле камеры
                        // водоём исчезал целиком — Павлон 03.09.2026 принял
                        // это за баг камеры и предложил придвигать её к
                        // герою. Признак у всей авторской воды один и тот же
                        // — её шейдер.
                        bool authorWater = mat.shader != null &&
                            mat.shader.name.IndexOf("WaterShader",
                                StringComparison.OrdinalIgnoreCase) >= 0;

                        if (!authorWater && !mat.name.Contains("Water_Lake")) continue;

                        mat.SetFloat("_Cull", 0f);          // Off: видно с обеих сторон
                        mat.doubleSidedGI = true;
                        EditorUtility.SetDirty(mat);
                        faces++;

                        Debug.Log("[IsoRPG] Гладь стала двусторонней: " + mat.name);
                    }

                    AssetDatabase.SaveAssets();

                    // Компонент на камеру. Ищем ту, что снимает мир, а не
                    // камеру интерфейса: у последней нет подводного вида.
                    var cam = UnityEngine.Object.FindObjectsByType<Camera>(
                                  FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                              .OrderByDescending(c => c.depth == 0 ? 1 : 0)
                              .FirstOrDefault(c => c.cullingMask != 0);

                    if (cam == null)
                    {
                        Debug.LogError("[IsoRPG] Камеры в сцене нет — вешать подводный вид некуда.");
                        break;
                    }

                    var uw = cam.GetComponent<IsoRPG.Cameras.Underwater>();

                    if (uw == null) uw = cam.gameObject.AddComponent<IsoRPG.Cameras.Underwater>();

                    // Цвет и непрозрачность заливки — из ЦВЕТА САМОЙ ВОДЫ, а
                    // не придуманы отдельно. У «Water_Lake_URP» уже есть тон,
                    // снятый с оригинального материала набора («water-fix»);
                    // повторно изобретать его здесь — плодить два источника
                    // истины для одного и того же цвета.
                    //
                    // Непрозрачность заливки привязана к альфе того же
                    // материала: непрозрачная вода мутнее, прозрачная —
                    // светлее. Коэффициент 0.6 подобран так, чтобы персонажа
                    // было видно сквозь синеву, а не только синеву. Другая
                    // вода с другой альфой получит соразмерную непрозрачность
                    // автоматически, а не наугад.
                    var srcWater = AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/_Game/Art/Materials/Water_Lake_URP.mat");

                    Color lookColor = new Color(0.10f, 0.42f, 0.44f);   // запасной — старое проверенное значение
                    float lookAlpha = 0.5f;

                    if (srcWater != null && srcWater.HasProperty("_BaseColor"))
                    {
                        var c = srcWater.GetColor("_BaseColor");
                        lookColor = new Color(c.r, c.g, c.b);
                        lookAlpha = Mathf.Clamp(c.a * 0.6f, 0.35f, 0.65f);

                        Debug.Log("[IsoRPG] Подводный тон снят с «" + srcWater.name + "»: " +
                                  lookColor + ", непрозрачность заливки " + lookAlpha.ToString("0.00") +
                                  " (альфа воды " + c.a.ToString("0.00") + ").");
                    }
                    else
                    {
                        Debug.LogWarning("[IsoRPG] Материала воды нет или в нём нет _BaseColor — " +
                                         "подводный тон взят запасным, старым проверенным.");
                    }

                    uw.SetLook(lookColor, lookAlpha);
                    EditorUtility.SetDirty(uw);

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Вода: материалов сделано двусторонними " + faces +
                              ", подводный вид повешен на камеру «" + cam.name + "».");
                    break;
                }

                case "author-light":
                    AuthorLight.Report();
                    break;

                case "author-light-use":
                    AuthorLight.Adopt();
                    break;

                case "backdrop-off":
                {
                    // Убрать дальний план — «бесконечные поля» вокруг локации.
                    //
                    // Автор закрывает горизонт обманкой: за игровой землёй в
                    // 400 м лежат огромные плоскости с полями, уходящие на
                    // километры. Их место займут горы, поэтому пока просто
                    // гасим. ВЫКЛЮЧАЕМ, а не удаляем: вернуть или заменить
                    // надо будет одним движением, а удаление необратимо.
                    //
                    // Небо не трогаем: купола и облачные кольца тоже стоят
                    // далеко и огромны, но без них будет дыра вместо неба.
                    // Отличаем по принадлежности к Background_Sky.
                    const float Edge = 210f;   // граница земли автора плюс запас

                    int off = 0;
                    float farthest = 0f;

                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(
                                 FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    {
                        if (r == null || !r.gameObject.activeInHierarchy) continue;

                        // Небо и облака пропускаем.
                        bool sky = false;
                        var walk = r.transform;

                        while (walk != null)
                        {
                            if (walk.name == "Background_Sky") { sky = true; break; }
                            walk = walk.parent;
                        }

                        if (sky) continue;

                        var b = r.bounds;

                        // Дальний план: либо стоит за краем земли, либо сам
                        // размером с несколько её карт.
                        bool beyond = Mathf.Abs(b.center.x) > Edge ||
                                      Mathf.Abs(b.center.z) > Edge;

                        bool huge = b.size.x > 600f || b.size.z > 600f;

                        if (!beyond && !huge) continue;

                        float away = Mathf.Max(Mathf.Abs(b.center.x), Mathf.Abs(b.center.z));
                        if (away > farthest) farthest = away;

                        Debug.Log("[IsoRPG] Гашу дальний план: «" + r.gameObject.name +
                                  "», размер " + b.size.x.ToString("0") + " x " +
                                  b.size.z.ToString("0") + " м, в точке " +
                                  b.center.x.ToString("0") + ", " + b.center.z.ToString("0"));

                        r.gameObject.SetActive(false);
                        off++;
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Дальний план погашен: объектов " + off +
                              ", самый дальний стоял в " + farthest.ToString("0") +
                              " м от середины. Небо не тронуто.");
                    break;
                }

                case "sky-upper-off":
                {
                    // Выключить ВЕРХНИЙ купол неба.
                    //
                    // У автора небо собрано из двух вложенных куполов: нижний
                    // 639 м высотой даёт розово-белый градиент и в URP
                    // рисуется верно, верхний 1760 м — тёмно-синюю заливку
                    // вместо градиента. Его шейдер `SyntyStudios/SkyboxUnlit`
                    // в URP считает высоту иначе, и купол читается синим
                    // диском над головой. Материалы НЕ трогаем: одно
                    // выключение обратимо, а правка шейдера — нет.
                    int off = 0;

                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (r.gameObject.name.IndexOf("Skydome",
                                System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                        // Верхний — тот, что выше. Отличаем по габаритам, а
                        // не по имени: имена у них одинаковые.
                        if (r.bounds.size.y < 1000f) continue;

                        r.gameObject.SetActive(false);
                        off++;

                        Debug.Log("[IsoRPG] Выключен верхний купол «" + r.gameObject.name +
                                  "», высота " + r.bounds.size.y.ToString("0") + " м.");
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Верхних куполов выключено: " + off + ".");
                    break;
                }

                case "sky-report":
                {
                    // Перепись ВСЕГО, что относится к небу автора.
                    //
                    // «Купол» на кадре имеет форму горы с плоской вершиной —
                    // а купол круглый. Значит под одним словом могут прятаться
                    // разные объекты, и гадать, какой из них синий, нельзя.
                    // Печатаем группу целиком: имя, размер, шейдер, материал,
                    // включён ли объект.
                    var group = UnityEngine.Object.FindObjectsByType<GameObject>(
                                    FindObjectsInactive.Include)
                                .FirstOrDefault(g => g.name == "Background_Sky");

                    if (group == null)
                    {
                        Debug.LogError("[IsoRPG] Группы Background_Sky в сцене нет.");
                        break;
                    }

                    Debug.Log("[IsoRPG] === ЧТО ЛЕЖИТ В BACKGROUND_SKY ===");

                    foreach (var r in group.GetComponentsInChildren<Renderer>(true))
                    {
                        var m = r.sharedMaterial;
                        var b = r.bounds;

                        Debug.Log("[IsoRPG] «" + r.gameObject.name + "»: " +
                                  (r.gameObject.activeInHierarchy ? "включён" : "ВЫКЛЮЧЕН") +
                                  ", размер " + b.size.x.ToString("0") + " x " +
                                  b.size.y.ToString("0") + " x " + b.size.z.ToString("0") +
                                  " м, в точке " + r.transform.position +
                                  ", материал " + (m != null ? m.name : "НЕТ") +
                                  ", шейдер " + (m != null && m.shader != null
                                      ? m.shader.name : "НЕТ") +
                                  ", очередь " + (m != null ? m.renderQueue.ToString() : "—"));
                    }

                    Debug.Log("[IsoRPG] Небо в настройках сцены: " +
                              (RenderSettings.skybox != null
                                  ? RenderSettings.skybox.name : "НЕТ") +
                              ", дальняя граница камеры даст обрез, если купол за ней.");
                    break;
                }

                case "sky-dome-restore":
                {
                    // Вернуть куполу его РОДНОЙ материал.
                    //
                    // Купол автора работал в URP как есть. Синим диском были
                    // облачные кольца — их я перевёл верно, а купол трогать
                    // было незачем: «за компанию» он потерял текстуру и стал
                    // плоским тёмно-синим силуэтом. Это ровно тот случай,
                    // когда починка ломает исправное.
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Materials/" +
                        "Skybox_Meadows_Mat_01.mat");

                    if (mat == null)
                    {
                        Debug.LogError("[IsoRPG] Родного материала неба нет — " +
                                       "искать в PNB_Meadow_Forest/Materials.");
                        break;
                    }

                    int back = 0;

                    foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (r.gameObject.name.IndexOf("Skydome",
                                System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                        r.sharedMaterial = mat;
                        back++;
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Куполу возвращён родной материал «" + mat.name +
                              "» на " + back + " объектах.");
                    break;
                }

                case "sky-dome-purge":
                {
                    // Снести ВСЕ куполы неба, кроме авторских.
                    //
                    // Прошлое задание удаляло по имени «Небо Synty» и сняло
                    // один объект из двух: второй наш купол лежит под другим
                    // именем, и заказчик видит его третий заход подряд.
                    // Поэтому здесь ищем по сути — по шейдеру неба и по имени
                    // меша — и печатаем ПУТЬ каждого, чтобы было видно, чей он.
                    var suspects = UnityEngine.Object.FindObjectsByType<Renderer>(
                                       FindObjectsInactive.Include, FindObjectsSortMode.None)
                                   .Where(r =>
                                   {
                                       var m = r.sharedMaterial;
                                       bool skyShader = m != null && m.shader != null &&
                                                        m.shader.name.Contains("Skybox");
                                       bool skyName = r.gameObject.name.IndexOf(
                                           "Skydome", System.StringComparison.OrdinalIgnoreCase) >= 0;

                                       return skyShader || skyName;
                                   })
                                   .ToArray();

                    int removed = 0;

                    foreach (var r in suspects)
                    {
                        if (r == null) continue;

                        // Полный путь: по нему сразу видно, из чьей сцены объект.
                        var path = r.gameObject.name;
                        var walk = r.transform.parent;
                        bool authors = false;

                        while (walk != null)
                        {
                            path = walk.name + "/" + path;
                            if (walk.name == "МИР АВТОРА") authors = true;
                            walk = walk.parent;
                        }

                        Debug.Log("[IsoRPG] Купол: " + path +
                                  (authors ? "  — авторский, оставляю"
                                           : "  — НАШ, сношу"));

                        if (!authors)
                        {
                            UnityEngine.Object.DestroyImmediate(r.gameObject);
                            removed++;
                        }
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Куполов найдено " + suspects.Length +
                              ", снесено наших " + removed + ".");
                    break;
                }

                case "flower-probe":
                    FlowerProbe.Measure();
                    break;

                case "sky-dome-off":
                {
                    // Снять купол неба, оставив настоящий скайбокс.
                    //
                    // Купол — наш обход того, что шейдер неба Synty не встаёт
                    // в RenderSettings.skybox. Он следует за камерой только
                    // по горизонтали, а по высоте стоит там, где его
                    // поставили. На земле автора это дало тёмный круг с
                    // облаками посреди неба — заказчик увидел его первым
                    // кадром. Здесь купол не нужен вовсе: скайбокс лугового
                    // биома в настройках сцены уже стоит и рисуется.
                    var domes = UnityEngine.Object.FindObjectsByType<GameObject>(
                                    FindObjectsInactive.Include)
                                .Where(g => g.name == "Небо Synty").ToArray();

                    foreach (var d in domes) UnityEngine.Object.DestroyImmediate(d);

                    string sky = RenderSettings.skybox != null
                        ? RenderSettings.skybox.name : "НЕТ";

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Купол снят: " + domes.Length +
                              ". Небо теперь скайбоксом: " + sky + ".");
                    break;
                }

                case "arena-open-author":
                {
                    // Открыть копию арены, чтобы следующие задания работали
                    // в ней.
                    //
                    // Пакетный запуск всегда открывает НАШУ арену — задание,
                    // отработавшее не в той сцене, выглядит успешным и не
                    // меняет ничего. Поэтому переключение сцены сделано
                    // явным заданием, а не спрятано внутри других.
                    const string path = "Assets/_Game/Scenes/ArenaAuthor.unity";

                    if (!System.IO.File.Exists(path))
                    {
                        Debug.LogError("[IsoRPG] Нет копии арены: " + path);
                        break;
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        path, UnityEditor.SceneManagement.OpenSceneMode.Single);

                    Debug.Log("[IsoRPG] Открыта копия арены: " + path);
                    break;
                }

                case "arena-clone":
                {
                    // Клон арены файлом, а не пересборкой.
                    //
                    // Копия файла — самый честный клон: в ней ровно то же
                    // самое, включая настройки объектов, которые ни один
                    // строитель не воспроизведёт один в один. И старая арена
                    // остаётся нетронутой — это и есть возможность вернуться.
                    const string from = "Assets/_Game/Scenes/Arena.unity";
                    const string to = "Assets/_Game/Scenes/ArenaAuthor.unity";

                    if (System.IO.File.Exists(to)) AssetDatabase.DeleteAsset(to);

                    if (!AssetDatabase.CopyAsset(from, to))
                    {
                        Debug.LogError("[IsoRPG] Не удалось скопировать арену в " + to);
                        break;
                    }

                    AssetDatabase.Refresh();
                    Debug.Log("[IsoRPG] Арена склонирована: " + to);
                    break;
                }

                case "author-world":
                {
                    // На копии арены: снести НАШУ землю и небо, оставить всё
                    // живое, развернуть мир автора.
                    //
                    // Остаются герой, волки и правила их стояния на
                    // поверхности (GroundHug у человека, GroundAlign у
                    // зверя) — они висят на самих существах и переезжают
                    // вместе с ними. Уходит только окружение.
                    const string arenaAuthor = "Assets/_Game/Scenes/ArenaAuthor.unity";
                    const string demo =
                        "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Scene/Demo.unity";

                    if (!System.IO.File.Exists(arenaAuthor))
                    {
                        Debug.LogError("[IsoRPG] Нет копии арены — сперва «arena-clone».");
                        break;
                    }

                    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        arenaAuthor, UnityEditor.SceneManagement.OpenSceneMode.Single);

                    // 1. Снос нашего окружения. Список имён, а не угадывание:
                    //    что не названо — остаётся жить.
                    string[] ourWorld =
                    {
                        "Луг Synty", "Деревья", "Пруд Synty", "Небо Synty", "СЦЕНА АВТОРА",
                    };

                    int killed = 0;

                    foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(
                                 FindObjectsInactive.Include))
                    {
                        if (go == null) continue;
                        if (!ourWorld.Contains(go.name)) continue;

                        UnityEngine.Object.DestroyImmediate(go);
                        killed++;
                    }

                    foreach (var t in UnityEngine.Object.FindObjectsByType<Terrain>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (t == null) continue;
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                        killed++;
                    }

                    Debug.Log("[IsoRPG] Наше окружение снесено: " + killed + " узлов.");

                    // 2. Мир автора — на место нашего, без сдвига: его земля
                    //    лежит от −200 до 200, а герой стоит в нуле, то есть
                    //    ровно в середине его карты.
                    var extra = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        demo, UnityEditor.SceneManagement.OpenSceneMode.Additive);

                    var holder = new GameObject("МИР АВТОРА");
                    int roots = 0;

                    foreach (var root in extra.GetRootGameObjects())
                    {
                        root.transform.SetParent(holder.transform, true);
                        roots++;
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(extra, true);

                    // Его камеру и слушателя звука выбрасываем — у нас свои,
                    // иначе картинка пойдёт через чужую камеру. Свет тоже:
                    // два солнца складываются в пересвет, наше настроено.
                    int stripped = 0;

                    foreach (var cam in holder.GetComponentsInChildren<Camera>(true))
                    { UnityEngine.Object.DestroyImmediate(cam.gameObject); stripped++; }

                    foreach (var lit in holder.GetComponentsInChildren<Light>(true))
                    { UnityEngine.Object.DestroyImmediate(lit.gameObject); stripped++; }

                    foreach (var ear in holder.GetComponentsInChildren<AudioListener>(true))
                    { UnityEngine.Object.DestroyImmediate(ear); stripped++; }

                    // 3. Героя и волков поставить на ЕГО землю: наша ушла, и
                    //    без этого они падают в пустоту с прежней высоты.
                    var newGround = UnityEngine.Object.FindObjectsByType<Terrain>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

                    int lifted = 0;

                    if (newGround != null)
                    {
                        foreach (var agent in UnityEngine.Object
                                     .FindObjectsByType<UnityEngine.AI.NavMeshAgent>(
                                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                        {
                            var p = agent.transform.position;
                            float h = newGround.SampleHeight(p) + newGround.transform.position.y;

                            agent.transform.position = new Vector3(p.x, h, p.z);
                            lifted++;
                        }
                    }

                    NavBake.Rebake();

                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

                    Debug.Log("[IsoRPG] Мир автора развёрнут на копии арены: корней " + roots +
                              ", выброшено чужих камер и светильников " + stripped +
                              ", поставлено на грунт существ " + lifted +
                              ", земля автора " +
                              (newGround != null ? newGround.terrainData.size.x.ToString("0") + " м"
                                                 : "НЕ НАЙДЕНА") + ".");
                    break;
                }

                case "arena-use-author":
                case "arena-use-ours":
                {
                    // Переключить, в какую арену ведёт кнопка «Начать игру».
                    //
                    // Меню грузит сцену ПО ИМЕНИ из своего поля, а в сборку
                    // попадает то, что перечислено в настройках сборки.
                    // Поэтому правим оба места: иначе кнопка позовёт сцену,
                    // которой в билде нет, и игра встанет на чёрном экране.
                    bool wantAuthor = lowered == "arena-use-author";

                    string arenaPath = wantAuthor
                        ? "Assets/_Game/Scenes/ArenaAuthor.unity"
                        : "Assets/_Game/Scenes/Arena.unity";

                    string arenaName = wantAuthor ? "ArenaAuthor" : "Arena";

                    if (!System.IO.File.Exists(arenaPath))
                    {
                        Debug.LogError("[IsoRPG] Нет сцены " + arenaPath);
                        break;
                    }

                    const string menuPath = "Assets/_Game/Scenes/MainMenu.unity";

                    var menuScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        menuPath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                    int fixedMenus = 0;

                    foreach (var menu in UnityEngine.Object
                                 .FindObjectsByType<IsoRPG.UI.MainMenu>(
                                     FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        menu.SetGameScene(arenaName);
                        EditorUtility.SetDirty(menu);
                        fixedMenus++;
                    }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menuScene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(menuScene);

                    EditorBuildSettings.scenes = new[]
                    {
                        new EditorBuildSettingsScene(menuPath, true),
                        new EditorBuildSettingsScene(arenaPath, true),
                    };

                    Debug.Log("[IsoRPG] Кнопка «Начать игру» ведёт в «" + arenaName +
                              "». Меню поправлено у " + fixedMenus +
                              " объектов, в сборке две сцены: меню и " + arenaName + ".");
                    break;
                }

                case "author-clear":
                {
                    // Снять приставленную сцену автора с нашей арены.
                    //
                    // Она ставилась эталоном «дойти и сверить», но живёт в
                    // том же файле сцены и тащит за собой второй террейн,
                    // навигацию на километр и лишний вес сборки. Убираем
                    // подчистую; вернуть её — одно задание «author-scene».
                    var gone = UnityEngine.Object.FindObjectsByType<GameObject>(
                                   FindObjectsInactive.Include)
                               .Where(g => g.name == "СЦЕНА АВТОРА").ToArray();

                    int inside = gone.Sum(g => g.GetComponentsInChildren<Transform>(true).Length);

                    foreach (var g in gone) UnityEngine.Object.DestroyImmediate(g);

                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Сцена автора снята с арены: корней " + gone.Length +
                              ", объектов внутри " + inside + ".");
                    break;
                }

                case "author-scene":
                {
                    // Демо-сцена автора целиком — на границе нашей карты.
                    //
                    // Нужна как живой эталон под боком: любую нашу правку
                    // можно сверить, дойдя до неё пешком, вместо спора по
                    // памяти. Свою землю она приносит с собой, поэтому
                    // ставим её ЗА нашим террейном (наш 600 м от −300 до
                    // +300), чтобы две земли не спорили за одно место.
                    const string demo =
                        "Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Scene/Demo.unity";

                    // Отступ считаем ОТ КРАЯ нашего террейна, а не на глаз.
                    //
                    // Первый заход я поставил 420 м «с запасом» — и сцена
                    // легла прямо на нашу: у его земли своя точка отсчёта, и
                    // 420 от нуля это не 420 от края. Считаем честно: правый
                    // край нашего террейна плюс сотня метров чистого поля.
                    if (!System.IO.File.Exists(demo))
                    {
                        Debug.LogError("[IsoRPG] Демо-сцены нет: " + demo);
                        break;
                    }

                    var arena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

                    var old = UnityEngine.Object.FindObjectsByType<GameObject>(
                                  FindObjectsInactive.Include)
                              .Where(g => g.name == "СЦЕНА АВТОРА").ToArray();

                    foreach (var o in old) UnityEngine.Object.DestroyImmediate(o);

                    // Край НАШЕЙ земли меряем ПОСЛЕ сноса прошлой копии.
                    //
                    // Меряя до, я получил край 2870 м — это был террейн
                    // автора, оставшийся от предыдущего прогона. Задание
                    // прилежно отставило новую копию ещё на триста метров
                    // дальше, и так каждый раз. Порядок здесь не украшение:
                    // «наш» террейн становится нашим только после уборки.
                    float ourEdge = 300f;

                    var ourTerr = UnityEngine.Object.FindObjectsByType<Terrain>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

                    if (ourTerr != null)
                        ourEdge = ourTerr.transform.position.x + ourTerr.terrainData.size.x;

                    var extra = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        demo, UnityEditor.SceneManagement.OpenSceneMode.Additive);

                    var holder = new GameObject("СЦЕНА АВТОРА");
                    int moved = 0;

                    // Равняем по ЕГО ТЕРРЕЙНУ, а не по габаритам сцены.
                    //
                    // По габаритам я уже промахнулся: сцена оказалась 4.5 км
                    // в поперечнике — в ней лежит огромная декорация-задник
                    // для горизонта. Равняясь по ней, я увёз игровую землю за
                    // два с половиной километра, куда пешком не дойти. Земля —
                    // вот что должно встать рядом с нашей.
                    float demoMinX = float.MaxValue, demoMaxX = float.MinValue;

                    foreach (var root in extra.GetRootGameObjects())
                        foreach (var t in root.GetComponentsInChildren<Terrain>(true))
                        {
                            demoMinX = Mathf.Min(demoMinX, t.transform.position.x);
                            demoMaxX = Mathf.Max(demoMaxX,
                                       t.transform.position.x + t.terrainData.size.x);
                        }

                    if (demoMinX == float.MaxValue) { demoMinX = 0f; demoMaxX = 400f; }

                    // Сдвиг такой, чтобы его западный край встал в ста метрах
                    // восточнее нашего восточного.
                    float shift = (ourEdge + 100f) - demoMinX;

                    foreach (var root in extra.GetRootGameObjects())
                    {
                        root.transform.position += new Vector3(shift, 0f, 0f);
                        root.transform.SetParent(holder.transform, true);
                        moved++;
                    }

                    Debug.Log("[IsoRPG] Наш край по X: " + ourEdge.ToString("0") +
                              " м. Сцена автора была от " + demoMinX.ToString("0") +
                              " до " + demoMaxX.ToString("0") +
                              " м, сдвиг " + shift.ToString("0") +
                              " м, станет от " + (demoMinX + shift).ToString("0") +
                              " до " + (demoMaxX + shift).ToString("0") + " м.");

                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(extra, true);

                    // Задник горизонта выбрасываем.
                    //
                    // Сцена автора 4.5 км в поперечнике при игровой земле в
                    // 400 м: остальное — огромная декорация, нарисованная,
                    // чтобы закрыть горизонт вокруг демонстрации. Тащить её
                    // в игру значит расширять наш мир до пяти километров ради
                    // картинки, которую всё равно закроет наше небо.
                    int backdrops = 0;

                    foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null) continue;

                        var size = r.bounds.size;

                        if (size.x > 1200f || size.z > 1200f)
                        {
                            UnityEngine.Object.DestroyImmediate(r.gameObject);
                            backdrops++;
                        }
                    }

                    // Своё солнце, свою камеру и своего слушателя звука
                    // выбрасываем: в нашей сцене они уже есть, и два
                    // источника света складываются в пересвет.
                    int stripped = backdrops;

                    foreach (var cam in holder.GetComponentsInChildren<Camera>(true))
                    { UnityEngine.Object.DestroyImmediate(cam.gameObject); stripped++; }

                    foreach (var lit in holder.GetComponentsInChildren<Light>(true))
                    { UnityEngine.Object.DestroyImmediate(lit.gameObject); stripped++; }

                    foreach (var ear in holder.GetComponentsInChildren<AudioListener>(true))
                    { UnityEngine.Object.DestroyImmediate(ear); stripped++; }

                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(arena);

                    Debug.Log("[IsoRPG] Сцена автора поставлена: корней " + moved +
                              ", выброшено своих камер и светильников " + stripped + ".");
                    break;
                }

                case "conform-off":
                {
                    // ВЫКЛЮЧИТЬ изгиб травы по рельефу.
                    //
                    // Изгиб — наше изобретение, у автора набора его нет вовсе:
                    // он кладёт куст на склон наклоном по нормали, и этого
                    // достаточно. Наклон мы теперь тоже делаем, а изгиб
                    // остался — и это он поднимает траву полосой над гребнем
                    // склона. Заказчик видит «трава висит в небе» второй день
                    // подряд, и каждый раз я чинил соседнее.
                    int off = 0;

                    foreach (var guid in AssetDatabase.FindAssets("t:Material Grass",
                             new[] { "Assets/PolygonNatureBiomes" }))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                        if (mat == null || !mat.HasProperty("_ConformStrength")) continue;

                        mat.SetFloat("_ConformStrength", 0f);
                        EditorUtility.SetDirty(mat);
                        off++;
                    }

                    AssetDatabase.SaveAssets();
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

                    Debug.Log("[IsoRPG] Изгиб по рельефу ВЫКЛЮЧЕН у " + off + " материалов травы.");
                    break;
                }

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

                // Пересчёт хвата по матрице из Blender. Ставить ПЕРЕД щупом:
                // щуп строит ряд вокруг посчитанного числа.
                case "hand-probe":
                    HandProbe.Run();
                    break;

                case "npc-poses":
                    NpcPoseProbe.Build();
                    SceneEye.Shot("npc-poses", NpcPoseProbe.Centre, 8.5f, 4f, 180f);
                    break;

                case "npc-move":
                    NpcMove.Apply();
                    SceneEye.Shot("npc-spot", new Vector3(53.3f, 1.2f, -30.0f), 7f, 14f, 140f);
                    break;

                case "npc-idle":
                    NpcIdleKit.Apply();
                    break;

                case "rpg-anim-probe":
                    RpgAnimProbe.Run();
                    break;

                case "hero-anim-probe":
                    HeroAnimProbe.Run();
                    break;

                case "hero-move":
                    HeroMoveKit.Apply();
                    break;

                case "hand-pose":
                    HandPose.Apply();
                    break;

                case "grip-fit":
                    GripFit.Apply();
                    break;

                case "grip-probe":
                    GripProbe.Build();

                    // Ряд целиком — чтобы выбрать вариант; и крупный план
                    // посчитанного, с двух сторон: с одного ракурса клинок
                    // закрывает кисть, и половина выводов делается о том, чего
                    // не видно (урок примерки в Blender, память проекта).
                    SceneEye.Shot("grip", GripProbe.Centre, 7.2f, 4f, 180f);
                    SceneEye.Shot("grip-one", GripProbe.FirstHand, 0.85f, 8f, 180f);
                    SceneEye.Shot("grip-one-side", GripProbe.FirstHand, 0.85f, 14f, 265f);
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

                case "creature-layer":
                    CreatureLayer.Apply();
                    break;

                case "boar-probe":
                    BoarProbe.Run();
                    break;

                case "flinch-tune":
                    FlinchTune.Apply();
                    break;

                case "cam-block":
                    CameraBlockProbe.Run();
                    break;

                case "ui-shot":
                    UiShot.Run();
                    break;

                case "ui-sprites":
                    UiSprites.Apply();
                    break;

                case "ui-norms":
                    UiNorms.Run();
                    break;

                default:
                    Debug.LogWarning("[IsoRPG] Не знаю задания «" + task + "».");
                    break;
            }
        }
    }
}
