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

        private void OnEnable() => health.Died += OnDied;
        private void OnDisable() => health.Died -= OnDied;

        private void OnDied(GameObject killer)
        {
            if (dead) return;
            dead = true;
            deathTime = Time.time;

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
            // взять его в цель. Позже сюда же встанет окно лута — тогда
            // коллайдер вернём, но уже для другой роли.
            foreach (var collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;
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
