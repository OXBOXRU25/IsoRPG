using UnityEngine;

namespace IsoRPG.Audio
{
    /// <summary>
    /// Непрерывный фон места: птицы, ветер, вода.
    ///
    /// Отдельно от остальных звуков, потому что живёт по другим правилам.
    /// Удар звучит полсекунды в точке на карте — его громкость решается в
    /// момент удара. Фон играет всё время и без источника: он не «откуда-то»,
    /// он везде. Поэтому у него свой источник, своя петля и своя громкость,
    /// которую надо менять на лету, а не при следующем воспроизведении.
    ///
    /// Тихо по умолчанию. Фон, который слышно, — это уже не фон: птицы,
    /// заглушающие шаги, читаются как звук, забытый включённым.
    /// </summary>
    public sealed class AmbienceLoop : MonoBehaviour
    {
        [Tooltip("Что играет фоном. Берётся из банка звуков, если не задано.")]
        [SerializeField] private AudioClip clip;

        [Tooltip("Своя громкость дорожки, до общей настройки.")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;

        [Tooltip("Сколько секунд нарастает при запуске.")]
        [SerializeField] private float fadeIn = 2.5f;

        private AudioSource source;
        private float target;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();

            source.loop = true;
            source.playOnAwake = false;

            // Без положения в мире: фон одинаков во всей локации, и затухать
            // при повороте камеры ему незачем.
            source.spatialBlend = 0f;
            source.volume = 0f;
        }

        private void OnEnable()
        {
            Sfx.AmbienceChanged += Refresh;
        }

        private void OnDisable()
        {
            Sfx.AmbienceChanged -= Refresh;
        }

        private void Start()
        {
            if (clip == null && Sfx.Bank != null) clip = SoundBank.Pick(Sfx.Bank.ambience);

            if (clip == null)
            {
                // Молчать нельзя: пустой фон выглядит как поломка звука, а не
                // как «дорожку ещё не положили».
                Debug.Log("[IsoRPG] Фонового звука нет — положи дорожку в банк " +
                          "звуков, поле «Окружение».");

                enabled = false;
                return;
            }

            source.clip = clip;
            source.Play();

            Refresh();
        }

        private void Update()
        {
            if (fadeIn <= 0.01f)
            {
                source.volume = target;
                return;
            }

            // Плавный вход: фон, включающийся рывком в первый кадр, слышно
            // именно как включение.
            source.volume = Mathf.MoveTowards(source.volume, target,
                                              Time.unscaledDeltaTime / fadeIn);
        }

        private void Refresh()
        {
            target = volume * Sfx.AmbienceVolume;
        }

        /// <summary>Заменить дорожку на ходу — на будущее, когда локаций станет больше.</summary>
        public void SetClip(AudioClip value)
        {
            clip = value;

            if (source == null) return;

            source.clip = clip;
            if (clip != null) source.Play();
        }
    }
}
