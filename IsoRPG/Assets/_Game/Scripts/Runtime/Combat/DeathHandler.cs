using UnityEngine;
using UnityEngine.AI;
using IsoRPG.Player;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Что происходит при смерти: анимация, отключение всего живого,
    /// исчезновение тела.
    ///
    /// Отдельным компонентом, потому что смерть касается многих систем —
    /// навигации, боя, ИИ, интерфейса. Пусть каждая узнаёт о ней из одного
    /// места, а не проверяет здоровье сама у себя в Update.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class DeathHandler : MonoBehaviour
    {
        [Tooltip("Через сколько секунд убрать тело. Ноль — оставить лежать навсегда.")]
        [SerializeField] private float removeAfter = 6f;

        [Tooltip("Плавно опустить тело под землю перед исчезновением — вместо того чтобы оно моргнуло и пропало.")]
        [SerializeField] private bool sinkBeforeRemoval = true;

        [SerializeField] private float sinkSpeed = 0.35f;
        [SerializeField] private float sinkDelay = 3f;

        private Health health;
        private bool dead;
        private float deathTime;

        private void Awake() => health = GetComponent<Health>();

        /// <summary>
        /// Не убирать тело. Зовётся возрождением: оно поднимет этот же объект,
        /// и удалять его нельзя.
        /// </summary>
        public void KeepBody() => removeAfter = 0f;

        /// <summary>Сбросить состояние смерти — объект снова живой.</summary>
        public void ResetDeath()
        {
            dead = false;
            deathTime = 0f;

            var driver = GetComponent<CharacterAnimatorDriver>();
            if (driver != null) driver.SetDead(false);
        }

        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        private void OnDied(GameObject killer)
        {
            if (dead) return;
            dead = true;
            deathTime = Time.time;

            GrantExperience(killer);

            // Анимация смерти, если персонаж вообще умеет анимироваться.
            var driver = GetComponent<CharacterAnimatorDriver>();
            if (driver != null) driver.SetDead(true);

            // Останавливаем и отключаем всё, что заставляет тело действовать.
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
                agent.enabled = false;
            }

            var brain = GetComponent<MonsterBrain>();
            if (brain != null) brain.enabled = false;

            var combat = GetComponent<MeleeCombatant>();
            if (combat != null) combat.enabled = false;

            var targets = GetComponent<TargetSelector>();
            if (targets != null)
            {
                targets.Clear();
                targets.enabled = false;
            }

            // Коллайдеры выключаем, чтобы по трупу нельзя было кликнуть и
            // взять его в цель.
            //
            // Но если на трупе есть добыча — оставляем: теперь коллайдер
            // работает в другой роли, как «нажми, чтобы обыскать». Выбор цели
            // при этом не мешает, потому что мёртвых в цель не берут.
            var loot = GetComponent<IsoRPG.Items.LootSource>();
            bool keepClickable = loot != null && loot.HasLoot;

            if (!keepClickable)
            {
                foreach (var collider in GetComponentsInChildren<Collider>())
                    collider.enabled = false;
            }
            else
            {
                // Тело с добычей не убираем, пока не обыщут.
                loot.LootTaken += OnLootTaken;
                removeAfter = 0f;
            }
        }

        /// <summary>
        /// Наградить убийцу опытом.
        ///
        /// Считает погибший, а не убийца: только он знает свой уровень.
        /// Убийца лишь получает готовое число — так же, как с уроном, где
        /// защиту применяет получатель.
        /// </summary>
        private void GrantExperience(GameObject killer)
        {
            if (killer == null) return;

            var killerExp = killer.GetComponent<Experience>();
            if (killerExp == null) return;

            var ownDefense = GetComponent<DefenseStats>();
            int ownLevel = ownDefense != null ? ownDefense.Level : 1;

            int reward = Experience.RewardFor(ownLevel, killerExp.Level);
            if (reward <= 0) return;   // серая цель опыта не даёт

            killerExp.AddExperience(reward);

            var self = GetComponent<Targetable>();

            CombatLog.Killed(self != null ? self.DisplayName : "Противник");
            CombatLog.GainedExperience(reward);

            Vector3 point = self != null ? self.OverheadPoint : transform.position + Vector3.up * 2f;
            ExperiencePopup.Show(point, reward);
        }

        /// <summary>Добычу забрали — теперь тело можно убирать как обычно.</summary>
        private void OnLootTaken()
        {
            foreach (var collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;

            // Отсчёт начинаем заново, от момента обыска: иначе тело исчезло
            // бы мгновенно, потому что смерть была давно.
            deathTime = Time.time;
            removeAfter = 4f;
            sinkDelay = 1.5f;
        }

        private void Update()
        {
            if (!dead || removeAfter <= 0f) return;

            float elapsed = Time.time - deathTime;

            if (sinkBeforeRemoval && elapsed > sinkDelay)
                transform.position += Vector3.down * (sinkSpeed * Time.deltaTime);

            if (elapsed >= removeAfter) Destroy(gameObject);
        }
    }
}
