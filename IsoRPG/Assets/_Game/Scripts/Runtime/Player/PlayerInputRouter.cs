using UnityEngine;
using UnityEngine.InputSystem;
using IsoRPG.Localization;
using IsoRPG.Combat;
using IsoRPG.Items;

namespace IsoRPG.Player
{
    /// <summary>
    /// Единственное место, которое читает мышь и решает, что означает клик.
    ///
    /// Почему один компонент, а не каждый сам за себя: клик по врагу и клик
    /// по земле — это один и тот же клик. Если движение и выбор цели читают
    /// мышь порознь, они срабатывают оба, и персонаж бежит в точку под
    /// монстром вместо того, чтобы взять его в цель.
    /// </summary>
    [RequireComponent(typeof(TargetSelector))]
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        [Tooltip("Слои, по которым вообще ищем клик. Земля, враги, объекты.")]
        [SerializeField] private LayerMask clickMask = ~0;

        [SerializeField] private float rayDistance = 500f;

        /// <summary>
        /// Те же слои и та же дальность, что у клика, — для наведения.
        ///
        /// Отдаём наружу, а не заводим у щупа наведения свои: два набора
        /// слоёв разъедутся при первой же правке одного из них, и получится
        /// худший вид расхождения — подсказка есть, а клик мимо.
        /// </summary>
        public LayerMask ClickMask => clickMask;

        public float RayDistance => rayDistance;

        [Tooltip("На каком расстоянии можно обыскать труп.")]
        [SerializeField] private float lootRange = 2.5f;

        private static readonly Color GoldColor = new Color32(0xE8, 0xC3, 0x5A, 0xFF);

        private TargetSelector targets;
        private LootWindow lootWindow;
        private IsoRPG.UI.MerchantWindow merchantWindow;
        private IsoRPG.Quests.DialogueWindow dialogue;
        private ClickToMoveController movement;
        private MeleeCombatant combat;
        private Inventory inventory;
        private Camera cam;

        /// <summary>
        /// Нажатие пришлось на объект: мешок, собеседника, врага.
        ///
        /// Без этого зажатая кнопка над мешком читалась как приказ идти
        /// в точку под курсором. Мешок за стеной давал самое обидное:
        /// окно открывалось, а персонаж убегал в обход препятствия —
        /// со стороны выглядело так, будто он убегает ОТ добычи.
        /// </summary>
        private bool pressConsumed;

