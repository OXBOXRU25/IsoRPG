using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using IsoRPG.Combat;
using IsoRPG.Player;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Доносит герою арены весь игровой набор: выбор цели, бой, сумку,
    /// характеристики, квесты и окна.
    ///
    /// Зачем понадобилось. Полный набор игрока вешал только строитель СТАРОЙ
    /// песочницы (<see cref="SandboxSceneBuilder"/>), а арену собирал другой
    /// строитель — и герой на ней остался с десятью компонентами: ходьба,
    /// прыжок, анимация, миникарта, журнал, подсказки, панель кнопок,
    /// настройки, шаги, прижим к грунту. Ни выбора цели, ни боя, ни сумки.
    ///
    /// Отсюда жалоба «клик не выделяет ни моба, ни NPC», которую мы искали в
    /// коллайдерах мобов и в слоях: мышь на арене не читал НИКТО. Компонент
    /// <see cref="PlayerInputRouter"/> — единственное место, где живёт клик, —
    /// в файлах обеих арен не встречался ни разу. Размер капсулы у волка тут
    /// был ни при чём: не было того, кто пускает луч.
    ///
    /// Задание идемпотентно: ставит только отсутствующее и ничего не
    /// перенастраивает заново. Прогонять можно сколько угодно раз, в том
    /// числе после переезда на новую арену.
    ///
    /// Эталон набора — сборка игрока в песочнице. Если там что-то добавится,
    /// добавить и сюда: две копии одного списка разъедутся молча, и разъедутся
    /// худшим способом — на арене окно откроется, а данных за ним не будет.
    /// </summary>
    public static class PlayerKit
    {
        /// <summary>Что добавили за прогон. Для отчёта в конце.</summary>
        private static readonly List<string> added = new List<string>();

        public static void Apply()
        {
            added.Clear();

            // Ищем среди неактивных тоже: герой мог быть выключен заданием,
            // и GameObject.Find его тогда молча не находит — прогон отчитался
            // бы «героя нет» на сцене, где он есть.
            var player = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,
                                                             FindObjectsSortMode.None)
                               .FirstOrDefaultNamed("Player");

            if (player == null)
            {
                Debug.LogError("[IsoRPG] Героя «Player» в сцене нет — набор ставить некому.");
                return;
            }

            Debug.Log("[IsoRPG] Набор игрока: начинаю с объекта «" + player.name + "».");

            Body(player);
            Combat(player);
            Bag(player);
            Windows(player);
            Quests(player);
            Death(player);

            // Силуэт за препятствиями. Ставится после Targetable: материал
            // выбирается по стороне, а до Targetable сторона неизвестна.
            if (player.GetComponent<SilhouetteVisual>() == null)
            {
                SandboxSceneBuilder.ApplySilhouetteLayer(player);
                added.Add("SilhouetteVisual");
            }

            EditorUtility.SetDirty(player);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Набор игрока: добавлено компонентов " + added.Count +
                      (added.Count > 0 ? " — " + string.Join(", ", added) : "."));

            Check(player);
        }

        // ------------------------------------------------------------------
        // Тело: коллайдер, метка цели, здоровье, движение
        // ------------------------------------------------------------------

        private static void Body(GameObject player)
        {
            // Коллайдер-триггер. Без него монстры игрока не видят вовсе:
            // они ищут врагов физическим сканированием и стоят столбом.
            // Триггером — чтобы не мешать лучу клика.
            if (player.GetComponent<Collider>() == null)
            {
                var body = player.AddComponent<CapsuleCollider>();
                body.center = Vector3.up;
                body.height = 2f;
                body.radius = 0.4f;
                body.isTrigger = true;
                added.Add("CapsuleCollider");
            }

            var targetable = Ensure<Targetable>(player);

            if (targetable != null)
            {
                // Имя менять НЕЛЬЗЯ: панель героя ищет его портрет по строке
                // «Разбойник» (CombatHud → Portraits). Другое имя оставит
                // круг портрета пустым, причём молча.
                targetable.Setup("Разбойник", Faction.Player);
                targetable.SetPortrait(PortraitRenderer.Load("Rogue_Hooded"));
                EditorUtility.SetDirty(targetable);
            }

            var health = Ensure<Health>(player);
            if (health != null) { health.Setup(200); EditorUtility.SetDirty(health); }

            // Ход к цели: роутер ведёт героя к собеседнику, сундуку и мешку,
            // когда до них не дотянуться. Отметка клика не нужна — поле
            // необязательное, компонент проверяет его на null.
            Ensure<ClickToMoveController>(player);
        }

        // ------------------------------------------------------------------
        // Бой и выбор цели
        // ------------------------------------------------------------------

        private static void Combat(GameObject player)
        {
            // Хранилище цели — раньше роутера: тот его требует (RequireComponent),
            // и без него компонент ввода не добавится вовсе.
            var selector = Ensure<TargetSelector>(player);
            if (selector != null) { selector.SetFaction(Faction.Player); EditorUtility.SetDirty(selector); }

            // Вот он — единственный читатель мыши. Кольцо под ногами цели
            // (TargetRing), расталкивание (BodySpace) и ходьба на клавишах
            // он вешает себе сам при старте игры.
            Ensure<PlayerInputRouter>(player);

            // Наведение: подсказка под курсором. Слои и дальность берёт у
            // роутера, чтобы подсказка не обещала того, чего клик не сделает.
            Ensure<HoverInspector>(player);

            var melee = Ensure<MeleeCombatant>(player);

            if (melee != null)
            {
                // Герой за целью НЕ бегает. Это не настройка вкуса, а
                // следствие схемы управления: ходьба живёт на WASD, левая
                // кнопка только выбирает цель (решение Павла от 27.08.2026).
                //
                // С включённой погоней получался «магнит»: выделил моба — и
                // персонажа тянет к нему каждый кадр, отойти нельзя, помогает
                // только выбор другой цели. Монстров это не касается: у них
                // своя галочка и она остаётся включённой, иначе они перестанут
                // догонять игрока.
                //
                // Ставим безусловно, а не только при добавлении: компонент мог
                // уже лежать в сцене с прежним значением, и правка умолчания
                // в коде его бы не тронула.
                var so = new SerializedObject(melee);
                so.FindProperty("chaseTarget").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(melee);
            }

            var energy = Ensure<ResourcePool>(player);
            if (energy != null) { energy.Setup(ResourceType.Energy, 100, 10f); EditorUtility.SetDirty(energy); }

            Ensure<ComboPoints>(player);

            var weapon = Ensure<WeaponStats>(player);
            if (weapon != null) { weapon.Equip("Кинжал", 10, 1.4f); EditorUtility.SetDirty(weapon); }

            Ensure<Experience>(player);
            Ensure<StealthState>(player);

            var defense = Ensure<DefenseStats>(player);
            if (defense != null) { defense.Setup(1, 0); EditorUtility.SetDirty(defense); }

            var book = Ensure<AbilityBook>(player);

            if (book != null)
            {
                var abilities = RogueAbilitiesBuilder.Load();

                if (abilities.Count == 0)
                {
                    RogueAbilitiesBuilder.Build();
                    abilities = RogueAbilitiesBuilder.Load();
                }

                book.Setup(abilities, RogueAbilitiesBuilder.LoadStealth());
                EditorUtility.SetDirty(book);
            }

            // Смерть игрока: тело не убираем. Пока нет воскрешения, исчезнувший
            // герой означал бы сцену без управления.
            var death = Ensure<DeathHandler>(player);

            if (death != null)
            {
                var so = new SerializedObject(death);
                so.FindProperty("removeAfter").floatValue = 0f;
                so.FindProperty("sinkBeforeRemoval").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Ensure<LevelUpRestore>(player);

            // Ускорение: спринт и всё, что будет разгонять героя впредь.
            Ensure<SpeedBoost>(player);

            // Как выглядит скрытность: полупрозрачный герой.
            Ensure<StealthVisual>(player);
        }

        // ------------------------------------------------------------------
        // Сумка, экипировка, оружие в руках
        // ------------------------------------------------------------------

        private static void Bag(GameObject player)
        {
            // Порядок обязателен: Equipment при старте ищет Inventory.
            Ensure<IsoRPG.Items.Inventory>(player);
            Ensure<IsoRPG.Items.Equipment>(player);

            var gear = Ensure<IsoRPG.Items.StartingGear>(player);

            if (gear != null)
            {
                var items = new List<IsoRPG.Items.ItemDefinition>();
                var dagger = ItemsBuilder.LoadItem("I_RustyDagger");

                if (dagger != null)
                {
                    // Два клинка: класс дерётся парой, и анимация удара
                    // рассчитана на две руки.
                    items.Add(dagger);
                    items.Add(dagger);
                }
                else Debug.LogWarning("[IsoRPG] Стартовый кинжал не найден — герой останется с кулаками.");

                gear.Setup(items, 0);
                EditorUtility.SetDirty(gear);
            }

            // Оружие в руках — после экипировки: компонент читает её при включении.
            Ensure<IsoRPG.Items.WeaponVisual>(player);

            // Затягивание ран вне боя: пять в секунду.
            Ensure<IsoRPG.Combat.HealthRegen>(player);

            // Редкие позы ожидания, когда герой долго стоит.
            Ensure<IsoRPG.Player.IdleFidget>(player);
            Ensure<IsoRPG.Items.FoodConsumer>(player);

            // Размер сумки задаём явно: у компонента, уже лежащего в сцене,
            // сохранено своё значение, и правка умолчания в коде его не меняет.
            var inventory = player.GetComponent<IsoRPG.Items.Inventory>();

            if (inventory != null)
            {
                inventory.SetCapacity(40);
                EditorUtility.SetDirty(inventory);
            }

            // Выброс вещей из сумки. Модель мешка та же, что у добычи с
            // монстров: игрок уже знает, что лежащий мешок можно подобрать.
            var dropper = Ensure<IsoRPG.Items.ItemDropper>(player);

            if (dropper != null)
            {
                dropper.Setup(
                    SandboxSceneBuilder.LoadDungeonModel("box_small"),
                    AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/_Game/Art/Materials/M_Silhouette_Ally.mat"));
                EditorUtility.SetDirty(dropper);
            }
        }

        // ------------------------------------------------------------------
        // Смерть и возвращение
        // ------------------------------------------------------------------

        /// <summary>
        /// Экран смерти с кнопкой «Возродиться» и сам возрождатель.
        ///
        /// Без этой пары смерть героя — тупик: тело лежит, управления нет,
        /// и единственный выход из игры — закрыть окно. Возрождатель тот же,
        /// что у монстров, но по команде: игрок встаёт кнопкой, а не сам
        /// через полминуты.
        /// </summary>
        private static void Death(GameObject player)
        {
            var respawner = Ensure<Respawner>(player);

            if (respawner != null)
            {
                respawner.SetManualOnly(true);
                EditorUtility.SetDirty(respawner);
            }

            // Импорт настраиваем ДО загрузки: текстура, не размеченная как
            // спрайт, не находится по пути — молча, без единой ошибки.
            IconBinder.PrepareSprites("Assets/_Game/Art/UI");

            var screen = Ensure<IsoRPG.UI.DeathScreen>(player);

            if (screen != null)
            {
                var art = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Game/Art/UI/DeathScreen.png");

                if (art == null)
                    Debug.LogWarning("[IsoRPG] Нет картинки экрана смерти — кнопка будет, надгробия нет.");

                screen.SetupArt(art);
                EditorUtility.SetDirty(screen);
            }

            // Полоска над головой у героя: панель вверху отвечает на вопрос
            // «сколько у меня осталось», а полоска над персонажем — на
            // другой: «попали по мне только что или нет».
            Ensure<OverheadHealthBar>(player);

            // Сохранение — последним: в Start оно раздаёт состояние всем
            // прочим компонентам, и они к этому моменту должны быть на месте.
            Ensure<IsoRPG.Save.SaveService>(player);
        }

        // ------------------------------------------------------------------
        // Окна интерфейса
        // ------------------------------------------------------------------

        private static void Windows(GameObject player)
        {
            // Панель цели и боевой журнал. Свой холст строят сами.
            Ensure<CombatHud>(player);
            Ensure<CombatLogHud>(player);

            var preview = Ensure<IsoRPG.Items.CharacterPreview>(player);
            if (preview != null) SandboxSceneBuilder.SetupPreview(preview);

            Ensure<IsoRPG.Items.InventoryHud>(player);

            var characterHud = Ensure<IsoRPG.Items.CharacterHud>(player);
            if (characterHud != null) SandboxSceneBuilder.SetupSlotHints(characterHud);

            Ensure<IsoRPG.Items.LootWindow>(player);
            Ensure<IsoRPG.UI.MerchantWindow>(player);

            // Панель кнопок на арене уже стоит, но без иконок: её вешало
            // задание «hud» голым AddComponent. Иконки раздаём в любом случае —
            // пустой ряд квадратов читается как брак вёрстки.
            var bar = player.GetComponent<IsoRPG.UI.HudBar>();
            if (bar == null) bar = Ensure<IsoRPG.UI.HudBar>(player);
            if (bar != null) SandboxSceneBuilder.SetupHudBar(bar);

            Ensure<IsoRPG.UI.Tooltip>(player);
            Ensure<IsoRPG.UI.SettingsWindow>(player);
        }

        // ------------------------------------------------------------------
        // Квесты и таланты
        // ------------------------------------------------------------------

        private static void Quests(GameObject player)
        {
            Ensure<IsoRPG.Quests.QuestLog>(player);
            Ensure<IsoRPG.Quests.QuestTracker>(player);
            Ensure<IsoRPG.Quests.DialogueWindow>(player);
            Ensure<IsoRPG.UI.QuestJournal>(player);

            var talents = Ensure<IsoRPG.Progression.TalentBook>(player);

            if (talents != null)
            {
                DatabaseBuilder.Build();

                var tree = TalentsBuilder.LoadAll();

                if (tree.Count == 0)
                {
                    TalentsBuilder.Build();
                    tree = TalentsBuilder.LoadAll();
                }

                talents.Setup(tree);
                EditorUtility.SetDirty(talents);
            }

            Ensure<IsoRPG.Progression.TalentStats>(player);
            Ensure<IsoRPG.UI.TalentWindow>(player);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Добавить, если такого ещё нет. Возвращает компонент в любом случае —
        /// настроить его надо и на повторном прогоне, если настройка дешёвая.
        /// </summary>
        private static T Ensure<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;

            var comp = go.AddComponent<T>();

            if (comp == null)
            {
                Debug.LogError("[IsoRPG] Не встал компонент " + typeof(T).Name +
                               " — проверь его требования (RequireComponent).");
                return null;
            }

            added.Add(typeof(T).Name);
            return comp;
        }

        /// <summary>
        /// Щуп: читает результат, а не повторяет намерение.
        ///
        /// Журнал печатает тот же код, который делал работу, и подтверждает
        /// лишь то, что строка выполнилась. Здесь спрашиваем сам объект.
        /// </summary>
        private static void Check(GameObject player)
        {
            var must = new (string name, bool present)[]
            {
                ("клик по миру (PlayerInputRouter)", player.GetComponent<PlayerInputRouter>() != null),
                ("хранилище цели (TargetSelector)",  player.GetComponent<TargetSelector>() != null),
                ("метка цели (Targetable)",          player.GetComponent<Targetable>() != null),
                ("здоровье (Health)",                player.GetComponent<Health>() != null),
                ("бой (MeleeCombatant)",             player.GetComponent<MeleeCombatant>() != null),
                ("панель цели (CombatHud)",          player.GetComponent<CombatHud>() != null),
                ("подсказка наведения (HoverInspector)", player.GetComponent<HoverInspector>() != null),
                ("коллайдер тела",                   player.GetComponent<Collider>() != null),
                ("сумка (Inventory)",                player.GetComponent<IsoRPG.Items.Inventory>() != null),
                ("экипировка (Equipment)",           player.GetComponent<IsoRPG.Items.Equipment>() != null),
                ("журнал заданий (QuestLog)",        player.GetComponent<IsoRPG.Quests.QuestLog>() != null),
                ("разговор (DialogueWindow)",        player.GetComponent<IsoRPG.Quests.DialogueWindow>() != null),
                ("экран смерти (DeathScreen)",       player.GetComponent<IsoRPG.UI.DeathScreen>() != null),
                ("возрождение (Respawner)",          player.GetComponent<Respawner>() != null),
                ("сохранение (SaveService)",         player.GetComponent<IsoRPG.Save.SaveService>() != null),
            };

            // Погоня у героя должна быть выключена: с ней персонажа тянет к
            // выбранной цели, и уйти на клавишах нельзя.
            var meleeCheck = player.GetComponent<MeleeCombatant>();

            if (meleeCheck != null)
            {
                bool chase = new SerializedObject(meleeCheck).FindProperty("chaseTarget").boolValue;

                if (chase) Debug.LogError("[IsoRPG]   погоня героя ВКЛЮЧЕНА — будет «магнитить» к цели.");
                else Debug.Log("[IsoRPG]   есть: погоня героя выключена (магнита не будет)");
            }

            int bad = 0;

            foreach (var (name, present) in must)
            {
                if (present) Debug.Log("[IsoRPG]   есть: " + name);
                else { Debug.LogError("[IsoRPG]   НЕТ: " + name); bad++; }
            }

            // Цели на сцене: если их ноль, выделять будет нечего, и это
            // отдельная поломка, которую легко спутать с неработающим кликом.
            int targets = 0;
            int withCollider = 0;

            foreach (var t in Object.FindObjectsByType<Targetable>(FindObjectsInactive.Exclude,
                                                                   FindObjectsSortMode.None))
            {
                if (t.gameObject == player) continue;
                targets++;
                if (t.GetComponentInChildren<Collider>() != null) withCollider++;
            }

            Debug.Log("[IsoRPG] Целей на сцене (кроме героя): " + targets +
                      ", из них с коллайдером: " + withCollider);

            if (targets > 0 && withCollider < targets)
                Debug.LogError("[IsoRPG] У " + (targets - withCollider) +
                               " целей нет коллайдера — по ним нельзя щёлкнуть.");

            Debug.Log(bad == 0
                ? "[IsoRPG] Набор игрока собран полностью."
                : "[IsoRPG] Набор игрока НЕПОЛНЫЙ: не хватает " + bad + ".");
        }
    }

    internal static class GameObjectSearch
    {
        /// <summary>
        /// Первый объект с таким именем. Заведено ради читаемости вызова:
        /// Linq-цепочка с FindObjectsByType в четыре строки заслоняет смысл.
        /// </summary>
        public static GameObject FirstOrDefaultNamed(this GameObject[] list, string name)
        {
            foreach (var go in list) if (go.name == name) return go;
            return null;
        }
    }
}

