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
        [Tooltip("Взмах оружием. Играет в начале замаха, до попадания.")]
        public AudioClip[] swing;

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

        [Tooltip("Жевание. Длинный звук: играется своим источником, а не пулом.")]
        public AudioClip[] chewing;

        [Header("Интерфейс")]
        public AudioClip[] openWindow;
        public AudioClip[] closeWindow;
        public AudioClip[] levelUp;

        [Header("Шаги")]
        public AudioClip[] stepStone;
        public AudioClip[] stepGrass;

        [Tooltip("Скрип снега. Пока играет как шаги по земле.")]
        public AudioClip[] stepSnow;

        [Header("Голоса NPC")]
        [Tooltip("Приветствие торговца. Звучит при открытии лавки.")]
        public AudioClip[] voiceMerchant;

        [Tooltip("Приветствие собеседницы с квестом.")]
        public AudioClip[] voiceVillager;

        [Tooltip("Рык главаря. Редкий и громкий — событие, а не фон.")]
        public AudioClip[] bossRoar;

        [Header("Голоса")]
        [Tooltip("Скрип костей нежити. Для скелета он выразительнее рычания.")]
        public AudioClip[] boneVoice;

        [Header("Окружение")]
        [Tooltip("Непрерывный фон места: птицы, ветер, вода. Играет петлёй.")]
        public AudioClip[] ambience;

        [Header("Музыка")]
        [Tooltip("Плейлист. Играется по кругу вперемешку.")]
        public AudioClip[] music;

        /// <summary>
        /// Случайный клип из набора. Пустой набор — тишина, и это законно:
        /// звук может быть ещё не подобран, а игра должна работать.
        /// </summary>
        /// <summary>
        /// Последний сыгранный звук в каждом наборе.
        ///
        /// Ключ — сам массив: наборы живут в банке и не пересоздаются, так
        /// что ссылка стабильна на всю игру.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<AudioClip[], int> lastPicked =
            new System.Collections.Generic.Dictionary<AudioClip[], int>();

        /// <summary>
        /// Случайный звук из набора, но не тот же, что в прошлый раз.
        ///
        /// Чистая случайность честна и звучит плохо: из четырёх взмахов она
        /// спокойно выдаёт один и тот же три раза подряд, а в бою взмах
        /// звучит каждую секунду. Ухо ловит повтор мгновенно и читает его
        /// как заевший звук, а не как совпадение.
        ///
        /// Достаточно запретить повтор соседнего: набор из двух звуков при
        /// этом честно чередуется, из четырёх — остаётся случайным на слух.
        /// </summary>
        public static AudioClip Pick(AudioClip[] set)
        {
            if (set == null || set.Length == 0) return null;
            if (set.Length == 1) return set[0];

            int previous;
            if (!lastPicked.TryGetValue(set, out previous)) previous = -1;

            int index = Random.Range(0, set.Length);

            // Выпал прежний — берём следующий по кругу. Перебирать заново
            // нельзя: у набора из двух звуков цикл может не кончиться.
            if (index == previous) index = (index + 1) % set.Length;

            lastPicked[set] = index;
            return set[index];
        }
    }
}
