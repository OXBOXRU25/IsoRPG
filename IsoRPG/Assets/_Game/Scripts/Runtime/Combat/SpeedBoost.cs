using UnityEngine;
using UnityEngine.AI;
using IsoRPG.Localization;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Временная прибавка к скорости бега. Спринт разбойника и всё, что
    /// будет ускорять персонажа впредь.
    ///
    /// Отдельным компонентом, а не полем в способности: ускорять может не
    /// только она — зелье, талант, эффект местности. Пусть у прибавки будет
    /// одно место, которое помнит исходную скорость и умеет её вернуть.
    ///
    /// Базовая скорость запоминается ОДИН раз при старте. Иначе повторное
    /// применение поверх действующего ускорения запомнило бы уже ускоренное
    /// значение — и скорость росла бы с каждым нажатием, а по истечении
    /// срока персонаж навсегда оставался бы быстрее, чем задумано.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SpeedBoost : MonoBehaviour
    {
        private NavMeshAgent agent;
        private float baseSpeed;
        private float endTime;
        private bool active;

        /// <summary>Действует ли ускорение прямо сейчас.</summary>
        public bool IsActive => active;

        /// <summary>Сколько секунд осталось. Ноль — не действует.</summary>
        public float Remaining => active ? Mathf.Max(0f, endTime - Time.time) : 0f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            baseSpeed = agent != null ? agent.speed : 0f;
        }

        /// <summary>
        /// Ускорить на долю от базовой скорости.
        /// </summary>
        /// <param name="bonus">0.7 — плюс 70% к исходной скорости.</param>
        /// <param name="seconds">Сколько держится.</param>
        public void Apply(float bonus, float seconds)
        {
            if (agent == null || bonus <= 0f || seconds <= 0f) return;

            agent.speed = baseSpeed * (1f + bonus);

            // Повторное применение продлевает, а не складывается: два спринта
            // подряд не должны разгонять героя вдвое.
            endTime = Time.time + seconds;
            active = true;

            CombatLog.Add(Loc.F("Ускорение: +{0}% на {1} с",
                                Mathf.RoundToInt(bonus * 100f), Mathf.RoundToInt(seconds)),
                          LogKind.System);
        }

        private void Update()
        {
            if (!active || Time.time < endTime) return;

            active = false;
            if (agent != null) agent.speed = baseSpeed;

            CombatLog.Add(Loc.T("Ускорение закончилось"), LogKind.System);
        }
    }
}
