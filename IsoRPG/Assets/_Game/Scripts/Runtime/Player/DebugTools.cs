using UnityEngine;
using UnityEngine.InputSystem;
using IsoRPG.Combat;
using IsoRPG.Items;

namespace IsoRPG.Player
{
    /// <summary>
    /// Отладочные клавиши: выдать уровень, убить цель, вылечиться, дать денег.
    ///
    /// Нужны потому, что система рассчитана на шестьдесят уровней, а контента
    /// пока на пять. Без них баланс сорокового уровня останется непроверенным
    /// до того момента, когда переделывать его будет уже дорого.
    ///
    /// Компонент легко выключить одной галочкой — в готовой сборке его быть
    /// не должно.
    /// </summary>
    public sealed class DebugTools : MonoBehaviour
    {
        [Tooltip("Включены ли отладочные клавиши.")]
        [SerializeField] private bool enableCheats = true;

        private Experience experience;
        private Health health;
        private ResourcePool energy;
        private TargetSelector targets;
        private Inventory inventory;

        private void Awake()
        {
            experience = GetComponent<Experience>();
            health = GetComponent<Health>();
            energy = GetComponent<ResourcePool>();
            targets = GetComponent<TargetSelector>();
            inventory = GetComponent<Inventory>();
        }

        private void Update()
        {
            if (!enableCheats) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // L — уровень
            if (keyboard.lKey.wasPressedThisFrame && experience != null)
            {
                experience.GrantLevels(1);
                Debug.Log("[Отладка] Уровень " + experience.Level);
            }

            // K — убить выбранную цель, чтобы не ждать боя
            if (keyboard.kKey.wasPressedThisFrame && targets != null)
            {
                var victim = targets.Current;
                if (victim != null && victim.Health != null)
                {
                    victim.Health.TakeDamage(victim.Health.Current + 9999, gameObject);
                    Debug.Log("[Отладка] Цель убита");
                }
            }

            // H — полное здоровье и энергия
            if (keyboard.hKey.wasPressedThisFrame)
            {
                if (health != null) health.Heal(health.Max);
                if (energy != null) energy.Refill();
                Debug.Log("[Отладка] Здоровье и энергия восстановлены");
            }

            // G — сто золота
            if (keyboard.gKey.wasPressedThisFrame && inventory != null)
            {
                inventory.AddGold(100);
                Debug.Log("[Отладка] Золота: " + inventory.Gold);
            }

            // R — поднять всех монстров, не дожидаясь таймеров
            if (keyboard.rKey.wasPressedThisFrame)
            {
                int revived = Respawner.ReviveAll();
                Debug.Log("[Отладка] Возрождено монстров: " + revived);
                CombatLog.Add("Возрождено монстров: " + revived, LogKind.System);
            }

            // J — показать содержимое сумки в консоль, пока нет окна инвентаря
            if (keyboard.jKey.wasPressedThisFrame && inventory != null)
            {
                var text = new System.Text.StringBuilder();
                text.AppendLine("[Отладка] Сумка (" + inventory.UsedSlots + " из " +
                                inventory.Capacity + "), золота " + inventory.Gold + ":");

                foreach (var stack in inventory.All())
                {
                    if (stack.IsEmpty) continue;
                    text.AppendLine("  " + stack + "  (" + stack.Item.ShortStats() + ")");
                }

                Debug.Log(text.ToString());
            }
        }
    }
}
