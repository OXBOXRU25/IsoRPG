using System;
using UnityEngine;
using IsoRPG.Localization;

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
                HitResult.Crit => " " + Loc.T("(крит)"),
                HitResult.Miss => " " + Loc.T("(вскользь)"),
                _ => ""
            };

            // Имя цели переводим отдельно: оно приходит из данных монстра, а
            // не из этой строки.
            Add(Loc.F("Вы наносите {0}: {1}{2}", Loc.T(target), amount, suffix),
                result == HitResult.Crit ? LogKind.Crit
              : result == HitResult.Miss ? LogKind.Miss
              : LogKind.DamageDealt);
        }

        /// <summary>
        /// Урон по игроку — с той же пометкой крита, что и свой.
        ///
        /// Монстры критуют ровно так же, как герой, и всегда критовали. Но в
        /// журнале их удары шли без пометки, и выглядело это как «мне крита
        /// не бывает». Когда из полосы здоровья вылетает четверть, игрок
        /// имеет право знать, что это был крит, а не «что-то пошло не так».
        /// </summary>
        public static void DamageTaken(string source, int amount,
                                       HitResult result = HitResult.Normal)
        {
            string suffix = result switch
            {
                HitResult.Crit => " " + Loc.T("(крит)"),
                HitResult.Miss => " " + Loc.T("(вскользь)"),
                _ => ""
            };

            Add(Loc.F("{0} наносит вам: {1}{2}", Loc.T(source), amount, suffix),
                result == HitResult.Crit ? LogKind.Crit : LogKind.DamageTaken);
        }

        public static void Killed(string target)
        {
            Add(Loc.F("{0} повержен", Loc.T(target)), LogKind.System);
        }

        public static void GainedExperience(int amount)
        {
            Add(Loc.F("Получено опыта: {0}", amount), LogKind.Experience);
        }

        public static void GainedGold(int amount)
        {
            Add(Loc.F("Получено золота: {0}", amount), LogKind.Gold);
        }

        public static void Looted(string item, Color color)
        {
            Add(Loc.F("Получено: {0}", Loc.T(item)), LogKind.Loot);
        }

        public static void Stunned(string target, float seconds)
        {
            Add(Loc.F("{0} оглушён на {1} с", Loc.T(target), seconds.ToString("0.#")), LogKind.System);
        }

        public static void LevelUp(int level)
        {
            Add(Loc.F("Новый уровень: {0}", level), LogKind.Experience);
        }
    }
}
