using UnityEngine;
using UnityEngine.InputSystem;

namespace IsoRPG.Player
{
    /// <summary>
    /// Примерка анимаций прямо в игре, клавишами.
    ///
    /// Заведено 04.09.2026, когда стало ясно, что подбор пластики по одной
    /// сборке на вариант — это круг за кругом: на зуме мы так потратили пять
    /// заходов. У набора три ветки бега, двадцать шесть стоек покоя и четыре
    /// приземления; выбрать их может только Павлон и только глазами, а моя
    /// работа — дать ему перебрать за минуту, а не за вечер.
    ///
    /// Подмена идёт через <c>AnimatorOverrideController</c>: он заменяет клип
    /// везде, где тот встречается, включая нутро деревьев смешивания. Машину
    /// состояний при этом не трогаем вовсе — значит ломаться нечему, и
    /// выбранный вариант потом просто вписывается в задание сборки.
    ///
    /// Инструмент приёмки, а не игровая механика: выбрали — числа уезжают в
    /// `HeroMoveKit`, а это снимается. Оставлять переключатель в игре нельзя
    /// по той же причине, по которой мы убрали переключатель проекций: пока
    /// варианты живы, каждая правка делается вслепую.
    /// </summary>
    public sealed class AnimTryout : MonoBehaviour
    {
        [Tooltip("Варианты бега. Первый — тот, что стоит сейчас.")]
        [SerializeField] private AnimationClip[] runs;

        [Tooltip("Варианты стойки покоя вне боя.")]
        [SerializeField] private AnimationClip[] idles;

        [Tooltip("Варианты стойки покоя в бою.")]
        [SerializeField] private AnimationClip[] combatIdles;

        [Tooltip("Варианты приземления после прыжка.")]
        [SerializeField] private AnimationClip[] landings;

        /// <summary>Клипы, которые стоят в контроллере сейчас, — их и подменяем.</summary>
        [SerializeField] private AnimationClip baseRun;
        [SerializeField] private AnimationClip baseIdle;
        [SerializeField] private AnimationClip baseCombatIdle;
        [SerializeField] private AnimationClip baseLanding;

        private Animator animator;
        private AnimatorOverrideController over;

        private int runAt, idleAt, combatAt, landAt;

        public void Setup(AnimationClip[] runVariants, AnimationClip[] idleVariants,
                          AnimationClip[] combatVariants, AnimationClip[] landVariants,
                          AnimationClip currentRun, AnimationClip currentIdle,
                          AnimationClip currentCombatIdle, AnimationClip currentLanding)
        {
            runs = runVariants;
            idles = idleVariants;
            combatIdles = combatVariants;
            landings = landVariants;

            baseRun = currentRun;
            baseIdle = currentIdle;
            baseCombatIdle = currentCombatIdle;
            baseLanding = currentLanding;
        }

        private void Start()
        {
            animator = GetComponentInChildren<Animator>(true);

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                enabled = false;
                return;
            }

            over = new AnimatorOverrideController(animator.runtimeAnimatorController);
            animator.runtimeAnimatorController = over;

            IsoRPG.Combat.CombatLog.Add(
                "Примерка анимаций: F1 бег, F2 стойка, F3 боевая стойка, F4 приземление.");
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null) return;

            if (keys.f1Key.wasPressedThisFrame) Next(runs, baseRun, ref runAt, "Бег");
            if (keys.f2Key.wasPressedThisFrame) Next(idles, baseIdle, ref idleAt, "Стойка");
            if (keys.f3Key.wasPressedThisFrame) Next(combatIdles, baseCombatIdle, ref combatAt, "Боевая стойка");
            if (keys.f4Key.wasPressedThisFrame) Next(landings, baseLanding, ref landAt, "Прыжок");
        }

        /// <summary>
        /// Поставить следующий вариант и сказать вслух, какой именно.
        ///
        /// Имя клипа в журнале обязательно: без него выбор нельзя перенести в
        /// сборку — Павлон скажет «вот этот», а какой это файл, не будет знать
        /// никто.
        /// </summary>
        private void Next(AnimationClip[] list, AnimationClip original, ref int at, string what)
        {
            if (list == null || list.Length == 0 || original == null)
            {
                IsoRPG.Combat.CombatLog.Add($"{what}: вариантов нет.");
                return;
            }

            at = (at + 1) % list.Length;

            var clip = list[at];
            if (clip == null) return;

            over[original] = clip;

            IsoRPG.Combat.CombatLog.Add($"{what} {at + 1} из {list.Length}: {clip.name}");
        }
    }
}
