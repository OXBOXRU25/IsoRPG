using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Новый уровень восстанавливает здоровье и энергию полностью.
    ///
    /// Это не подарок, а темп игры. Уровень приходит посреди боя, чаще всего
    /// на последнем убийстве, — и без восстановления награда за него сводится
    /// к тому, что игрок идёт сидеть в углу. Полное здоровье превращает
    /// уровень в разрешение продолжать, а не в паузу.
    ///
    /// Отдельным компонентом, а не строкой в интерфейсе: лечение — событие
    /// игрового мира, его видит полоска, слышит звук и пишет лог. Интерфейс
    /// должен показывать последствия, а не создавать их.
    /// </summary>
    [RequireComponent(typeof(Experience))]
    public sealed class LevelUpRestore : MonoBehaviour
    {
        private Experience experience;
        private Health health;
        private ResourcePool energy;

        private void Awake()
        {
            experience = GetComponent<Experience>();
            health = GetComponent<Health>();
            energy = GetComponent<ResourcePool>();
        }

        private void OnEnable()
        {
            if (experience != null) experience.LevelUp += OnLevelUp;
        }

        private void OnDisable()
        {
            if (experience != null) experience.LevelUp -= OnLevelUp;
        }

        private void OnLevelUp(int level)
        {
            if (health != null && health.IsAlive) health.Heal(health.Max);
            if (energy != null) energy.Refill();
        }
    }
}
