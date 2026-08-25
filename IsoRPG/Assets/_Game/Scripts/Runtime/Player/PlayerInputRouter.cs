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

            if (movement == null) return;
            if (!pressed && !movement.FollowWhileHeld) return;

            if (movement.TryClickToMove(screen, pressed) && pressed)
            {
                // Клик по земле — прямой приказ игрока. Он отменяет
                // преследование, иначе бой тут же уводит персонажа назад.
                if (combat != null) combat.CancelChase();
            }
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

                var shop = hit.collider.GetComponentInParent<IsoRPG.Items.Merchant>();
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

                var giver = hit.collider.GetComponentInParent<IsoRPG.Quests.QuestGiver>();
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
                // сундук — но только после того, как сундук открыт.
                var chest = hit.collider.GetComponentInParent<IsoRPG.Items.TreasureChest>();
                if (chest != null && !chest.IsOpen)
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

                var bag = hit.collider.GetComponentInParent<LootDrop>();
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

                var targetable = hit.collider.GetComponentInParent<Targetable>();
                if (targetable == null) continue;
                if (targetable.gameObject == gameObject) continue;   // сам себя не выбираем
                if (!targetable.IsAlive) continue;

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
