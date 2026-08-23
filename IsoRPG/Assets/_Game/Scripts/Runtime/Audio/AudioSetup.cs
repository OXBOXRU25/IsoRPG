using UnityEngine;

namespace IsoRPG.Audio
{
    /// <summary>
    /// Отдаёт банк звуков проигрывателю и ведёт фоновую музыку.
    ///
    /// Существует потому, что банк нельзя подключить из сборщика сцены:
    /// сборщик — редакторный код, он отрабатывает при нажатии пункта меню, а
    /// статические поля обнуляются при запуске игры. Ссылка должна лежать В
    /// СЦЕНЕ и ставиться в момент старта — иначе первый же удар обращается к
    /// пустому банку, и бой выходит беззвучным без единой ошибки в консоли.
    /// </summary>
    public sealed class AudioSetup : MonoBehaviour
    {
        [Tooltip("Банк звуков. Собирается через Tools/IsoRPG.")]
        [SerializeField] private SoundBank bank;

        [Tooltip("Плейлист. Пусто — тишина.")]
        [SerializeField] private AudioClip[] playlist;

        [Tooltip("Громкость музыки. Она фон, а не событие: тише всего остального.")]
        [Range(0f, 1f)]
        [SerializeField] private float musicVolume = 0.22f;

        private AudioSource musicSource;
        private int track = -1;
        private float nextTrackTime;

        public void Setup(SoundBank soundBank, AudioClip[] tracks)
        {
            bank = soundBank;
            playlist = tracks;
        }

        /// <summary>
        /// Громкость музыки, 0..1. Ноль — не пауза, а тишина: остановленный
        /// плейлист начал бы следующий трек с начала, стоит вернуть звук.
        /// </summary>
        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                if (musicSource != null) musicSource.volume = musicVolume;
            }
        }

        /// <summary>Единственный в сцене. Нужен окну настроек.</summary>
        public static AudioSetup Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            if (bank != null) Sfx.SetBank(bank);
            else Debug.LogWarning("[IsoRPG] Банк звуков не задан — игра будет беззвучной.");

            if (playlist == null || playlist.Length == 0) return;

            musicSource = gameObject.AddComponent<AudioSource>();

            // Не зацикливаем один трек: даже хороший, услышанный третий раз
            // подряд, начинает раздражать. Плейлист переключается сам.
            musicSource.loop = false;
            musicSource.volume = musicVolume;

            // Музыка не привязана к точке в мире: она не должна затихать,
            // когда камера отъезжает.
            musicSource.spatialBlend = 0f;
            musicSource.priority = 0;

            PlayNext();
        }

        private void Update()
        {
            if (musicSource == null || musicSource.isPlaying) return;

            // Трек кончился — берём следующий. Пауза перед ним нужна: музыка
            // встык звучит как радио, а тишина между темами даёт услышать
            // окружение.
            if (Time.time < nextTrackTime) return;

            PlayNext();
        }

        private void PlayNext()
        {
            if (playlist.Length == 0) return;

            // Следующий по кругу, а не случайный: случайный рано или поздно
            // повторит тот же трек подряд, и это слышно как сбой.
            track = (track + 1) % playlist.Length;

            musicSource.clip = playlist[track];
            musicSource.Play();

            nextTrackTime = Time.time + musicSource.clip.length + Random.Range(8f, 20f);
        }
    }
}
