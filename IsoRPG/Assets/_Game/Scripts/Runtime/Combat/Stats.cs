using System;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>Характеристики персонажа. Список закрытый — новые добавляются сюда.</summary>
    public enum StatType
    {
        Strength,   // Сила — немного добавляет урона
        Agility,    // Ловкость — основная для разбойника: урон, крит, уклонение
        Stamina,    // Выносливость — здоровье
    }

    /// <summary>Ресурсы, которые тратятся на действия.</summary>
    public enum ResourceType
    {
        Health,
        Energy,     // Разбойник. Копится сама, от уровня не зависит.
        Mana,       // Заготовка под будущие классы
        Rage,       // Заготовка под будущие классы
    }

    /// <summary>
    /// Набор характеристик. Структура, а не класс: складывается и умножается
    /// как число, и не порождает мусора при каждом пересчёте экипировки.
    /// </summary>
    [Serializable]
    public struct StatBlock
    {
        [SerializeField] private int strength;
        [SerializeField] private int agility;
        [SerializeField] private int stamina;

        public int Strength => strength;
        public int Agility => agility;
        public int Stamina => stamina;

        public StatBlock(int strength, int agility, int stamina)
        {
            this.strength = strength;
            this.agility = agility;
            this.stamina = stamina;
        }

        public int Get(StatType type) => type switch
        {
            StatType.Strength => strength,
            StatType.Agility => agility,
            StatType.Stamina => stamina,
            _ => 0
        };

        public static StatBlock operator +(StatBlock a, StatBlock b) =>
            new StatBlock(a.strength + b.strength,
                          a.agility + b.agility,
                          a.stamina + b.stamina);

        public static StatBlock operator *(StatBlock a, int k) =>
            new StatBlock(a.strength * k, a.agility * k, a.stamina * k);

        public override string ToString() =>
            $"Сила {strength}, Ловкость {agility}, Выносливость {stamina}";
    }
}
