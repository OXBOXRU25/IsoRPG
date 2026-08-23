using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using IsoRPG.Cameras;
using IsoRPG.Player;
using IsoRPG.Combat;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает игровую песочницу одним пунктом меню: земля, свет, камера,
    /// персонаж, препятствия и запечённая навигационная сетка.
    ///
    /// Зачем скриптом, а не руками: сцену можно пересобрать в любой момент
    /// одинаково, и все числа лежат в одном месте, где их видно и можно менять.
    /// </summary>
    public static class SandboxSceneBuilder
    {
        private const string ScenePath = "Assets/_Game/Scenes/Sandbox.unity";
        private const string MaterialsFolder = "Assets/_Game/Materials";

        // Палитра снята с референсов Albion Online (см. PROJECT.md).
        // Принцип оттуда же: один доминирующий тон плюс контрастный акцент.
        private static readonly Color GroundColor = new Color32(0x7C, 0x7A, 0x52, 0xFF); // приглушённая олива
        private static readonly Color RockColor = new Color32(0x8A, 0x8F, 0x94, 0xFF);   // холодный камень
        private static readonly Color PlayerColor = new Color32(0xC4, 0x62, 0x3A, 0xFF); // тёплый акцент
        private static readonly Color MarkerColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF); // отметка клика
        private static readonly Color DummyColor = new Color32(0x6E, 0x4A, 0x4A, 0xFF);  // манекен: тёплый тёмный, не путается с камнем

        private const float GroundSize = 80f;

        [MenuItem("Tools/IsoRPG/Собрать песочницу", priority = 0)]
        public static void Build()
        {
            // В режиме Play создавать сцены нельзя: сборка проходит в памяти,
            // выглядит успешной, а при выходе из Play всё откатывается к
            // файлу на диске. Самый коварный вид «работает, но не работает».
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Сначала выйди из режима Play",
                    "Пока игра запущена, сцену собрать нельзя — Unity не даёт " +
                    "создавать сцены на диске.\n\n" +
                    "Останови игру кнопкой Play и собери заново.",
                    "Понятно");

                Debug.LogWarning("[IsoRPG] Сборка отменена: нельзя собирать сцену в режиме Play.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Собрать песочницу",
                    "Будет создана новая сцена Sandbox со всем содержимым.\n\n" +
                    "Несохранённые изменения текущей сцены будут потеряны.",
                    "Собрать", "Отмена"))
            {
                return;
            }

            // Ассеты создаём ДО сборки сцены и с полным обновлением базы:
            // созданный и тут же загруженный в том же кадре ассет приходит
            // пустой ссылкой, и монстры остаются без добычи молча.
            RogueAbilitiesBuilder.Build();
            ItemsBuilder.Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();
            GameObject ground = CreateGround();
            CreateObstacles();

            // Навигацию печём ДО создания персонажа: NavMeshAgent, поставленный
            // туда, где сетки ещё нет, ругается в консоль и не двигается.
            BakeNavigation(ground);

            GameObject marker = CreateDestinationMarker();
            GameObject player = CreatePlayer(marker);
            CreateDummies();
            CreateCamera(player.transform);
            CreateEventSystem();

            EnsureFolder(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorSceneManager.MarkSceneDirty(scene);

            // Отчёт о том, что реально собралось. Нужен потому, что Unity
            // может выполнить СТАРУЮ версию этого метода, если нажать пункт
            // меню раньше, чем закончится компиляция. Со стороны выглядит как
            // «собралось, но не работает», и искать причину можно долго.
            int lootCount = Object.FindObjectsByType<IsoRPG.Items.LootSource>(
                FindObjectsSortMode.None).Length;
            int gearCount = Object.FindObjectsByType<IsoRPG.Items.StartingGear>(
                FindObjectsSortMode.None).Length;

            Debug.Log($"[IsoRPG] Песочница собрана: {ScenePath}\n" +
                      $"  монстров с добычей: {lootCount}\n" +
                      $"  стартовое снаряжение: {gearCount}\n" +
                      $"  Если числа нулевые — код не успел скомпилироваться. " +
                      $"Дождись полоски внизу справа и собери заново.");
        }

        // ------------------------------------------------------------------
        // Свет
        // ------------------------------------------------------------------

        private static void CreateLighting()
        {
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();

            light.type = LightType.Directional;

            // Низкое солнце: тени вытягиваются и дают объём пустому полю.
            // Это половина «дороговизны» картинки у Albion, стоит один поворот.
            go.transform.rotation = Quaternion.Euler(38f, 145f, 0f);

            light.color = new Color32(0xFF, 0xF0, 0xD2, 0xFF); // тёплый дневной
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(0x9C, 0xB4, 0xCC, 0xFF);     // холодное небо
            RenderSettings.ambientEquatorColor = new Color32(0x7A, 0x7A, 0x6E, 0xFF);
            RenderSettings.ambientGroundColor = new Color32(0x4A, 0x46, 0x38, 0xFF);

            // Дымка вдали прячет край локации без забора — приём с референса.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color32(0xB6, 0xBA, 0xA8, 0xFF);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 130f;
        }

        // ------------------------------------------------------------------
        // Геометрия
        // ------------------------------------------------------------------

        private static GameObject CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";

            // Plane в Unity — это 10x10 юнитов при масштабе 1.
            ground.transform.localScale = Vector3.one * (GroundSize / 10f);
            ground.transform.position = Vector3.zero;

            ApplyMaterial(ground, "M_Ground", GroundColor, smoothness: 0f);
            return ground;
        }

        private static void CreateObstacles()
        {
            var root = new GameObject("Obstacles");

            // Расставлены вручную, а не случайно: нужно видеть, что персонаж
            // именно обходит препятствие, а не проходит сквозь или застревает.
            var spots = new (Vector3 pos, Vector3 scale, float rotY)[]
            {
                (new Vector3(  6f, 0f,   4f), new Vector3(3f, 2.4f, 3f),  15f),
                (new Vector3( -5f, 0f,   7f), new Vector3(5f, 1.6f, 2f), -25f),
                (new Vector3( -8f, 0f,  -5f), new Vector3(2f, 3.2f, 2f),  40f),
                (new Vector3(  9f, 0f,  -7f), new Vector3(4f, 2f,   4f),  -8f),
                (new Vector3(  0f, 0f, -12f), new Vector3(7f, 1.8f, 2f),   0f),
                (new Vector3( 14f, 0f,   1f), new Vector3(2.5f, 4f, 2.5f), 22f),
            };

            var material = GetOrCreateMaterial("M_Rock", RockColor, smoothness: 0.08f);

            foreach (var (pos, scale, rotY) in spots)
            {
                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = "Rock";
                rock.transform.SetParent(root.transform);
                rock.transform.position = pos + Vector3.up * (scale.y * 0.5f);
                rock.transform.localScale = scale;
                rock.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
                rock.GetComponent<Renderer>().sharedMaterial = material;
            }

            CreateGridAlignedStructures(root.transform, material);
        }

        /// <summary>
        /// Постройки, строго выровненные по осям мира.
        ///
        /// Без них нельзя судить об угле поворота камеры: у повёрнутых под
        /// случайными углами камней грани видны при любом Yaw, и разница между
        /// 81° и 90° не проявляется вообще. А на выровненном доме при Yaw,
        /// кратном 90, видно ровно две грани вместо трёх — и объём пропадает.
        /// </summary>
        private static void CreateGridAlignedStructures(Transform parent, Material material)
        {
            var root = new GameObject("GridAligned");
            root.transform.SetParent(parent);

            var boxes = new (Vector3 pos, Vector3 scale)[]
            {
                (new Vector3(-14f, 0f,  10f), new Vector3(6f, 4f, 6f)),   // «дом»
                (new Vector3( -6f, 0f,  16f), new Vector3(4f, 3f, 4f)),   // «сарай»
                (new Vector3(  4f, 0f,  14f), new Vector3(10f, 1f, 1f)),  // «забор» вдоль X
                (new Vector3( 12f, 0f,   9f), new Vector3(1f, 1f, 8f)),   // «забор» вдоль Z
            };

            foreach (var (pos, scale) in boxes)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "Structure";
                box.transform.SetParent(root.transform);
                box.transform.position = pos + Vector3.up * (scale.y * 0.5f);
                box.transform.localScale = scale;
                box.transform.rotation = Quaternion.identity; // строго по осям — в этом весь смысл
                box.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        private static GameObject CreateDestinationMarker()
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "DestinationMarker";
            marker.transform.localScale = new Vector3(0.55f, 0.01f, 0.55f);

            // Коллайдер обязательно убрать: иначе отметка ловит следующий клик
            // на себя, и персонаж перестаёт слушаться там, где только что был.
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            ApplyMaterial(marker, "M_Marker", MarkerColor, smoothness: 0.2f, emissive: true);
            marker.SetActive(false);
            return marker;
        }

        // ------------------------------------------------------------------
        // Персонаж
        // ------------------------------------------------------------------

        private static GameObject CreatePlayer(GameObject marker)
        {
            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;

            // Визуал — отдельным дочерним объектом и БЕЗ коллайдера: иначе луч
            // клика попадает в самого персонажа, и он идёт сам в себя.
            GameObject visual = CreatePlayerVisual(player.transform);

            var agent = player.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 2f;
            agent.speed = 5.5f;              // подберём по ощущению
            agent.angularSpeed = 900f;       // быстрый разворот: медленный читается как «тормозит»
            agent.acceleration = 40f;
            agent.stoppingDistance = 0.05f;
            agent.autoBraking = true;

            var controller = player.AddComponent<ClickToMoveController>();

            // Отметку клика подставляем через SerializedObject — поле приватное,
            // и это честный способ его заполнить из редакторного кода.
            var so = new SerializedObject(controller);
            so.FindProperty("destinationMarker").objectReferenceValue = marker;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Анимация цепляется к движению только если визуал умеет анимироваться.
            if (visual != null && visual.GetComponentInChildren<Animator>() != null)
            {
                player.AddComponent<CharacterAnimatorDriver>();
            }

            // Бой: сам игрок тоже цель — иначе монстрам некого будет бить.
            //
            // Коллайдер обязателен, но помечен триггером. Причина тонкая:
            // монстры ищут врагов физическим сканированием, и без коллайдера
            // игрок для них не существует — они стоят столбом. При этом луч
            // клика по земле проходит сквозь триггеры насквозь, так что
            // персонаж по-прежнему не мешает игроку кликать себе под ноги.
            var playerBody = player.AddComponent<CapsuleCollider>();
            playerBody.center = Vector3.up;
            playerBody.height = 2f;
            playerBody.radius = 0.4f;
            playerBody.isTrigger = true;

            var playerTarget = player.AddComponent<Targetable>();
            playerTarget.Setup("Разбойник", Faction.Player);

            var playerHealth = player.AddComponent<Health>();
            playerHealth.Setup(200);

            var selector = player.AddComponent<TargetSelector>();
            selector.SetFaction(Faction.Player);

            player.AddComponent<PlayerInputRouter>();
            player.AddComponent<MeleeCombatant>();

            // Ресурсы разбойника: энергия копится сама, комбо-очки живут на цели.
            var energy = player.AddComponent<ResourcePool>();
            energy.Setup(ResourceType.Energy, 100, 10f);

            player.AddComponent<ComboPoints>();

            // Оружие. Кинжал — простейшее стартовое, урон 10. Когда появится
            // инвентарь, сюда будет писать надетый предмет, а боевой код
            // продолжит спрашивать урон в том же месте.
            var weapon = player.AddComponent<WeaponStats>();
            weapon.Equip("Кинжал", 10, 1.4f);

            // Уровень игрока и броня. Броня пока нулевая — её будет давать
            // экипировка, когда появится инвентарь.
            player.AddComponent<Experience>();
            player.AddComponent<StealthState>();

            // Сумка и экипировка. Порядок важен: Equipment в Awake ищет
            // Inventory, поэтому сумка должна появиться раньше.
            player.AddComponent<IsoRPG.Items.Inventory>();
            player.AddComponent<IsoRPG.Items.Equipment>();

            // Автоподбор добычи: клик по трупу оказался ненадёжным,
            // потому что тело теряет коллайдер при смерти.
            player.AddComponent<IsoRPG.Items.LootCollector>();

            // Стартовое снаряжение приходит через сумку и надевание — тем же
            // путём, что и добыча. Прописать оружие напрямую нельзя: экипировка
            // при старте увидит пустые руки и заменит его на «Кулаки».
            var gear = player.AddComponent<IsoRPG.Items.StartingGear>();
            var startingItems = new System.Collections.Generic.List<IsoRPG.Items.ItemDefinition>();

            var starterDagger = ItemsBuilder.LoadItem("I_RustyDagger");
            if (starterDagger != null) startingItems.Add(starterDagger);
            else Debug.LogError("[IsoRPG] Не найден стартовый кинжал — игрок останется с кулаками.");

            gear.Setup(startingItems, 0);
            EditorUtility.SetDirty(gear);

            // Отладочные клавиши. В готовой сборке компонента быть не должно —
            // выключается галочкой в инспекторе.
            player.AddComponent<DebugTools>();

            var playerDefense = player.AddComponent<DefenseStats>();
            playerDefense.Setup(1, 0);

            // Способности берём из ассетов. Если их ещё нет — создаём:
            // так сборка песочницы работает и на чистом проекте.
            var abilityAssets = RogueAbilitiesBuilder.Load();
            if (abilityAssets.Count == 0)
            {
                RogueAbilitiesBuilder.Build();
                abilityAssets = RogueAbilitiesBuilder.Load();
            }

            var book = player.AddComponent<AbilityBook>();
            book.Setup(abilityAssets);

            // Смерть игрока обрабатываем тем же компонентом, но тело не
            // убираем: пока нет воскрешения, исчезнувший игрок означал бы
            // сцену без героя и полную потерю управления.
            var death = player.AddComponent<DeathHandler>();
            var deathSo = new SerializedObject(death);
            deathSo.FindProperty("removeAfter").floatValue = 0f;
            deathSo.FindProperty("sinkBeforeRemoval").boolValue = false;
            deathSo.ApplyModifiedPropertiesWithoutUndo();

            // Боевой интерфейс висит на игроке: ему нужны и здоровье игрока,
            // и его выбранная цель, а оба живут здесь же.
            player.AddComponent<CombatHud>();
            player.AddComponent<CombatLogHud>();
            player.AddComponent<IsoRPG.Items.InventoryHud>();
            player.AddComponent<IsoRPG.Items.CharacterHud>();

            return player;
        }

        /// <summary>
        /// Манекены для битья: неподвижные цели с запасом здоровья.
        ///
        /// Специально не двигаются и не отвечают — на этом шаге проверяется
        /// только связка «выбрал цель, подошёл, ударил, здоровье убыло».
        /// Ответный удар и погоня появятся, когда эта часть будет надёжной.
        /// </summary>
        private static void CreateDummies()
        {
            var root = new GameObject("Monsters");

            // Разнесены по карте, чтобы радиусы агрессии не перекрывались:
            // иначе на первый же бой сбегаются все сразу, и понять поведение
            // одного монстра невозможно.
            // Три разных противника, чтобы было видно работу брони и уровней:
            // лёгкий, бронированный и средний.
            // Числа для удобства проверки, а не для баланса. Бой должен
            // укладываться в несколько ударов, иначе каждая проверка новой
            // механики превращается в минуту долбёжки.
            //
            // Настоящий баланс подберём, когда механики устоятся.
            var spots = new (Vector3 pos, string name, int hp, int level, int armor, string loot)[]
            {
                (new Vector3(  6f, 0f,   6f), "Бандит",     45, 1,  10, "LT_Bandit"),
                (new Vector3(-10f, 0f,  -6f), "Головорез", 110, 3,  45, "LT_Thug"),
                (new Vector3( 16f, 0f,  -8f), "Бродяга",    70, 2,  20, "LT_Drifter"),
            };

            var material = GetOrCreateMaterial("M_Dummy", DummyColor, smoothness: 0.1f);

            foreach (var (pos, name, hp, level, armor, loot) in spots)
            {
                // Корень стоит НА земле, а не в центре капсулы: навигационный
                // агент ищет сетку под своей точкой, и поднятый на метр монстр
                // может её не найти — тогда он просто стоит столбом.
                var monster = new GameObject(name);
                monster.transform.SetParent(root.transform);
                monster.transform.position = pos;

                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Body";
                visual.transform.SetParent(monster.transform);
                visual.transform.localPosition = Vector3.up;
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<Renderer>().sharedMaterial = material;

                // Коллайдер вешаем на корень: по нему игрок кликает, выбирая
                // цель, и по нему же монстров находят чужие сканирования.
                var body = monster.AddComponent<CapsuleCollider>();
                body.center = Vector3.up;
                body.height = 2f;
                body.radius = 0.5f;

                var targetable = monster.AddComponent<Targetable>();
                targetable.Setup(name, Faction.Hostile);

                var health = monster.AddComponent<Health>();
                health.Setup(hp);

                var defense = monster.AddComponent<DefenseStats>();
                defense.Setup(level, armor);

                var agent = monster.AddComponent<NavMeshAgent>();
                agent.radius = 0.45f;
                agent.height = 2f;
                agent.speed = 3.4f;          // медленнее игрока: от боя можно уйти
                agent.angularSpeed = 600f;
                agent.acceleration = 24f;
                agent.stoppingDistance = 0.1f;

                var selector = monster.AddComponent<TargetSelector>();
                selector.SetFaction(Faction.Hostile);

                monster.AddComponent<MeleeCombatant>();
                monster.AddComponent<MonsterBrain>();
                var lootSource = monster.AddComponent<IsoRPG.Items.LootSource>();
                var lootTable = ItemsBuilder.LoadTable(loot);

                // Молчаливо оставить монстра без добычи — худший исход:
                // выглядит как невезение с шансами, а на деле поломка.
                if (lootTable == null)
                    Debug.LogError("[IsoRPG] Не найдена таблица добычи " + loot +
                                   " — монстр останется без дропа.");

                lootSource.Setup(lootTable);
                EditorUtility.SetDirty(lootSource);

                monster.AddComponent<Respawner>();
                monster.AddComponent<StunReceiver>();
                monster.AddComponent<DeathHandler>();
                monster.AddComponent<OverheadHealthBar>();
            }
        }

        /// <summary>
        /// Ставит модель персонажа, если она собрана, иначе — капсулу-заглушку.
        ///
        /// Запасной вариант нужен не для красоты: пока модель не готова,
        /// сцена должна собираться и запускаться. Иначе один недостающий ассет
        /// блокирует всю работу над механикой.
        /// </summary>
        private static GameObject CreatePlayerVisual(Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player.prefab");

            if (prefab != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                visual.transform.SetParent(parent);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                Debug.Log("[IsoRPG] Персонаж: используется модель из Player.prefab.");
                return visual;
            }

            Debug.Log("[IsoRPG] Модель не найдена — ставлю капсулу. " +
                      "Собери персонажа через Tools/IsoRPG/Собрать персонажа.");

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(parent);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            ApplyMaterial(body, "M_Player", PlayerColor, smoothness: 0.15f);

            // Клинышек-нос, чтобы было видно, куда персонаж повёрнут.
            // Без него на капсуле поворот не читается вообще.
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            nose.transform.SetParent(parent);
            nose.transform.localPosition = new Vector3(0f, 1.1f, 0.42f);
            nose.transform.localScale = new Vector3(0.18f, 0.18f, 0.5f);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial("M_Marker", MarkerColor, smoothness: 0.2f, emissive: true);

            return body;
        }

        // ------------------------------------------------------------------
        // Камера
        // ------------------------------------------------------------------

        private static void CreateCamera(Transform target)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;

            // Сцена пустая, неба в ней нет: с режимом Skybox фон вышел бы
            // грязно-серым по умолчанию. Красим в цвет дымки, тогда дальний
            // край земли растворяется в фоне, а не обрывается линией.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0xB6, 0xBA, 0xA8, 0xFF);

            go.AddComponent<AudioListener>();

            var rig = go.AddComponent<IsoCameraRig>();
            rig.SetTarget(target);
        }

        /// <summary>
        /// Система событий интерфейса. Без неё кнопки в окнах не нажимаются
        /// вообще — они рисуются, но кликов не получают.
        ///
        /// Модуль ввода берём под новую систему: проект собран на ней, и
        /// старый StandaloneInputModule здесь просто не работает.
        /// </summary>
        private static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var go = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        // ------------------------------------------------------------------
        // Навигация
        // ------------------------------------------------------------------

        private static void BakeNavigation(GameObject ground)
        {
            var surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();

            Debug.Log("[IsoRPG] Навигационная сетка построена.");
        }

        // ------------------------------------------------------------------
        // Материалы
        // ------------------------------------------------------------------

        private static void ApplyMaterial(GameObject go, string name, Color color,
                                          float smoothness, bool emissive = false)
        {
            go.GetComponent<Renderer>().sharedMaterial =
                GetOrCreateMaterial(name, color, smoothness, emissive);
        }

        private static Material GetOrCreateMaterial(string name, Color color,
                                                    float smoothness, bool emissive = false)
        {
            EnsureFolder(MaterialsFolder);
            string path = MaterialsFolder + "/" + name + ".mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[IsoRPG] Не найден шейдер URP/Lit. Проект точно на URP?");
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);

            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.6f);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];                     // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

