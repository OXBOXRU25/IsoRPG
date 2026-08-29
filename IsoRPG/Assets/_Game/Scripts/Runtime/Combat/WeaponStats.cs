using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Характеристики оружия в руках. Пока одно поле, но заведено отдельно
    /// намеренно: когда появится инвентарь, сюда будет писать надетый предмет,
    /// а весь боевой код продолжит спрашивать урон в одном и том же месте.
    ///
    /// Без этого «урон оружия» разбредётся по способностям константами, и
    /// смена кинжала на меч превратится в правку десятка файлов.
    /// </summary>
    public sealed class WeaponStats : MonoBehaviour
    {
        [Tooltip("Урон оружия. Кинжал — простейшее стартовое оружие, 10 единиц.")]
        [SerializeField] private int weaponDamage = 10;

        [Tooltip("Секунд между ударами. Это характеристика оружия: кинжал быстрый, двуручный меч медленный. Анимация подгоняется под это число, а не наоборот.")]
        [SerializeField] private float attackInterval = 1.4f;

        [Tooltip("Название — понадобится интерфейсу и подсказкам.")]
        [SerializeField] private string weaponName = "Кинжал";

        public int WeaponDamage => weaponDamage;
        public float AttackInterval => attackInterval;
        public string WeaponName => weaponName;

        /// <summary>Изменилось оружие — боевым системам нужно пересчитать ритм.</summary>
        public event System.Action Changed;

        public void Equip(string name, int damage, float interval)
        {
            weaponName = name;
            weaponDamage = Mathf.Max(0, damage);
            attackInterval = Mathf.Max(0.2f, interval);
            Changed?.Invoke();
        }
    }
}
