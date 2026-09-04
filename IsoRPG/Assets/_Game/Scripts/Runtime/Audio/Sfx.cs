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
        private static float combatVolume = 1f;

        /// <summary>
        /// Бой: удары, взмахи, голоса зверей, крики боли.
        ///
        /// Предложение Павла 05.09.2026, и оно снимает пограничный случай, на
        /// котором я мялся. Рык зверя — это и не фон места, и не отклик на
        /// действие игрока: он звучит сам по себе, но принадлежит бою. Пока
        /// каналов было четыре, его приходилось класть либо к лесу, либо к
        /// шагам, и в обоих случаях он мешал.
        ///
        /// Правило раздачи по каналам: <b>Бой</b> — всё, что звучит в драке;
        /// <b>Действия</b> — что делает герой сам (шаги, подбор, надевание);
        /// <b>Окружение</b> — что звучит без нас (лес, вода, вой вдалеке);
        /// <b>Интерфейс</b> — окна и джинглы.
        /// </summary>
        public static float CombatVolume
        {
            get => combatVolume;
            set => combatVolume = Mathf.Clamp01(value);
        }

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
        public static void Swing(Vector3 at) => Play(bank?.swing, at, 0.55f, 0.12f, combatVolume);

        public static void BladeHit(Vector3 at) => Play(bank?.bladeHit, at, 1f, 0.05f, combatVolume);
        public static void HeavyHit(Vector3 at) => Play(bank?.heavyHit, at, 1f, 0.05f, combatVolume);
        public static void BowShot(Vector3 at) => Play(bank?.bowShot, at);
        public static void DrawWeapon(Vector3 at) => Play(bank?.drawWeapon, at);
        public static void Death(Vector3 at) => Play(bank?.death, at, 1f, 0.05f, combatVolume);

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

        /// <summary>
        /// Громкость ВСЕХ звериных голосов — одна на всех.
        ///
        /// Число Павла 05.09.2026: «давай громкость всех рыков и хрюков сделаем
        /// 0.50». Дальность он оставил как была — она и не мешала.
        ///
        /// Одно число, а не своё у каждого зверя: раньше они разъезжались
        /// (0.90 у главаря, 0.70 у волка и кабана, 0.55 у воя), и подкрутить
        /// «всех разом» было нельзя — приходилось помнить четыре места.
        /// </summary>
        private const float BeastVolume = 0.5f;

        /// <summary>
        /// Звериные голоса идут по каналу ОКРУЖЕНИЯ.
        ///
        /// Павлон 05.09.2026, после первой правки: «всё равно очень громко» —
        /// и на кадре настроек видно почему. У него «Действия» стоят на 72%, а
        /// «Окружение» на 27%: рык шёл по самому громкому каналу из четырёх.
        ///
        /// По смыслу звериные голоса и есть фон места — они звучат сами по
        /// себе, без участия игрока, в отличие от ударов и шагов. Значит и
        /// ползунок у них должен быть тот, которым человек убавляет лес.
        /// </summary>
        /// <summary>Рык главаря при захвате цели.</summary>
        public static void BossRoar(Vector3 at) =>
            Play(bank?.bossRoar, at, BeastVolume, 0.04f, combatVolume);

        /// <summary>Рычание волка при захвате цели.</summary>
        public static void WolfSnarl(Vector3 at) =>
            Play(bank?.wolfSnarl, at, BeastVolume, 0.07f, combatVolume);

        /// <summary>
        /// Волчий вой. Редкий и дальний — для настроения места.
        ///
        /// Слышно за 90 метров против обычных 28: вой на то и вой, что
        /// доносится издалека. Павлон дальность оставил.
        ///
        /// Идёт по каналу ОКРУЖЕНИЯ, а не эффектов, и это отдельная починка:
        /// раньше он висел на эффектах, поэтому ползунок окружения на него не
        /// влиял вовсе — а по смыслу вой это фон места, а не событие боя.
        /// </summary>
        public static void WolfHowl(Vector3 at) =>
            Play(bank?.wolfHowl, at, BeastVolume, 0.05f, ambienceVolume, 90f);

        /// <summary>Хрюканье кабана.</summary>
        public static void BoarGrunt(Vector3 at) => Play(bank?.boarGrunt, at, BeastVolume, 0.1f, combatVolume);

        // --- Гриб-исполин -------------------------------------------------
        //
        // Своих звуков у набора InfinityPBR нет вовсе — ни одного файла на
        // весь пакет, проверено. Генерит их Павлон, промты лежат в
        // `Войс/sound-list.md`. Пока файлов нет, наборы пустые, и Play молча
        // ничего не играет — игра от этого не ломается.

        /// <summary>Пробуждение из засады: гриб оказался живым. Громче прочего — это событие.</summary>
        public static void MushroomWake(Vector3 at) => Play(bank?.mushroomWake, at, 0.9f, 0.04f, -1f, 45f);

        /// <summary>Замах гриба.</summary>
        public static void MushroomAttack(Vector3 at) => Play(bank?.mushroomAttack, at, 0.75f, 0.07f);

        /// <summary>Гриб получил урон.</summary>
        public static void MushroomHurt(Vector3 at) => Play(bank?.mushroomHurt, at, 0.7f, 0.08f);

        /// <summary>Гриб умирает.</summary>
        public static void MushroomDeath(Vector3 at) => Play(bank?.mushroomDeath, at, 0.85f, 0.05f);

        /// <summary>Холостой звук: сопение и хлюпанье, пока гриб просто стоит рядом.</summary>
        public static void MushroomIdle(Vector3 at) => Play(bank?.mushroomIdle, at, 0.55f, 0.09f);

        // ------------------------------------------------------------------

        /// <summary>Слышимость по умолчанию: полная громкость и полная тишина, метры.</summary>
        private const float NearRange = 8f, FarRange = 28f;

        public static void Play(AudioClip[] set, Vector3 at, float volume = 1f,
                                float pitchSpread = 0.08f, float channel = -1f,
                                float range = -1f)
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

            // Дальность задаём КАЖДЫЙ раз: источники живут в пуле и достаются
            // по кругу, а один дальний звук иначе оставил бы свою слышимость
            // следующему — и удар кинжалом было бы слышно через полкарты.
            source.maxDistance = range > 0f ? range : FarRange;
            source.minDistance = range > 0f ? Mathf.Min(NearRange, range * 0.25f) : NearRange;

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
