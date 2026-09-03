using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Затягивает раны вне боя.
    ///
    /// Пять единиц в секунду, как у Blizzard, и только когда бой кончился.
    /// Правило Павла 03.09.2026. Смысл не в лечении, а в темпе игры: без него
    /// игрок после каждой драки идёт к костру или к зельям, и прогулка по
    /// миру превращается в череду простоев. С ним отдых занимает столько,
    /// сколько заняла драка, и никого никуда не гонит.
    ///
    /// Признак боя не заводим свой, а спрашиваем у того, кто его уже ведёт:
    /// пластика героя переключается по нему же. Два таймера «в бою» разошлись
    /// бы в первый же вечер — стойка боевая, а раны затягиваются.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class HealthRegen : MonoBehaviour
    {
        [Tooltip("Сколько здоровья возвращается за секунду вне боя.")]
        [SerializeField] private float perSecond = 5f;

        private Health health;
        private IsoRPG.Player.CharacterAnimatorDriver driver;

        /// <summary>
        /// Недобранная доля единицы.
        ///
        /// Здоровье целое, а пять в секунду при шестидесяти кадрах — это
        /// восемь сотых за кадр. Без накопления округление съедало бы их все,
        /// и лечение стояло бы на месте.
        /// </summary>
        private float carry;

        private void Awake()
        {
            health = GetComponent<Health>();
            driver = GetComponentInChildren<IsoRPG.Player.CharacterAnimatorDriver>(true);
        }

        private void Update()
        {
            if (health == null || !health.IsAlive) return;

            if (health.Current >= health.Max)
            {
                carry = 0f;
                return;
            }

            if (driver != null && driver.InCombat)
            {
                carry = 0f;
                return;
            }

            carry += perSecond * Time.deltaTime;

            int whole = Mathf.FloorToInt(carry);
            if (whole <= 0) return;

            carry -= whole;
            health.Heal(whole);
        }
    }
}
