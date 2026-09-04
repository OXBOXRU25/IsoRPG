using UnityEngine;
using UnityEngine.InputSystem;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Убрать и достать оба кинжала.
    ///
    /// Клипы отобрал Павлон 04.09.2026 через консоль: доставание
    /// `Unarmed-Unsheath-Dual-Hips`, убирание `Armed-Sheath-Dual-ToUnarmed-Hips`.
    /// Вариант «с пояса», а не «со спины»: у разбойника кинжалы на поясе.
    ///
    /// Клавиша <b>Z</b>, как в больших РПГ. Плюс оружие достаётся само при
    /// входе в бой — иначе игрок раз за разом будет получать первый удар с
    /// пустыми руками, а это не решение, а забывчивость.
    ///
    /// Чего здесь пока НЕТ, и это надо знать: убранные кинжалы просто
    /// исчезают, а не переезжают на пояс. Для второго нужны точки крепления на
    /// поясе и модели в них — отдельная работа, и делать её вслепую нельзя:
    /// место на поясе подбирается глазами, как подбирался хват.
    /// </summary>
    public sealed class WeaponSheath : MonoBehaviour
    {
        private static readonly int SheathHash = Animator.StringToHash("Sheath");
        private static readonly int UnsheathHash = Animator.StringToHash("Unsheath");

        [Tooltip("Через сколько секунд после начала клипа оружие появляется или пропадает.")]
        [SerializeField] private float swapAt = 0.45f;

        private Animator animator;
        private IsoRPG.Items.WeaponVisual visual;
        private IsoRPG.Player.CharacterAnimatorDriver driver;

        /// <summary>Убраны ли клинки прямо сейчас.</summary>
        public bool Sheathed { get; private set; }

        private float swapWhen;
        private bool swapTo;

        /// <summary>Был ли герой в бою в прошлом кадре: доставать надо на переходе.</summary>
        private bool wasFighting;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            visual = GetComponent<IsoRPG.Items.WeaponVisual>();
            driver = GetComponent<IsoRPG.Player.CharacterAnimatorDriver>();
        }

        private void Update()
        {
            var keys = Keyboard.current;

            if (keys != null && keys.zKey.wasPressedThisFrame) Toggle();

            // Достаём сами — но только В МОМЕНТ входа в бой.
            //
            // Первая версия проверяла «в бою и убрано» каждый кадр, и оружие
            // возвращалось на следующем же кадре после нажатия: боевое
            // состояние держится ещё несколько секунд после последнего удара.
            // Со стороны это выглядело как «Z не пашет» — Павлон 05.09.2026.
            //
            // Смотреть надо на переход, а не на состояние: вошёл в бой —
            // достал. Стоишь в бою с убранными клинками по своей воле — твоё
            // дело.
            bool fighting = driver != null && driver.InCombat;

            if (fighting && !wasFighting && Sheathed) Toggle();

            wasFighting = fighting;

            if (swapWhen > 0f && Time.time >= swapWhen)
            {
                swapWhen = 0f;

                if (visual != null) visual.SetHidden(swapTo);
            }
        }

        /// <summary>Переключить: убрать или достать.</summary>
        public void Toggle()
        {
            if (animator == null) return;

            Sheathed = !Sheathed;

            animator.SetTrigger(Sheathed ? SheathHash : UnsheathHash);

            // Оружие меняется НЕ сразу, а в середине жеста: рука должна
            // дойти до пояса. Иначе клинки исчезают, когда герой ещё только
            // начал движение, и жест читается как пустой взмах.
            swapWhen = Time.time + swapAt;
            swapTo = Sheathed;

            Debug.Log($"[IsoRPG] Ножны: {(Sheathed ? "убираю" : "достаю")} клинки " +
                      $"(аниматор {(animator != null ? animator.name : "нет")}).");
        }
    }
}
