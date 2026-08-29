using System.Collections.Generic;
using UnityEngine;

namespace IsoRPG.Audio
{
    /// <summary>
    /// Фоновая музыка в игре: девять тем по кругу, вперемешку.
    ///
    /// <b>Почему не одна зацикленная тема.</b> Одна дорожка на весь сеанс
    /// приедается за двадцать минут и начинает мешать — игрок слышит петлю
    /// и выключает звук. Девять тем вперемешку дают полтора часа без
    /// повтора.
    ///
    /// <b>Порядок мешаем, но соседей не повторяем.</b> Обычная случайность
    /// иногда ставит одну и ту же дорожку дважды подряд, и это слышно как
    /// сбой. Поэтому берём перемешанный список и проходим его целиком,
    /// прежде чем мешать заново.
    ///
    /// <b>Между темами пауза.</b> Тишина в несколько секунд отделяет одну
    /// вещь от другой; без неё склейка читается как обрыв.
    ///
    /// Громкость идёт через тот же канал, что и остальная музыка, — им
    /// управляет ползунок в настройках.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class MusicPlaylist : MonoBehaviour
    {
        /// <summary>Где лежат дорожки — папка внутри Resources.</summary>
        private const string Folder = "Music";

        /// <summary>Пауза между темами, секунды.</summary>
        private const float Gap = 6f;

        /// <summary>Сколько времени занимает нарастание и затухание, секунды.</summary>
        private const float Fade = 2.5f;

        private static MusicPlaylist instance;

        private AudioSource source;
        private readonly List<AudioClip> order = new List<AudioClip>();
        private AudioClip[] all;

        private int next;
        private float silentUntil;
        private float target = 1f;

        private void Awake()
        {
            // Одна на всю игру: при смене сцены музыка не должна начинаться
            // заново — это самый заметный признак склейки.
            if (instance != null && instance != this) { Destroy(gameObject); return; }

            instance = this;
            DontDestroyOnLoad(gameObject);

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;   // музыка не привязана к месту
            source.volume = 0f;

            all = Resources.LoadAll<AudioClip>(Folder);

            if (all == null || all.Length == 0)
            {
                Debug.LogWarning("[IsoRPG] Музыки в Resources/" + Folder + " не найдено.");
                enabled = false;
                return;
            }

            Debug.Log("[IsoRPG] Фоновая музыка: дорожек " + all.Length + ".");
            Shuffle();
        }

        private void Update()
        {
            if (all == null || all.Length == 0) return;

            float wanted = target * Volume;

            // Плавно ведём громкость к нужной: резкий старт дорожки бьёт
            // по ушам, резкий обрыв слышен как выдернутый шнур.
            source.volume = Mathf.MoveTowards(source.volume, wanted, Time.unscaledDeltaTime / Fade);

            if (source.isPlaying)
            {
                // Затухаем к концу дорожки.
                float left = source.clip.length - source.time;
                target = left < Fade ? Mathf.Clamp01(left / Fade) : 1f;
                return;
            }

            if (Time.unscaledTime < silentUntil) return;

            Play();
        }

        private void Play()
        {
            if (next >= order.Count) Shuffle();

            source.clip = order[next++];
            source.volume = 0f;
            target = 1f;
            source.Play();

            silentUntil = Time.unscaledTime + source.clip.length + Gap;
        }

        /// <summary>
        /// Перемешать список, не оставив ту же дорожку на стыке кругов.
        /// </summary>
        private void Shuffle()
        {
            AudioClip last = order.Count > 0 ? order[order.Count - 1] : null;

            order.Clear();
            order.AddRange(all);

            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            // Если первой снова выпала та, что играла последней, — меняем
            // её местами со второй. Повтор подряд слышен как сбой.
            if (last != null && order.Count > 1 && order[0] == last)
                (order[0], order[1]) = (order[1], order[0]);

            next = 0;
        }

        // ------------------------------------------------------------------

        /// <summary>Громкость музыкального канала, 0..1. Ведёт её окно настроек.</summary>
        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat("isorpg.volume.music", 0.22f));
        }
    }
}
