using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using IsoRPG.Items;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Возрождение монстра на своём месте через время после смерти.
    ///
    /// Не пересоздаёт объект, а поднимает тот же самый: так сохраняются все
    /// настройки, ссылки на таблицу добычи и место, где он стоял. Пересоздание
    /// потребовало бы префабов на каждый вид монстра, а это отдельная работа
    /// ради того же результата.
    /// </summary>
    public sealed class Respawner : MonoBehaviour
    {
        /// <summary>Все живые возрождатели на сцене — для глобального «поднять всех».</summary>
        private static readonly List<Respawner> all = new List<Respawner>();

        /// <summary>
        /// Полторы минуты вместо прежних двадцати пяти секунд.
        ///
        /// На четверти минуты зачищенная комната восстанавливалась раньше,
        /// чем игрок успевал её обыскать: разворачиваешься от сундука, а
        /// убитые уже стоят на своих местах. Это отменяет саму мысль
        /// «я тут прибрался» — а она и есть награда за бой.
        /// </summary>
        [Tooltip("Через сколько секунд после смерти монстр встаёт снова.")]
        [SerializeField] private float respawnDelay = 90f;

        /// <summary>
        /// Разброс времени, долей от задержки.
        ///
        /// Без него убитая разом группа встаёт разом же — четверо появляются
        /// в одну секунду, и видно, что это не мир живёт, а сработал таймер.
        /// Четверть — достаточно, чтобы возвращались вразнобой.
        /// </summary>
        [Tooltip("Насколько время возрождения гуляет: 0.25 — это ±25%.")]
        [SerializeField] private float respawnJitter = 0.25f;

        [Tooltip("Ждать ли, пока труп обыщут. Иначе добыча пропадёт вместе с телом.")]
        [SerializeField] private bool waitForLooting = true;

        [Tooltip("Только по команде: игрок встаёт по кнопке, а не сам через полминуты.")]
        [SerializeField] private bool manualOnly = false;

        /// <summary>Возрождение по кнопке, а не по таймеру. Для игрока.</summary>
        public void SetManualOnly(bool value) => manualOnly = value;

        /// <summary>
        /// Своё время возрождения. Нужно тем, кого не должно быть на месте
        /// через полторы минуты, — прежде всего боссам: их убивают редко и
        /// готовятся к этому, поэтому мгновенное возвращение обесценивает
        /// саму победу.
        /// </summary>
        public void SetDelay(float seconds) => respawnDelay = seconds;

        public bool IsDead => dead;

        private Health health;
        private DeathHandler death;
        private LootSource loot;
        private NavMeshAgent agent;

        private Vector3 homePosition;
        private Quaternion homeRotation;

        private bool dead;
        private float reviveTime;

        private void Awake()
        {
            health = GetComponent<Health>();
            death = GetComponent<DeathHandler>();
            loot = GetComponent<LootSource>();
            agent = GetComponent<NavMeshAgent>();

            homePosition = transform.position;
            homeRotation = transform.rotation;
        }

        private void OnEnable()
        {
            all.Add(this);
            if (health != null) health.Died += OnDied;
        }

        private void OnDisable()
        {
            all.Remove(this);
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied(GameObject killer)
        {
            dead = true;
            reviveTime = Time.time + respawnDelay *
                         (1f + Random.Range(-respawnJitter, respawnJitter));

            // Тело убирать нельзя: мы его же и поднимем. Отключаем удаление
            // в обработчике смерти, иначе объекта к моменту возрождения
            // просто не будет.
            if (death != null) death.KeepBody();
        }

        private void Update()
        {
            if (manualOnly) return;
            if (!dead || Time.time < reviveTime) return;

            // Пока на трупе висит добыча — не поднимаем: иначе она исчезнет
            // вместе со старым телом, и игрок потеряет свою награду.
            if (waitForLooting && loot != null && loot.HasLoot) return;

            Revive();
        }

        public void Revive()
        {
            dead = false;

            transform.SetPositionAndRotation(homePosition, homeRotation);

            // Возвращаем всё, что смерть отключила.
            foreach (var collider in GetComponentsInChildren<Collider>())
                collider.enabled = true;

            foreach (var renderer in GetComponentsInChildren<Renderer>())
                renderer.enabled = true;

            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh)
                {
                    agent.Warp(homePosition);
                    agent.isStopped = false;
                    agent.ResetPath();
                }
            }

            var brain = GetComponent<MonsterBrain>();
            if (brain != null) brain.enabled = true;

            // Включаем любого бойца, а не только ближнего: у лучника другой
            // компонент, и с конкретным типом он оставался бы выключенным
            // после возрождения — живой монстр, который не дерётся.
            if (GetComponent<ICombatant>() is MonoBehaviour combat) combat.enabled = true;

            var targets = GetComponent<TargetSelector>();
            if (targets != null) targets.enabled = true;

            var stun = GetComponent<StunReceiver>();
            if (stun != null) stun.enabled = true;

            if (loot != null) loot.ResetLoot();
            if (death != null) death.ResetDeath();
            if (health != null) health.Revive();

            var self = GetComponent<Targetable>();
            CombatLog.Add((self != null ? self.DisplayName : "Противник") + " вернулся", LogKind.System);
        }

        /// <summary>Поднять всех мёртвых разом. Отладочная команда.</summary>
        public static int ReviveAll()
        {
            int count = 0;

            // Копия списка: Revive может менять состав, если кто-то выключится.
            var snapshot = new List<Respawner>(all);

            foreach (var respawner in snapshot)
            {
                if (respawner == null || !respawner.dead) continue;
                respawner.Revive();
                count++;
            }

            return count;
        }
    }
}
