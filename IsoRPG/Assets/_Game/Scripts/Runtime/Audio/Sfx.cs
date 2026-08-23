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

        // --- Короткие вызовы под каждое событие --------------------------
        // Отдельные методы, а не один с перечислением: так место вызова
        // читается вслух («здесь играет удар клинком»), и опечатка в имени
        // ловится компилятором, а не ухом.

        public static void BladeHit(Vector3 at) => Play(bank?.bladeHit, at);
        public static void HeavyHit(Vector3 at) => Play(bank?.heavyHit, at);
        public static void BowShot(Vector3 at) => Play(bank?.bowShot, at);
        public static void DrawWeapon(Vector3 at) => Play(bank?.drawWeapon, at);
        public static void Death(Vector3 at) => Play(bank?.death, at);

        public static void Gold(Vector3 at) => Play(bank?.gold, at);
        public static void Pickup(Vector3 at) => Play(bank?.pickup, at);
        public static void Equip(Vector3 at) => Play(bank?.equip, at);

        public static void OpenWindow() => Play2D(bank?.openWindow);
        public static void CloseWindow() => Play2D(bank?.closeWindow);
        // Тише прочего: повышение уровня и так заметно полоской и записью
        // в логе, звук тут — подтверждение, а не объявление о победе.
        public static void LevelUp() => Play2D(bank?.levelUp, 0.35f);

        // ------------------------------------------------------------------

        public static void Play(AudioClip[] set, Vector3 at, float volume = 1f,
                                float pitchSpread = 0.08f)
        {
            var clip = SoundBank.Pick(set);
            if (clip == null) return;

            var source = Ensure().Take();
            source.transform.position = at;
            source.spatialBlend = 1f;   // объёмный: слышно, откуда
            source.volume = volume;
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
        public static void Play2D(AudioClip[] set, float volume = 1f)
        {
            var clip = SoundBank.Pick(set);
            if (clip == null) return;

            var source = Ensure().Take();
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
            source.volume = volume;

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
                source.minDistance = 4f;
                source.maxDistance = 34f;

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
