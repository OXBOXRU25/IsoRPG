using UnityEngine;
using UnityEngine.InputSystem;

namespace IsoRPG.Player
{
    /// <summary>
    /// Прыжок по пробелу.
    ///
    /// Ничего не даёт и не должен: в игре с кликом по земле прыгать некуда,
    /// препятствия обходит навигация. Но прыжок — первое, что человек жмёт,
    /// когда берёт мышь и клавиатуру, и его отсутствие читается как
    /// незаконченная игра. Это жест, а не механика.
    ///
    /// Персонажа поднимает код, а не анимация: движением заведует агент
    /// навигации, он держит героя на поверхности и любое смещение по высоте
    /// из анимации съедает. Поэтому вверх едет модель внутри персонажа —
    /// агент при этом спокойно продолжает вести его по земле, и прыгать
    /// можно на бегу.
    /// </summary>
    public sealed class JumpGesture : MonoBehaviour
    {
        [Tooltip("Насколько высоко подпрыгивает, в метрах.")]
        [SerializeField] private float height = 0.55f;

        [Tooltip("Сколько длится прыжок. Подогнано под клип Jump_Full_Short.")]
        [SerializeField] private float duration = 0.75f;

        private CharacterAnimatorDriver animation;
        private IsoRPG.Combat.Health health;
        private IsoRPG.Items.FoodConsumer food;
        private Transform model;

        private float startTime = -99f;
        private float baseHeight;

        public bool IsJumping => Time.time < startTime + duration;

        private void Awake()
        {
            animation = GetComponent<CharacterAnimatorDriver>();
            health = GetComponent<IsoRPG.Combat.Health>();
            food = GetComponent<IsoRPG.Items.FoodConsumer>();

            // Поднимаем ту же ветку, в которой живёт аниматор: это и есть
            // видимая модель, всё остальное — логика и коллайдер.
            var animator = GetComponentInChildren<Animator>();
            if (animator != null) model = animator.transform;

            if (model != null) baseHeight = model.localPosition.y;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame) TryJump();

            Lift();
        }

        private void TryJump()
        {
            if (IsJumping) return;
            if (health != null && !health.IsAlive) return;

            // Прыжок со стула — это вставание. Еда прерывается своим же
            // правилом «встал с места», просто скажем это вслух.
            if (food != null && food.IsEating) food.Interrupt("подпрыгнул");

            startTime = Time.time;
            if (animation != null) animation.PlayJump();
        }

        /// <summary>
        /// Парабола от нуля до нуля. Считается каждый кадр, а не хранится:
        /// так прыжок сам заканчивается ровно на земле, чем бы его ни
        /// прервали.
        /// </summary>
        private void Lift()
        {
            if (model == null) return;

            float lift = 0f;

            if (IsJumping)
            {
                float t = (Time.time - startTime) / duration;
                lift = 4f * height * t * (1f - t);
            }

            var local = model.localPosition;

            // Сравнение с запасом: писать в transform каждый кадр, когда
            // персонаж просто стоит, незачем.
            if (Mathf.Abs(local.y - (baseHeight + lift)) < 0.0005f) return;

            local.y = baseHeight + lift;
            model.localPosition = local;
        }
    }
}
