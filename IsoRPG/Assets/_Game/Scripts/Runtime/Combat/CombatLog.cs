using System;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>О чём сообщение — от этого зависит цвет строки.</summary>
    public enum LogKind
    {
        DamageDealt,     // мы бьём
        DamageTaken,     // бьют нас
        Crit,            // критический удар
        Miss,            // отражённый
        Loot,            // подобрано
        Gold,            // золото
        Experience,      // опыт
        System,          // прочее: оглушение, уровень, скрытность
    }

    /// <summary>
    /// Боевой лог. Точка входа для всех сообщений о бое.
    ///
    /// Статический намеренно: сообщения шлют десятки мест — урон, лут, опыт,
    /// эффекты. Тянуть в каждое ссылку на интерфейс означало бы связать
    /// логику с отображением, а мы договорились держать их порознь ради
    /// будущей сети. Здесь логика просто кричит в пустоту, а слушает её тот,
    /// кто захочет.
    /// </summary>
    public static class CombatLog
    {
        /// <summary>Новая строка: текст и вид. Интерфейс подписывается и рисует.</summary>
        public static event Action<string, LogKind> LineAdded;

        public static void Add(string text, LogKind kind = LogKind.System)
        {
            if (string.IsNullOrEmpty(text)) return;
            LineAdded?.Invoke(text, kind);
        }

        // --- Готовые формулировки, чтобы фразы были одинаковыми везде ---

        public static void DamageDealt(string target, int amount, HitResult result)
        {
            string suffix = result switch
            {
                HitResult.Crit => " (крит)",
                HitResult.Miss => " (вскользь)",
                _ => ""
            };

            Add($"Вы наносите {target}: {amount}{suffix}",
                result == HitResult.Crit ? LogKind.Crit
              : result == HitResult.Miss ? LogKind.Miss
              : LogKind.DamageDealt);
        }

        public static void DamageTaken(string source, int amount)
        {
            Add($"{source} наносит вам: {amount}", LogKind.DamageTaken);
        }

        public static void Killed(string target)
        {
            Add($"{target} повержен", LogKind.System);
        }

        public static void GainedExperience(int amount)
        {
            Add($"Получено опыта: {amount}", LogKind.Experience);
        }

        public static void GainedGold(int amount)
        {
            Add($"Получено золота: {amount}", LogKind.Gold);
        }

        public static void Looted(string item, Color color)
        {
            Add($"Получено: {item}", LogKind.Loot);
        }

        public static void Stunned(string target, float seconds)
        {
            Add($"{target} оглушён на {seconds:0.#} с", LogKind.System);
        }

        public static void LevelUp(int level)
        {
            Add($"Новый уровень: {level}", LogKind.Experience);
        }
    }
}
