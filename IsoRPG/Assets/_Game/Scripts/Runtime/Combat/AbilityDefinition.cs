using System;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>Как способность обращается с комбо-очками.</summary>
    public enum ComboRole
    {
        None,        // не трогает очки
        Generator,   // добавляет очко
        Finisher,    // тратит все накопленные
    }

    /// <summary>Разброс урона финишера при определённом числе комбо-очков.</summary>
    [Serializable]
    public struct DamageRange
    {
        public int min;
        public int max;

        public int Roll() => UnityEngine.Random.Range(min, max + 1);
    }

    /// <summary>
    /// Описание способности. Данные, а не код.
    ///
    /// Боевая система умеет исполнять любую такую запись и ничего не знает
    /// про конкретное «Потрошение». Добавить способность — значит создать
    /// ещё один ассет, а не написать ещё один класс.
    /// </summary>
    [CreateAssetMenu(fileName = "Ability", menuName = "IsoRPG/Способность")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        [Header("Описание")]
        public string displayName = "Способность";

        [TextArea(2, 4)]
        public string description = "";

        [Tooltip("Клавиша на панели способностей.")]
        public string hotkeyLabel = "1";

        [Tooltip("Цвет иконки, пока нет настоящих картинок.")]
        public Color iconColor = new Color32(0x8A, 0x6A, 0x3A, 0xFF);

        [Header("Стоимость и откат")]
        public int energyCost = 45;

        [Tooltip("Секунд до повторного использования. Ноль — только глобальный откат.")]
        public float cooldown = 0f;

        [Header("Комбо-очки")]
        public ComboRole comboRole = ComboRole.Generator;

        [Tooltip("Сколько очков добавляет генератор.")]
        public int comboGain = 1;

        [Header("Урон")]
        [Tooltip("Наносит ли способность урон вообще. Оглушение, например, не наносит.")]
        public bool dealsDamage = true;

        [Tooltip("Множитель урона оружия. 1 — обычный удар, 2.5 — двести пятьдесят процентов.")]
        public float weaponMultiplier = 1f;

        [Tooltip("Прибавка к урону оружия. Итог = урон оружия * множитель + это число. Работает у обычных способностей, не у финишеров.")]
        public int bonusDamage = 5;

        [Tooltip("Урон финишера по числу потраченных очков. Первый элемент — за одно очко, второй за два и так далее. Значение выбирается случайно из диапазона.")]
        public DamageRange[] finisherDamage = new DamageRange[0];

        [Header("Оглушение")]
        [Tooltip("Базовая длительность в секундах.")]
        public float stunBase = 0f;

        [Tooltip("Прибавка за каждое потраченное комбо-очко.")]
        public float stunPerCombo = 0f;

        [Header("Исход удара")]
        [Range(0f, 1f)] public float critChance = CombatMath.DefaultCritChance;
        public float critMultiplier = CombatMath.DefaultCritMultiplier;

        [Tooltip("Шанс, что удар будет частично отражён и нанесёт вдвое меньше.")]
        [Range(0f, 1f)] public float missChance = CombatMath.DefaultMissChance;
        public float missMultiplier = CombatMath.DefaultMissMultiplier;

        [Header("Условия применения")]
        [Tooltip("Дальность сверх радиусов тел.")]
        public float reach = 0.9f;

        [Tooltip("Требовать захода со спины. В открытом бою почти невыполнимо — приберегаем для ударов из скрытности.")]
        public bool requiresBehindTarget = false;

        [Range(30f, 180f)]
        public float behindAngle = 120f;

        [Tooltip("Нужна ли цель. У скрытности и защитных приёмов цели нет.")]
        public bool requiresTarget = true;

        [Tooltip("Применяется только из скрытности.")]
        public bool requiresStealth = false;

        [Tooltip("Выводит из скрытности при применении. Верно почти для всего, кроме самой скрытности.")]
        public bool breaksStealth = true;

        [Tooltip("Включает и выключает скрытность. Это и есть способность «Скрытность».")]
        public bool togglesStealth = false;

        [Header("Анимация")]
        [Tooltip("Имя триггера в контроллере анимаций.")]
        public string animationTrigger = "Attack";

        [Tooltip("Задержка урона от начала анимации — момент касания клинка.")]
        public float impactDelay = 0.4f;

        /// <summary>
        /// Урон до броска на крит и отражение.
        ///
        /// У финишера берётся из таблицы по числу очков, у остальных —
        /// урон оружия плюс прибавка. Оружие приходит снаружи: способность
        /// не должна знать, кинжал в руке или меч.
        /// </summary>
        public int ComputeBaseDamage(int weaponDamage, int comboSpent)
        {
            if (!dealsDamage) return 0;

            if (comboRole == ComboRole.Finisher)
            {
                if (finisherDamage == null || finisherDamage.Length == 0) return weaponDamage;

                int index = Mathf.Clamp(comboSpent - 1, 0, finisherDamage.Length - 1);
                return finisherDamage[index].Roll();
            }

            // Множитель отдельно от прибавки: у сильных приёмов урон растёт
            // вместе с оружием, а не остаётся плоским. Иначе к шестидесятому
            // уровню способность превращается в щелчок по носу.
            return Mathf.RoundToInt(weaponDamage * weaponMultiplier) + bonusDamage;
        }

        /// <summary>Полный расчёт: база, затем бросок на крит и отражение.</summary>
        public int RollDamage(int weaponDamage, int comboSpent, out HitResult result)
        {
            int baseDamage = ComputeBaseDamage(weaponDamage, comboSpent);

            if (baseDamage <= 0)
            {
                result = HitResult.Normal;
                return 0;
            }

            return CombatMath.Roll(baseDamage, critChance, critMultiplier,
                                   missChance, missMultiplier, out result);
        }

        /// <summary>Сколько секунд оглушения даст способность при данном числе очков.</summary>
        public float ComputeStun(int comboSpent)
        {
            if (stunBase <= 0f && stunPerCombo <= 0f) return 0f;
            return stunBase + stunPerCombo * Mathf.Max(0, comboSpent);
        }
    }
}
