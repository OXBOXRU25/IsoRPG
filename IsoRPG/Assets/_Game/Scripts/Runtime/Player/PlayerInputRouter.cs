using UnityEngine;
using UnityEngine.InputSystem;
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
        private ClickToMoveController movement;
        private MeleeCombatant combat;
        private Inventory inventory;
        private Camera cam;

        private void Awake()
        {
            targets = GetComponent<TargetSelector>();
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

            if (!pressed && !held) return;

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
            if (pressed && TryPickTarget(screen)) return;

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
                    LootPopup.Show(loot.transform.position, gold + " золота", GoldColor);
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
                // Труп с добычей — обыскиваем, а не выбираем целью.
                // Проверяем раньше живых: мёртвый в цель всё равно не берётся,
                // а клик по нему должен что-то делать, а не проваливаться.
                var loot = hit.collider.GetComponentInParent<LootSource>();
                if (loot != null && loot.HasLoot)
                {
                    Debug.Log($"[IsoRPG] Клик по добыче: {loot.name}, золота {loot.Gold}");
                    TryLoot(loot);
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