        private void Awake()
        {
            targets = GetComponent<TargetSelector>();
            lootWindow = GetComponent<LootWindow>();
            merchantWindow = GetComponent<IsoRPG.UI.MerchantWindow>();
            dialogue = GetComponent<IsoRPG.Quests.DialogueWindow>();
            movement = GetComponent<ClickToMoveController>();

            // Ходьба на клавишах подключается сама, если её ещё нет.
            //
            // Компонент можно было бы прописать в сборщике сцены, но тогда
            // ради одной строчки пришлось бы пересобирать песочницу — а
            // вместе с ней слетело бы всё, что мы в сцену поставили руками.
            // Здесь дешевле и безопаснее.
            if (GetComponent<KeyboardMove>() == null) gameObject.AddComponent<KeyboardMove>();

            // Кольцо под ногами выбранной цели — оттуда же и по той же
            // причине: пересобирать сцену ради одного компонента дороже,
            // чем добавить его на старте.
            if (GetComponent<IsoRPG.Combat.TargetRing>() == null)
                gameObject.AddComponent<IsoRPG.Combat.TargetRing>();

            // Телесное разведение: чтобы монстр не влезал в героя.
            if (GetComponent<IsoRPG.Combat.BodySpace>() == null)
                gameObject.AddComponent<IsoRPG.Combat.BodySpace>();
            combat = GetComponent<MeleeCombatant>();
            inventory = GetComponent<Inventory>();
            cam = Camera.main;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool pressed = mouse.leftButton.wasPressedThisFrame;
            bool held = mouse.leftButton.isPressed;

            if (!held) pressConsumed = false;
            if (!pressed && !held) return;

            // Тащить персонажа может только нажатие, начавшееся на земле.
            if (!pressed && pressConsumed) return;

            // Клик по окну интерфейса не должен уходить в игру: иначе
            // нажатие на ячейку сумки заодно отправляет персонажа бежать
            // куда-то за окно.
            if (UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector2 screen = mouse.position.ReadValue();

            // Выбор цели — только по свежему нажатию. Иначе, ведя зажатой
            // кнопкой мимо монстра, игрок случайно перебирает цели.
            if (pressed && TryPickTarget(screen))
            {
                pressConsumed = true;
                return;
            }

            // Ходьбы по клику больше нет — как в WoW.
            //
            // Решение Павла от 27.08.2026, и оно не про удобство, а про то,
            // чтобы левая кнопка значила ОДНО. Пока клик по земле уводил
            // персонажа, каждый промах мимо монстра превращался в пробежку не
            // туда — а промахиваться в бою приходится постоянно, потому что
            // цель движется. Теперь левая кнопка только выбирает цель, ходьба
            // живёт на WASD, и перепутать их нельзя.
            //
            // Подход к предметам и собеседникам остался: там персонажа ведёт
            // не клик по земле, а сам предмет — «дойти и подобрать».
        }

        /// <summary>
        /// Обыскать труп. Работает только вблизи — иначе игрок собирал бы
        /// добычу с другого конца карты, и вся ценность подхода пропадает.
        /// </summary>
        /// <summary>
        /// Показать панель мирного, по которому щёлкнули.
        ///
        /// Игрок нажал на существо и вправе увидеть, кто это, — даже если
        /// бить его нельзя.
        /// </summary>
        private void ShowNeutralTarget(Collider hit)
        {
            var hud = GetComponentInChildren<IsoRPG.Combat.CombatHud>();
            if (hud == null) return;

            var shop = hit.GetComponentInParent<IsoRPG.Items.Merchant>();

            if (shop != null)
            {
                hud.ShowNeutral(shop.DisplayName,
                                IsoRPG.Combat.Portraits.For(shop.DisplayName));
                return;
            }

            var giver = hit.GetComponentInParent<IsoRPG.Quests.QuestGiver>();

            if (giver != null)
            {
                hud.ShowNeutral(giver.DisplayName, IsoRPG.Combat.Portraits.QuestGiver());
            }
        }

        private void TryLoot(LootSource loot)
        {
            float distance = Vector3.Distance(transform.position, loot.transform.position);

            if (distance > lootRange)
            {
                // Не дотянулись — подходим. Добычу возьмём следующим кликом:
                // автоматический подбор по прибытии сюрпризом не нужен.
                if (movement != null) movement.MoveTo(loot.transform.position);
                CombatLog.Add("Слишком далеко, подхожу...", LogKind.System);
                return;
            }

            if (inventory == null)
            {
                Debug.LogWarning("[IsoRPG] У игрока нет сумки — подбирать некуда.");
                return;
            }

            if (loot.TakeAll(inventory, out int gold, out var items))
            {
                if (gold > 0)
                {
                    LootPopup.Show(loot.transform.position, Loc.F("{0} золота", gold), GoldColor);
                    CombatLog.GainedGold(gold);
                }

                foreach (var stack in items)
                {
                    LootPopup.Show(loot.transform.position, stack.ToString(), stack.Item.RarityColor);
                    CombatLog.Looted(stack.ToString(), stack.Item.RarityColor);
                }
            }
        }

        private bool TryPickTarget(Vector2 screenPosition)
        {
            Ray ray = cam.ScreenPointToRay(screenPosition);

            // Собираем все попадания, а не первое: у монстра коллайдер может
            // перекрываться травой или собственной полоской здоровья, и первое
            // попадание окажется не тем, во что игрок целился.
            var hits = Physics.RaycastAll(ray, rayDistance, clickMask, QueryTriggerInteraction.Collide);
            if (hits.Length == 0) return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // Мешок с добычей — открываем окно, а не выбираем целью.
                // Проверяем раньше живых: мешок лежит на земле, и клик по
                // нему должен открывать его, а не проваливаться сквозь.
                // NPC с квестом — разговариваем, а не бьём. Раньше живых:
                // NPC мирный, целью он не берётся, и клик по нему должен
                // открывать разговор, а не проваливаться.
                // Торговец раньше собеседника: у лавочника может быть и
                // квест, но пришли к нему в первую очередь торговать.
                // Панель цели для мирного: игрок нажал на существо и вправе
                // увидеть, кто это, — даже если бить его нельзя.
                ShowNeutralTarget(hit.collider);

                // Опознание вынесено в WorldPick и общее с наведением: под
                // курсором и под кликом обязано быть одно и то же существо,
                // иначе подсказка начнёт врать. Действие остаётся здесь —
                // «далеко, подойди» относится к поступку, а не к опознанию.
                var pick = WorldPick.From(hit.collider, gameObject);
                if (!pick.Found) continue;

                if (pick.Kind == PickKind.Self) continue;

                var shop = pick.Thing as IsoRPG.Items.Merchant;
                if (shop != null)
                {
                    float toShop = Vector3.Distance(transform.position, shop.transform.position);

                    if (toShop > shop.TalkRange)
                    {
                        if (movement != null) movement.MoveTo(shop.transform.position);
                        return true;
                    }

                    if (merchantWindow != null) merchantWindow.Open(shop);
                    return true;
                }

                var giver = pick.Thing as IsoRPG.Quests.QuestGiver;
                if (giver != null)
                {
                    float distance = Vector3.Distance(transform.position, giver.transform.position);

                    if (distance <= giver.TalkRange)
                    {
                        // Разворачиваем к себе до открытия окна: собеседник
                        // должен смотреть на того, с кем говорит.
                        giver.FaceTo(transform.position);
                        if (dialogue != null) dialogue.Open(giver);
                    }
                    else
                    {
                        // Далеко — подходим. Требовать от игрока самому
                        // догадаться подойти ближе, когда клик просто ничего
                        // не делает, — худший вид немого отказа.
                        if (movement != null) movement.MoveTo(giver.transform.position);
                    }

                    return true;
                }

                // Сундук раньше мешка: мешок с его добычей ляжет рядом, и
                // повторный клик должен попадать в мешок, а не в открытый
                // сундук — но только после того, как сундук открыт. Порядок
                // держит WorldPick, здесь он лишь разбирается по ролям.
                var chest = pick.Thing as IsoRPG.Items.TreasureChest;
                if (chest != null)
                {
                    float toChest = Vector3.Distance(transform.position, chest.transform.position);

                    if (toChest > chest.Reach)
                    {
                        if (movement != null) movement.MoveTo(chest.transform.position);
                        return true;
                    }

                    chest.TryOpen(gameObject);
                    return true;
                }

                var bag = pick.Thing as LootDrop;
                if (bag != null)
                {
                    float toBag = Vector3.Distance(transform.position, bag.transform.position);

                    // Далеко — подходим, а окно оставляем закрытым: до
                    // мешка за стеной ещё идти, и показывать его содержимое
                    // раньше времени — обещание, которого мы не держим.
                    if (toBag > lootRange)
                    {
                        if (movement != null) movement.MoveTo(bag.transform.position);
                        return true;
                    }

                    if (lootWindow != null) lootWindow.Open(bag);
                    return true;
                }

                // Живое существо. Мёртвых и самого себя WorldPick сюда не
                // пропускает: первые уже стали мешком или доигрывают падение,
                // второй целью не берётся.
                var targetable = pick.Target;
                if (targetable == null) continue;

                targets.Select(targetable);

                // Клик по врагу — приказ «займись им», значит погоня снова
                // разрешена, даже если только что уводили персонажа вручную.
                if (combat != null) combat.EngageTarget();

                return true;
            }

            // Клик мимо всех целей снимает выделение — как в WoW по земле.
            targets.Clear();
            return false;
        }
    }
}
