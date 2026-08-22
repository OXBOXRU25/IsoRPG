using UnityEngine;
using UnityEngine.InputSystem;
using IsoRPG.Combat;

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

        private TargetSelector targets;
        private ClickToMoveController movement;
        private MeleeCombatant combat;
        private Camera cam;

        private void Awake()
        {
            targets = GetComponent<TargetSelector>();
            movement = GetComponent<ClickToMoveController>();
            combat = GetComponent<MeleeCombatant>();
            cam = Camera.main;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool pressed = mouse.leftButton.wasPressedThisFrame;
            bool held = mouse.leftButton.isPressed;

            if (!pressed && !held) return;
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
