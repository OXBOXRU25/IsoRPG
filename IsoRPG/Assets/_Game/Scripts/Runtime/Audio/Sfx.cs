using UnityEngine;

namespace IsoRPG.Audio
{
    /// <summary>
    /// Проигрыватель звуков.
    ///
    /// Точка входа статическая по той же причине, что и боевой лог: звук
    /// нужен отовсюду, и тянуть ссылку на проигрыватель через пять классов
    /// ради одного вызова — плохая сделка. Внутри обычный компонент, который
    /// создаётся сам при первом обращении.
    ///
    /// Источники живут в пуле. Создавать объект на каждый звук (как делает
    /// PlayClipAtPoint) в бою означает десятки созданий и удалений в секунду —
    /// это заметно на телефоне и бессмысленно на любом устройстве.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        private const int PoolSize = 12;

        private static Sfx instance;
        private static SoundBank bank;

        private AudioSource[] pool;
        private int next;

        /// <summary>Банк звуков. Ставится сборщиком сцены.</summary>
        public static void SetBank(SoundBank value) => bank = value;

        public static SoundBank Bank => bank;

        /// <summary>
        /// Общая громкость эффектов, 0..1. Множитель, а не подмена: у каждого
        /// звука своя громкость в месте вызова, и настройка игрока обязана
        /// сохранять их соотношение, иначе тихие звуки исчезнут раньше громких.
        /// </summary>
        public static float MasterVolume
        {
            get => masterVolume;
            set => masterVolume = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Общая громкость по умолчанию.
        ///
        /// Не единица: на полной громкости удары и голоса нежити заглушают
        /// всё остальное, и первое, что делает человек, — лезет убавлять
        /// звук. Настройка в меню по-прежнему работает и перекрывает это
        /// значение — здесь только то, с чего игра начинается.
        /// </summary>
        private static float masterVolume = 0.6f;

        /// <summary>
        /// Громкость по каналам, поверх общей.
        ///
        /// Роли у звуков разные, и слушать их на одной громкости неудобно:
        /// шаги и удары — это отклик на действие, их хочется слышать; писк
        /// интерфейса на той же громкости надоедает через пять минут; звук
        /// нового уровня должен пробиваться сквозь бой. Одним ползунком всё
        /// это настраивается только в одну сторону — в тишину.
        /// </summary>
        private static float effectsVolume = 1f;
        private static float systemVolume = 1f;
        private static float ambienceVolume = 1f;

        /// <summary>Шаги, удары, еда — всё, что делает мир и персонажи.</summary>
        public static float EffectsVolume
        {
            get => effectsVolume;
            set => effectsVolume = Mathf.Clamp01(value);
        }

        /// <summary>Интерфейс и джинглы: окна, новый уровень, монеты.</summary>
        public static float SystemVolume
        {
            get => systemVolume;
            set => systemVolume = Mathf.Clamp01(value);
        }

        /// <summary>Фон места: птицы, ветер, вода.</summary>
        public static float AmbienceVolume
        {
            get => ambienceVolume;
            set
            {
                ambienceVolume = Mathf.Clamp01(value);
                AmbienceChanged?.Invoke();
            }
        }

        /// <summary>
        /// Фоновый звук играет непрерывно, поэтому громкость ему надо менять
        /// на лету, а не при следующем воспроизведении.
        /// </summary>
        public static event System.Action AmbienceChanged;

        // --- Короткие вызовы под каждое событие --------------------------
        // Отдельные методы, а не один с перечислением: так место вызова
        // читается вслух («здесь играет удар клинком»), и опечатка в имени
        // ловится компилятором, а не ухом.

        /// <summary>
        /// Взмах — в начале замаха, а не при попадании.
        ///
        /// Разница слышна: со взмахом удар получает подготовку, и промах
        /// перестаёт быть тишиной. Без него бой звучит как череда попаданий
        /// без движения между ними.
        /// </summary>
        public static void Swing(Vector3 at) => Play(bank?.swing, at, 0.55f, 0.12f);

        public static void BladeHit(Vector3 at) => Play(bank?.bladeHit, at);
        public static void HeavyHit(Vector3 at) => Play(bank?.heavyHit, at);
        public static void BowShot(Vector3 at) => Play(bank?.bowShot, at);
        public static void DrawWeapon(Vector3 at) => Play(bank?.drawWeapon, at);
        public static void Death(Vector3 at) => Play(bank?.death, at);

        // Деньги, подбор и надевание — отклик интерфейса, а не мира:
        // звучат они у игрока в руках, а не в точке на карте.
        public static void Gold(Vector3 at) => Play(bank?.gold, at, 1f, 0.08f, systemVolume);
        public static void Pickup(Vector3 at) => Play(bank?.pickup, at, 1f, 0.08f, systemVolume);
        public static void Equip(Vector3 at) => Play(bank?.equip, at, 1f, 0.08f, systemVolume);

        public static void OpenWindow() => Play2D(bank?.openWindow, 1f, systemVolume);
        public static void CloseWindow() => Play2D(bank?.closeWindow, 1f, systemVolume);
        // Тише прочего: повышение уровня и так заметно полоской и записью
        // в логе, звук тут — подтверждение, а не объявление о победе.
        public static void LevelUp() => Play2D(bank?.levelUp, 0.85f, systemVolume);

        // Голоса NPC — заметно тише прочего.
        //
        // Приветствие звучит при каждом подходе к торговцу, а к нему подходят
        // десятки раз за сессию. На общей громкости это первое, что начинает
        // раздражать, и человек убавляет звук целиком.
        public static void MerchantVoice(Vector3 at) => Play(bank?.voiceMerchant, at, 0.5f, 0.05f);
        public static void VillagerVoice(Vector3 at) => Play(bank?.voiceVillager, at, 0.5f, 0.05f);

        /// <summary>Рык главаря. Громче остальных: это событие, а не реплика.</summary>
        public static void BossRoar(Vector3 at) => Play(bank?.bossRoar, at, 0.9f, 0.04f);

        // ------------------------------------------------------------------

        public static void Play(AudioClip[] set, Vector3 at, float volume = 1f,
                                float pitchSpread = 0.08f, float channel = -1f)
        {
            var clip = SoundBank.Pick(set);
            if (clip == null) return;

            // Канал по умолчанию — эффекты: в мире звучат шаги, удары и еда,
            // и перечислять их в каждом вызове значило бы однажды забыть.
            if (channel < 0f) channel = effectsVolume;

            var source = Ensure().Take();
            source.transform.position = at;
            source.spatialBlend = 1f;   // объёмный: слышно, откуда
            source.volume = volume * masterVolume * channel;
            source.pitch = RandomPitch(pitchSpread);
            source.clip = clip;
            source.Play();
        }

        /// <summary>
        /// Лёгкий разброс высоты тона.
        ///
        /// Без него сэмпл слышится как сэмпл: живой звук никогда не
        /// повторяется точь-в-точь, и ухо ловит идентичность мгновенно —
        /// именно она читается как «плоско» и «дёшево». Несколько процентов
        /// разброса убирают эффект полностью, при этом сам звук не меняется.
        ///
        /// Разброс несимметричный по восприятию: вверх тон уходит заметнее,
        /// чем вниз, поэтому берём чуть больше вниз.
        /// </summary>
        private static float RandomPitch(float spread)
        {
            if (spread <= 0.001f) return 1f;
            return 1f + Random.Range(-spread * 1.25f, spread);
        }

        /// <summary>
        /// Звук без положения в мире: интерфейс, повышение уровня.
        /// Такие не должны затихать, когда камера смотрит в сторону.
        /// </summary>
        public static void Play2D(AudioClip[] set, float volume = 1f, float channel = -1f)
        {
            var clip = SoundBank.Pick(set);
            if (clip == null) return;

            if (channel < 0f) channel = systemVolume;

            var source = Ensure().Take();
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
            source.volume = volume * masterVolume * channel;

            // Интерфейс и джинглы тоном не гуляют: там ожидается один и тот
            // же отклик, а разброс читался бы как расстроенный инструмент.
            source.pitch = 1f;
            source.clip = clip;
            source.Play();
        }

        // ------------------------------------------------------------------

        private static Sfx Ensure()
        {
            if (instance != null) return instance;

            var go = new GameObject("Sfx");
            DontDestroyOnLoad(go);

            instance = go.AddComponent<Sfx>();
            instance.Build();

            return instance;
        }

        private void Build()
        {
            pool = new AudioSource[PoolSize];

            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("Source" + i);
                child.transform.SetParent(transform, false);

                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;

                // Затухание по расстоянию линейное: логарифмическое по
                // умолчанию рассчитано на большие пространства, и у нас звук
                // пропадал бы уже через несколько метров.
                source.rolloffMode = AudioRolloffMode.Linear;

                // От слушателя, а он теперь на герое: восемь единиц вокруг
                // слышно полностью, к двадцати восьми звук сходит на нет.
                // Это примерно край видимой области — дальше слышать нечего.
                source.minDistance = 8f;
                source.maxDistance = 28f;

                pool[i] = source;
            }
        }

        /// <summary>
        /// Следующий источник по кругу.
        ///
        /// Занятый прерываем сознательно: в бою звуков больше, чем источников,
        /// и обрыв самого старого слышен меньше, чем пропажа нового.
        /// </summary>
        private AudioSource Take()
        {
            var source = pool[next];
            next = (next + 1) % pool.Length;

            return source;
        }
    }
}
