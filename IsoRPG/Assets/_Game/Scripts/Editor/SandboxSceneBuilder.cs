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
        private static readonly Color GroundColor = new Color32(0x5E, 0x7C, 0x3E, 0xFF); // трава, приглушённая
        private static readonly Color RockColor = new Color32(0x8A, 0x8F, 0x94, 0xFF);   // холодный камень
        private static readonly Color PlayerColor = new Color32(0xC4, 0x62, 0x3A, 0xFF); // тёплый акцент
        private static readonly Color MarkerColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF); // отметка клика
        private static readonly Color DummyColor = new Color32(0x6E, 0x4A, 0x4A, 0xFF);  // манекен: тёплый тёмный, не путается с камнем

        private const float GroundSize = 130f;

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

            CreateAudio();
            GameObject ground = CreateGround();
            // Окружение вместо серых коробок. Коробки были нужны, пока
            // проверялось, что персонаж обходит препятствия; теперь ту же
            // роль играют стены руин и стволы деревьев.
            EnvironmentBuilder.Build(null);

            // Навигацию печём ДО создания персонажа: NavMeshAgent, поставленный
            // туда, где сетки ещё нет, ругается в консоль и не двигается.
            BakeNavigation(ground);

            GameObject marker = CreateDestinationMarker();
            GameObject player = CreatePlayer(marker);
            CreateDummies();
            CreateBossRoom();
            CreateQuestGiver();
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
                FindObjectsInactive.Include).Length;
            int gearCount = Object.FindObjectsByType<IsoRPG.Items.StartingGear>(
                FindObjectsInactive.Include).Length;

            Debug.Log($"[IsoRPG] Песочница собрана: {ScenePath}\n" +
                      $"  монстров с добычей: {lootCount}\n" +
                      $"  стартовое снаряжение: {gearCount}\n" +
                      $"  Если числа нулевые — код не успел скомпилироваться. " +
                      $"Дождись полоски внизу справа и собери заново.");
        }

        // ------------------------------------------------------------------
        // Свет
        // ------------------------------------------------------------------

        /// <summary>
        /// Ставит в сцену объект со звуком.
        ///
        /// Именно объект, а не вызов статики из сборщика: сборщик — код
        /// редактора, он отрабатывает при нажатии пункта меню, а статические
        /// поля обнуляются при запуске игры. Ссылка должна лежать в сцене.
        /// </summary>
        private static void CreateAudio()
        {
            var go = new GameObject("Audio");
            var setup = go.AddComponent<IsoRPG.Audio.AudioSetup>();

            var bank = SoundBankBuilder.Load();

            if (bank == null)
                Debug.LogWarning("[IsoRPG] Банк звуков не собран — игра будет беззвучной. " +
                                 "Прогони Tools/IsoRPG/Собрать банк звуков.");

            setup.Setup(bank, bank != null ? bank.music : null);
            EditorUtility.SetDirty(setup);
        }

        private static void CreateLighting()
        {
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();

            light.type = LightType.Directional;

            // Вечер, солнце у горизонта. Угол ниже дневного вдвое: тени
            // вытягиваются через всю площадку и дают объём там, где геометрия
            // плоская. Это половина «дороговизны» картинки, и стоит она один
            // поворот.
            //
            // И главное: при дневном свете факелы и свечи — просто модели.
            // Огонь виден только когда вокруг темнее его.
            go.transform.rotation = Quaternion.Euler(21f, 152f, 0f);

            light.color = new Color32(0xFF, 0xC9, 0x8A, 0xFF); // закатный, оранжевый
            light.intensity = 0.95f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.82f;

            // Тени холодные, свет тёплый — контраст, на котором держится
            // вечернее освещение. Если и то и другое тёплое, получается не
            // вечер, а жёлтый фильтр.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(0x5A, 0x74, 0xA6, 0xFF);     // сумеречное небо
            RenderSettings.ambientEquatorColor = new Color32(0x62, 0x63, 0x70, 0xFF);
            RenderSettings.ambientGroundColor = new Color32(0x33, 0x30, 0x2E, 0xFF);

            // Дымка вдали прячет край локации без забора — приём с референса.
            // К вечеру она гуще и холоднее: это же и добавляет глубины.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color32(0x6E, 0x76, 0x8C, 0xFF);
            RenderSettings.fogStartDistance = 34f;
            RenderSettings.fogEndDistance = 96f;
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

            // Центр главного зала, а не центр координат.
            //
            // Карта руин выросла вправо под склеп, и её середина уехала
            // на двадцать с лишним метров: прежний ноль оказался вплотную
            // к восточной стене. Такие координаты надо брать от планировки,
            // а не от начала координат, иначе они врут при каждой правке
            // карты.
            player.transform.position = RuinsLayout.HallCentre + new Vector3(2f, 0f, -3f);

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

            // Стартовое снаряжение приходит через сумку и надевание — тем же
            // путём, что и добыча. Прописать оружие напрямую нельзя: экипировка
            // при старте увидит пустые руки и заменит его на «Кулаки».
            var gear = player.AddComponent<IsoRPG.Items.StartingGear>();
            var startingItems = new System.Collections.Generic.List<IsoRPG.Items.ItemDefinition>();

            var starterDagger = ItemsBuilder.LoadItem("I_RustyDagger");

            if (starterDagger != null)
            {
                // Два клинка: класс дерётся парой, и анимация удара
                // рассчитана на две руки. С одним кинжалом левая рука машет
                // пустой, и удар читается как размахивание.
                startingItems.Add(starterDagger);
                startingItems.Add(starterDagger);
            }
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
            book.Setup(abilityAssets, RogueAbilitiesBuilder.LoadStealth());

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
            SetupPreview(player.AddComponent<IsoRPG.Items.CharacterPreview>());

            player.AddComponent<IsoRPG.Items.InventoryHud>();
            var characterHud = player.AddComponent<IsoRPG.Items.CharacterHud>();
            SetupSlotHints(characterHud);

            // Оружие в руках. Ставится после экипировки: компонент читает её
            // состояние сразу при включении.
            player.AddComponent<IsoRPG.Items.WeaponVisual>();

            // Слой ставим в самом конце сборки персонажа: к этому моменту
            // модель и оружие уже на месте, и слой достанется всему разом.
            // Портрет игрока: он же модель, которой играем.
            var playerTargetable = player.GetComponent<Targetable>();
            if (playerTargetable != null)
            {
                playerTargetable.SetPortrait(PortraitRenderer.Load("Rogue_Hooded"));
                EditorUtility.SetDirty(playerTargetable);
            }

            ApplySilhouetteLayer(player);
            player.AddComponent<IsoRPG.Audio.FootstepPlayer>();

            // Уровень восстанавливает здоровье, еда лечит сидя, пробел
            // подбрасывает. Всё на игроке: им нужны его здоровье и модель.
            player.AddComponent<LevelUpRestore>();
            player.AddComponent<IsoRPG.Items.FoodConsumer>();
            player.AddComponent<JumpGesture>();

            // Окно добычи. На игроке, потому что ему нужны и сумка, и
            // положение персонажа: окно закрывается, когда игрок отходит.
            player.AddComponent<IsoRPG.Items.LootWindow>();

            // Квесты: журнал считает прогресс по сумке, панель показывает
            // цели, окно ведёт разговор. Все трое на игроке — им нужны его
            // сумка, опыт и положение.
            player.AddComponent<IsoRPG.Quests.QuestLog>();
            player.AddComponent<IsoRPG.Quests.QuestTracker>();
            player.AddComponent<IsoRPG.Quests.DialogueWindow>();

            // Подсказка одна на всю игру и ставится первой: остальные
            // окна обращаются к ней при наведении.
            player.AddComponent<IsoRPG.UI.Tooltip>();
            player.AddComponent<IsoRPG.UI.QuestJournal>();

            // Таланты: книга держит вложенное и раздаёт прибавки бою,
            // окно только показывает. Книга первой — окно её ищет.
            var talents = player.AddComponent<IsoRPG.Progression.TalentBook>();
            var tree = TalentsBuilder.LoadAll();

            // Пусто — значит ассетов ещё нет. Создаём сами: иначе дерево
            // молча соберётся пустым, и виноват будет порядок пунктов меню.
            if (tree.Count == 0)
            {
                TalentsBuilder.Build();
                tree = TalentsBuilder.LoadAll();
            }

            talents.Setup(tree);
            EditorUtility.SetDirty(talents);

            player.AddComponent<IsoRPG.Progression.TalentStats>();
            player.AddComponent<IsoRPG.UI.TalentWindow>();
            player.AddComponent<IsoRPG.UI.SettingsWindow>();

            SetupHudBar(player.AddComponent<IsoRPG.UI.HudBar>());

            // Полоска над головой у игрока тоже. Панель вверху экрана
            // отвечает на вопрос «сколько у меня осталось», а полоска над
            // персонажем — на другой: «попали по мне только что или нет».
            // В бою взгляд держится на персонаже, а не на углу экрана.
            player.AddComponent<OverheadHealthBar>();

            // Смерть и возвращение. Возрождатель тот же, что у монстров, но
            // по команде: игрок встаёт кнопкой, а не сам через полминуты.
            var playerRespawn = player.AddComponent<Respawner>();
            playerRespawn.SetManualOnly(true);
            EditorUtility.SetDirty(playerRespawn);

            // Импорт настраиваем до загрузки: текстура, не размеченная как
            // спрайт, не находится по пути — молча, без единой ошибки.
            IconBinder.PrepareSprites("Assets/_Game/Art/UI");

            var deathScreen = player.AddComponent<IsoRPG.UI.DeathScreen>();
            deathScreen.SetupArt(AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Game/Art/UI/DeathScreen.png"));
            EditorUtility.SetDirty(deathScreen);

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
            var bankForVoices = SoundBankBuilder.Load();

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
            // Разные виды нежити, а не один вид разного роста. Рост под
            // уровень был костылём той поры, когда модель была одна на всех:
            // ни в WoW, ни в Albion габарит не значит силу — он значит, кто
            // перед тобой. Уровень читается из полоски и цифры.
            // Оружие — не украшение, а подсказка о противнике. Щит виден
            // раньше, чем полоска здоровья: по нему понятно, что бить будешь
            // долго. Лук говорит, что подходить придётся под выстрелами.
            var spots = new (Vector3 pos, string name, int hp, int level, int armor,
                             string loot, string prefab, string rightHand, string leftHand, bool ranged)[]
            {
                (new Vector3(  6f, 0f,   6f), "Скелет-прислужник",  45, 1, 10, "LT_Bandit",  "Skeleton_Minion",
                 "Skeleton_Blade", "Skeleton_Shield_Small_A", false),

                (new Vector3(-10f, 0f,  -6f), "Скелет-воин",       110, 3, 45, "LT_Thug",    "Skeleton_Warrior",
                 "Skeleton_Axe", "Skeleton_Shield_Large_A", false),

                (new Vector3( 16f, 0f,  -8f), "Костяной лучник",    70, 2, 20, "LT_Drifter", "Skeleton_Rogue",
                 "bow_withString", null, true),

                // Свита в склепе. Стоят по бокам от владыки: игрок входит и
                // сразу видит троих, а не открывает их по одному.
                (RuinsLayout.CryptCentre + new Vector3(-6f, 0f, -4f), "Страж склепа",      130, 3, 50, "LT_Thug",    "Skeleton_Warrior",
                 "Skeleton_Axe", "Skeleton_Shield_Large_B", false),

                (RuinsLayout.CryptCentre + new Vector3( 6f, 0f, -4f), "Лучник склепа",      85, 3, 25, "LT_Drifter", "Skeleton_Rogue",
                 "bow_withString", null, true),
            };

            var material = GetOrCreateMaterial("M_Dummy", DummyColor, smoothness: 0.1f);

            foreach (var (pos, name, hp, level, armor, loot, prefab, rightHand, leftHand, ranged) in spots)
            {
                // Корень стоит НА земле, а не в центре капсулы: навигационный
                // агент ищет сетку под своей точкой, и поднятый на метр монстр
                // может её не найти — тогда он просто стоит столбом.
                var monster = new GameObject(name);
                monster.transform.SetParent(root.transform);
                monster.transform.position = pos;

                CreateMonsterVisual(monster.transform, prefab, material);

                // Коллайдер вешаем на корень: по нему игрок кликает, выбирая
                // цель, и по нему же монстров находят чужие сканирования.
                var body = monster.AddComponent<CapsuleCollider>();
                body.center = Vector3.up;
                body.height = 2f;
                body.radius = 0.5f;

                var targetable = monster.AddComponent<Targetable>();
                targetable.Setup(name, Faction.Hostile);
                targetable.SetOverheadHeight(2.2f);

                // Портрет берётся по имени модели: один источник для того,
                // что игрок видит в мире и на панели цели.
                targetable.SetPortrait(PortraitRenderer.Load(prefab));

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

                if (ranged)
                {
                    var archer = monster.AddComponent<RangedCombatant>();
                    archer.Setup(LoadWeapon("arrow_bow"));
                    EditorUtility.SetDirty(archer);
                }
                else
                {
                    monster.AddComponent<MeleeCombatant>();
                }
                monster.AddComponent<MonsterBrain>();

                // Тот же водитель анимаций, что у игрока: он зависит только от
                // навигационного агента и аниматора в детях, а они у монстра
                // есть. Ходьба, удар и смерть заработают сами — боевой код уже
                // дёргает его через проверку на null.
                monster.AddComponent<CharacterAnimatorDriver>();

                var arms = monster.AddComponent<HandAttachments>();
                arms.Setup(LoadWeapon(rightHand), LoadWeapon(leftHand));
                EditorUtility.SetDirty(arms);
                var lootSource = monster.AddComponent<IsoRPG.Items.LootSource>();
                var lootTable = ItemsBuilder.LoadTable(loot);

                // Молчаливо оставить монстра без добычи — худший исход:
                // выглядит как невезение с шансами, а на деле поломка.
                if (lootTable == null)
                    Debug.LogError("[IsoRPG] Не найдена таблица добычи " + loot +
                                   " — монстр останется без дропа.");

                lootSource.Setup(lootTable);
                lootSource.SetupModels(
                    LoadDungeonModel("box_small"),
                    AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/_Game/Art/Materials/M_Silhouette_Ally.mat"));
                EditorUtility.SetDirty(lootSource);

                monster.AddComponent<Respawner>();
                monster.AddComponent<StunReceiver>();
                monster.AddComponent<DeathHandler>();
                monster.AddComponent<OverheadHealthBar>();

                ApplySilhouetteLayer(monster);
                monster.AddComponent<IsoRPG.Audio.FootstepPlayer>();

                // Голос нежити. Скелет, который поскрипывает за стеной,
                // существует для игрока ещё до того, как покажется.
                var voice = monster.AddComponent<IsoRPG.Audio.AmbientVoice>();
                if (bankForVoices != null) voice.Setup(bankForVoices.boneVoice);
                EditorUtility.SetDirty(voice);
            }
        }

        /// <summary>
        /// Включает силуэт сквозь препятствия.
        ///
        /// Материал добавляется вторым на каждый рендерер персонажа — это
        /// делает сам компонент в момент запуска, когда модель уже собрана.
        /// </summary>
        private static void ApplySilhouetteLayer(GameObject go)
        {
            // Цвет по стороне: свой зелёный, чужой красный.
            var targetable = go.GetComponent<Targetable>();
            bool enemy = targetable != null && targetable.Faction == Faction.Hostile;

            var material = AssetDatabase.LoadAssetAtPath<Material>(
                enemy ? "Assets/_Game/Art/Materials/M_Silhouette_Enemy.mat"
                      : "Assets/_Game/Art/Materials/M_Silhouette_Ally.mat");

            if (material == null)
            {
                Debug.LogWarning("[IsoRPG] Нет материала силуэта — прогони " +
                                 "Tools/IsoRPG/Настроить силуэты.");
                return;
            }

            var visual = go.AddComponent<SilhouetteVisual>();
            visual.Setup(material);
            EditorUtility.SetDirty(visual);
        }

        /// <summary>
        /// Раздаёт окну персонажа силуэты пустых слотов.
        ///
        /// Пустой квадрат ничего не говорит игроку, а силуэт шлема или сапога
        /// объясняет назначение слота без единого слова и без обучения.
        /// </summary>
        private static void SetupSlotHints(IsoRPG.Items.CharacterHud hud)
        {
            var pairs = new (IsoRPG.Items.EquipSlot slot, string file)[]
            {
                (IsoRPG.Items.EquipSlot.Head, "Slot_Head"),
                (IsoRPG.Items.EquipSlot.Chest, "Slot_Chest"),
                (IsoRPG.Items.EquipSlot.Hands, "Slot_Hands"),
                (IsoRPG.Items.EquipSlot.Legs, "Slot_Legs"),
                (IsoRPG.Items.EquipSlot.Feet, "Slot_Feet"),
                (IsoRPG.Items.EquipSlot.MainHand, "Slot_MainHand"),
                (IsoRPG.Items.EquipSlot.OffHand, "Slot_OffHand"),
                (IsoRPG.Items.EquipSlot.Ring, "Slot_Ring"),

                // Второе кольцо носит тот же силуэт: это тот же слот по сути,
                // и рисовать «кольцо номер два» было бы странно.
                (IsoRPG.Items.EquipSlot.Ring2, "Slot_Ring"),
                (IsoRPG.Items.EquipSlot.Necklace, "Slot_Necklace"),
            };

            foreach (var (slot, file) in pairs)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/_Game/Art/UI/Icons/Slots/" + file + ".png");

                if (sprite == null)
                    Debug.LogWarning("[IsoRPG] Нет силуэта слота " + file);

                hud.SetupSlotHints(slot, sprite);
            }

            EditorUtility.SetDirty(hud);
        }

        /// <summary>
        /// Иконки кнопок нижнего ряда. Не найденная иконка не ломает ряд:
        /// кнопка останется квадратом и продолжит открывать своё окно.
        /// </summary>
        private static void SetupHudBar(IsoRPG.UI.HudBar bar)
        {
            // Импорт настраивается до загрузки: текстура, не размеченная
            // как спрайт, не находится по пути — молча, без единой ошибки.
            IconBinder.PrepareSprites("Assets/_Game/Art/UI/Icons/Buttons");

            bar.SetupIcons(
                LoadButtonIcon("UI_Bag"),
                LoadButtonIcon("UI_Character"),
                LoadButtonIcon("UI_Journal"),
                LoadButtonIcon("UI_Talents"),
                LoadButtonIcon("UI_Settings"));

            EditorUtility.SetDirty(bar);
        }

        private static Sprite LoadButtonIcon(string fileName)
        {
            string path = "Assets/_Game/Art/UI/Icons/Buttons/" + fileName + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null) Debug.LogWarning("[IsoRPG] Нет иконки кнопки " + path);

            return sprite;
        }

        /// <summary>
        /// Витрине нужны те же модель и контроллер, что и герою: она
        /// показывает его самого, а не похожего персонажа.
        /// </summary>
        private static void SetupPreview(IsoRPG.Items.CharacterPreview preview)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/Player.prefab");

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_Game/Art/KayKit/Controllers/AC_Rogue.controller");

            if (model == null)
            {
                Debug.LogWarning("[IsoRPG] Нет Player.prefab — окно снаряжения будет без модели.");
                return;
            }

            preview.Setup(model, controller);
            EditorUtility.SetDirty(preview);
        }

        /// <summary>
        /// Костяной владыка и его сундук.
        ///
        /// Отдельно от прочих монстров, потому что отличается не числами, а
        /// устройством: своя таблица добычи, гарантированный ключ, магическая
        /// атака вместо удара. Затолкать это в общий список кортежей значило
        /// бы завести в нём пять полей ради одного существа.
        ///
        /// Баланс задуман так: игрок первого уровня с ржавым кинжалом сюда
        /// приходит и умирает — и это правильный ответ игры. Пройденный квест
        /// даёт Клык Тени и уровень, и с ними бой становится трудным, но
        /// выигрышным. Никаких запретов и невидимых стен: только числа.
        /// </summary>
        private static void CreateBossRoom()
        {
            var root = GameObject.Find("Monsters");
            if (root == null) root = new GameObject("Monsters");

            CreateBoss(root.transform, GetOrCreateMaterial("M_Dummy", DummyColor, smoothness: 0.1f));
        }

        private static void CreateBoss(Transform root, Material material)
        {
            var at = RuinsLayout.CryptCentre;

            var boss = new GameObject("Костяной владыка");
            boss.transform.SetParent(root);
            boss.transform.position = at;

            CreateMonsterVisual(boss.transform, "Skeleton_Mage", material);

            var body = boss.AddComponent<CapsuleCollider>();
            body.center = Vector3.up;
            body.height = 2f;
            body.radius = 0.5f;

            var targetable = boss.AddComponent<Targetable>();
            targetable.Setup("Костяной владыка", Faction.Hostile);
            targetable.SetPortrait(PortraitRenderer.Load("Skeleton_Mage"));
            EditorUtility.SetDirty(targetable);

            var health = boss.AddComponent<Health>();
            health.Setup(420);
            EditorUtility.SetDirty(health);

            var defense = boss.AddComponent<DefenseStats>();
            defense.Setup(4, 60);
            EditorUtility.SetDirty(defense);

            var agent = boss.AddComponent<NavMeshAgent>();
            agent.speed = 2.6f;
            agent.angularSpeed = 520f;
            agent.acceleration = 30f;
            agent.stoppingDistance = 0.1f;
            agent.radius = 0.4f;

            var selector = boss.AddComponent<TargetSelector>();
            selector.SetFaction(Faction.Hostile);

            // Бьёт заклинанием: посох в руках без магии читается как дубина,
            // которой почему-то машут издалека.
            var caster = boss.AddComponent<RangedCombatant>();
            caster.Setup(SpellBoltBuilder.Load());
            EditorUtility.SetDirty(caster);

            boss.AddComponent<MonsterBrain>();
            boss.AddComponent<CharacterAnimatorDriver>();

            var arms = boss.AddComponent<HandAttachments>();
            arms.Setup(LoadWeapon("Skeleton_Staff"), null);
            EditorUtility.SetDirty(arms);

            var loot = boss.AddComponent<IsoRPG.Items.LootSource>();
            loot.Setup(ItemsBuilder.LoadTable("LT_Thug"));

            // Ключ падает наверняка и только раз: он не награда, а следующий
            // шаг. Второй такой же ключ не открыл бы ничего.
            loot.SetupUnique(ItemsBuilder.LoadItem("I_CryptKey"),
                             "С пояса владыки свалился тяжёлый ключ.");

            loot.SetupModels(
                LoadDungeonModel("box_small"),
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Game/Art/Materials/M_Silhouette_Ally.mat"));
            EditorUtility.SetDirty(loot);

            boss.AddComponent<DeathHandler>();
            boss.AddComponent<StunReceiver>();
            boss.AddComponent<OverheadHealthBar>();

            var respawn = boss.AddComponent<Respawner>();
            EditorUtility.SetDirty(respawn);

            ApplySilhouetteLayer(boss);

            CreateChest(root, RuinsLayout.CryptCentre + new Vector3(0f, 0f, 6f));
        }

        /// <summary>Сундук в глубине склепа. Заперт ключом с владыки.</summary>
        private static void CreateChest(Transform root, Vector3 at)
        {
            var model = LoadDungeonModel("chest_gold");

            var chest = model != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(model)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            chest.name = "Сундук владыки";
            chest.transform.SetParent(root);
            chest.transform.position = at;
            chest.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Коллайдер на корне: по нему кликают. Модель набора своего не
            // несёт, а без него сундук нельзя ни выбрать, ни открыть.
            var box = chest.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.35f, 0f);
            box.size = new Vector3(1.1f, 0.8f, 0.8f);

            var loot = chest.AddComponent<IsoRPG.Items.LootSource>();
            loot.Setup(ItemsBuilder.LoadTable("LT_Chest"));
            loot.SetupUnique(ItemsBuilder.LoadItem("I_RingOfTheBoneLord"),
                             "В сундуке лежит перстень с фиолетовым камнем.");
            loot.SetupModels(
                LoadDungeonModel("box_small"),
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Game/Art/Materials/M_Silhouette_Ally.mat"));
            EditorUtility.SetDirty(loot);

            // Россыпь монет вокруг: подсказка «тут награда» ещё до того, как
            // игрок разглядит знак над крышкой. Небрежно, врассыпную — ровный
            // круг читался бы как выложенный кем-то узор.
            var coins = new[] { "coin_stack_small", "coin_stack_medium", "coin" };

            for (int i = 0; i < 5; i++)
            {
                var coinModel = LoadDungeonModel(coins[i % coins.Length]);
                if (coinModel == null) break;

                float angle = 40f + i * 62f;
                float radius = 0.9f + (i % 3) * 0.35f;

                var spot = at + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;

                var coin = (GameObject)PrefabUtility.InstantiatePrefab(coinModel);
                coin.transform.SetParent(root);
                coin.transform.position = spot;
                coin.transform.rotation = Quaternion.Euler(0f, angle * 2.3f, 0f);
            }

            var lock2 = chest.AddComponent<IsoRPG.Items.TreasureChest>();
            lock2.Setup(ItemsBuilder.LoadItem("I_CryptKey"));
            EditorUtility.SetDirty(lock2);

            // Знак появляется, только когда ключ уже в сумке: до этого он был
            // бы подсказкой к загадке, которую игра ещё не задала.
            var mark = chest.AddComponent<IsoRPG.Items.ChestMarker>();
            mark.Setup(ItemsBuilder.LoadItem("I_CryptKey"));
            EditorUtility.SetDirty(mark);
        }

        /// <summary>Модель из набора подземелья: кучки золота, мешочки.</summary>
        private static GameObject LoadDungeonModel(string fileName)
        {
            string path = "Assets/_Game/Art/KayKit/Dungeon/" + fileName + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (model == null)
                Debug.LogWarning("[IsoRPG] Не найдена модель " + path);

            return model;
        }

        /// <summary>
        /// Модель оружия из набора. Пустое имя — рука свободна, это законно.
        /// </summary>
        private static GameObject LoadWeapon(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            string path = "Assets/_Game/Art/KayKit/Weapons/" + fileName + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (model == null)
                Debug.LogWarning("[IsoRPG] Не найдена модель оружия " + path + " — рука останется пустой.");

            return model;
        }

        /// <summary>
        /// Ставит NPC с квестом рядом с точкой появления игрока.
        ///
        /// Рядом, а не где-то в мире: первый квест должен найтись сам, без
        /// поисков. Игрок появляется, видит знак над головой в двух шагах —
        /// и дальше игра объясняет себя сама.
        /// </summary>
        private static void CreateQuestGiver()
        {
            var quest = QuestBuilder.LoadFirst();

            if (quest == null)
            {
                Debug.LogWarning("[IsoRPG] Квест не создан — прогони " +
                                 "Tools/IsoRPG/Создать квесты. NPC будет молчать.");
            }

            var go = new GameObject("Старый оружейник");
            // В стороне от места появления игрока, но в том же зале:
            // заказчик должен попадаться на глаза, а не искаться.
            go.transform.position = RuinsLayout.HallCentre + new Vector3(-6f, 0f, 4f);

            // Модель мирного человека, а не скелета: заказчик должен
            // отличаться от того, кого он просит убивать.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Art/KayKit/Characters/Mage.fbx");

            if (model != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.transform.SetParent(go.transform, false);
                visual.transform.localRotation = Quaternion.identity;

                // Анимация покоя. Неподвижная модель читается как статуя, и
                // никакой знак над головой этого не исправит: живым делает
                // движение, а не метка.
                var animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.AddComponent<Animator>();

                var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                    "Assets/_Game/Art/KayKit/Controllers/AC_Rogue.controller");

                if (controller != null)
                {
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                }
                else
                {
                    Debug.LogWarning("[IsoRPG] Нет контроллера анимаций — NPC будет неподвижен.");
                }
            }
            else
            {
                Debug.LogWarning("[IsoRPG] Модель NPC не найдена — ставлю капсулу.");

                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(go.transform, false);
                body.transform.localPosition = Vector3.up;
                Object.DestroyImmediate(body.GetComponent<Collider>());
            }

            // Коллайдер для клика. Триггер, чтобы не мешать навигации: NPC
            // стоит на проходе, и твёрдое тело заставляло бы его обходить.
            var box = go.AddComponent<CapsuleCollider>();
            box.isTrigger = true;
            box.center = Vector3.up;
            box.height = 2f;
            box.radius = 0.5f;

            // Изначально смотрит на точку появления игрока: первое, что тот
            // увидит, — обращённое к нему лицо, а не спина.
            go.transform.rotation = Quaternion.LookRotation(
                (Vector3.zero - go.transform.position).normalized);

            var giver = go.AddComponent<IsoRPG.Quests.QuestGiver>();
            giver.Setup(quest);
            EditorUtility.SetDirty(giver);

            // Силуэт: NPC стоит в комнате, и стена может его закрыть.
            var silhouette = go.AddComponent<SilhouetteVisual>();
            silhouette.Setup(AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Game/Art/Materials/M_Silhouette_Ally.mat"));
            EditorUtility.SetDirty(silhouette);
        }

        /// <summary>
        /// Ставит модель противника по имени префаба, иначе — капсулу.
        ///
        /// Запасной вариант нужен не для красоты: пока модели не собраны,
        /// сцена должна запускаться. Иначе один недостающий ассет блокирует
        /// всю работу над механикой.
        /// </summary>
        private static void CreateMonsterVisual(Transform parent, string prefabName, Material fallback)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Game/Prefabs/" + prefabName + ".prefab");

            if (prefab != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                visual.transform.SetParent(parent);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                return;
            }

            Debug.LogWarning("[IsoRPG] Нет префаба " + prefabName + " — ставлю капсулу. " +
                             "Собери их через Tools/IsoRPG/Собрать персонажей KayKit.");

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(parent);
            body.transform.localPosition = Vector3.up;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = fallback;
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
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

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

