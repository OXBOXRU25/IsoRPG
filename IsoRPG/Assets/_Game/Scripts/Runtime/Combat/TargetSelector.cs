using System;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Хранит текущую выбранную цель. Один на игрока.
    ///
    /// Отдельно от боя и от ввода намеренно: целью интересуются многие —
    /// интерфейс рисует рамку и полоску, бой решает, кого бить, способности
    /// проверяют дистанцию. Пусть у всех будет один источник правды.
    /// </summary>
    public sealed class TargetSelector : MonoBehaviour
    {
        [SerializeField] private Faction ownFaction = Faction.Player;

        private Targetable current;

        public Targetable Current => current;
        public Faction OwnFaction => ownFaction;

        /// <summary>Цель сменилась. null означает «сняли выделение».</summary>
        public event Action<Targetable> TargetChanged;

        public bool HasHostileTarget =>
            current != null && current.IsAlive && current.IsHostileTo(ownFaction);

        public void Select(Targetable target)
        {
            if (current == target) return;

            Unsubscribe();
            current = target;
            Subscribe();

            TargetChanged?.Invoke(current);
        }

        public void Clear() => Select(null);

        private void Update()
        {
            // Мёртвую цель снимаем сами: иначе игрок продолжает «бить труп»,
            // а интерфейс показывает пустую полоску.
            if (current != null && !current.IsAlive) Clear();
        }

        private void Subscribe()
        {
            if (current != null && current.Health != null)
                current.Health.Died += OnTargetDied;
        }

        private void Unsubscribe()
        {
            if (current != null && current.Health != null)
                current.Health.Died -= OnTargetDied;
        }

        private void OnTargetDied(GameObject killer) => Clear();

        private void OnDestroy() => Unsubscribe();
    }
}
