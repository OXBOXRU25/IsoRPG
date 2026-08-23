using UnityEngine;

namespace IsoRPG.Audio
{
    /// <summary>
    /// Все звуки игры в одном ассете.
    ///
    /// Лежат наборами, а не по одному клипу на событие: один и тот же звук
    /// удара, услышанный трижды подряд, перестаёт быть звуком удара и
    /// становится сигналом «динь». Три-четыре варианта решают это полностью,
    /// и стоит это ноль — клипы всё равно уже есть в наборе.
    ///
    /// Ассетом, а не константами в коде, по той же причине, что и остальные
    /// игровые данные: заменить звук должно быть можно перетаскиванием в
    /// инспекторе, без компиляции и без меня.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundBank", menuName = "IsoRPG/Банк звуков")]
    public sealed class SoundBank : ScriptableObject
    {
        [Header("Бой")]
        [Tooltip("Удар клинком — наш кинжал.")]
        public AudioClip[] bladeHit;

        [Tooltip("Тяжёлый удар — топор скелета.")]
        public AudioClip[] heavyHit;

        [Tooltip("Выстрел из лука.")]
        public AudioClip[] bowShot;

        [Tooltip("Достать оружие: начало боя.")]
        public AudioClip[] drawWeapon;

        [Tooltip("Смерть существа.")]
        public AudioClip[] death;

        [Header("Добыча")]
        public AudioClip[] gold;
        public AudioClip[] pickup;
        public AudioClip[] equip;

        [Header("Интерфейс")]
        public AudioClip[] openWindow;
        public AudioClip[] closeWindow;
        public AudioClip[] levelUp;

        [Header("Шаги")]
        public AudioClip[] stepStone;
        public AudioClip[] stepGrass;

        [Header("Голоса")]
        [Tooltip("Скрип костей нежити. Для скелета он выразительнее рычания.")]
        public AudioClip[] boneVoice;

        [Header("Музыка")]
        [Tooltip("Плейлист. Играется по кругу вперемешку.")]
        public AudioClip[] music;

        /// <summary>
        /// Случайный клип из набора. Пустой набор — тишина, и это законно:
        /// звук может быть ещё не подобран, а игра должна работать.
        /// </summary>
        public static AudioClip Pick(AudioClip[] set)
        {
            if (set == null || set.Length == 0) return null;
            return set[Random.Range(0, set.Length)];
        }
    }
}
