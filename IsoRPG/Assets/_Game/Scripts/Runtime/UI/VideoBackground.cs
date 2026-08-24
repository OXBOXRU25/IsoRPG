using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace IsoRPG.UI
{
    /// <summary>
    /// Живой фон стартового экрана.
    ///
    /// Раньше кадры шли через отдельную текстуру, которую проигрыватель
    /// рисовал, а картинка на холсте показывала. Ролик при этом проигрывался
    /// один раз и замирал: собственное зацикливание не помогало, сторож,
    /// следивший за состоянием, тоже — проигрыватель считал себя работающим.
    ///
    /// Здесь промежуточной текстуры нет вовсе. Проигрыватель отдаёт кадр сам,
    /// а мы каждый кадр берём его и показываем. Так нечему застревать: если
    /// картинка перестала меняться, это видно сразу и по тому же признаку,
    /// по которому это видит человек.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    [RequireComponent(typeof(RawImage))]
    public sealed class VideoBackground : MonoBehaviour
    {
        private VideoPlayer player;
        private RawImage image;

        /// <summary>Как часто проверять, что кадры вообще идут.</summary>
        private const float CheckInterval = 0.5f;

        /// <summary>
        /// Сколько проверок подряд кадр может стоять, прежде чем считать ролик
        /// замершим. Три — полторы секунды: заминка при загрузке в это
        /// укладывается, а настоящая остановка не успевает надоесть.
        /// </summary>
        private const int StalledLimit = 3;

        private float nextCheck;
        private long lastFrame = -1;
        private int stalled;

        private void Awake()
        {
            player = GetComponent<VideoPlayer>();
            image = GetComponent<RawImage>();

            if (player == null) return;

            // Кадры забираем сами, без промежуточной текстуры.
            player.renderMode = VideoRenderMode.APIOnly;
            player.isLooping = true;
            player.playOnAwake = true;

            // Пропускать кадры при нехватке времени можно: это фон, и лучше
            // потерять кадр, чем задержать всю картину.
            player.skipOnDrop = true;

            player.loopPointReached += OnLoopPoint;
            player.errorReceived += OnError;
        }

        private void OnDestroy()
        {
            if (player == null) return;

            player.loopPointReached -= OnLoopPoint;
            player.errorReceived -= OnError;
        }

        private void Start()
        {
            if (player != null && !player.isPlaying) player.Play();
        }

        private void OnLoopPoint(VideoPlayer source)
        {
            // Перематываем и запускаем вручную. При исправной встроенной петле
            // это ничего не меняет, при сломанной — заменяет её.
            source.frame = 0;
            source.Play();
        }

        private void OnError(VideoPlayer source, string message)
        {
            // Ошибка декодирования не всплывает никуда сама: ролик просто
            // перестаёт идти. Пишем её, иначе искать причину будет негде.
            Debug.LogWarning("[IsoRPG] Видео фона: " + message);
        }

        private void Update()
        {
            if (player == null || image == null) return;

            // Кадр отдаётся как текстура и меняется каждый раз — присваиваем
            // без проверок, это дешевле сравнения.
            if (player.texture != null) image.texture = player.texture;

            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckInterval;

            if (!player.isPrepared) return;

            long frame = player.frame;

            if (frame != lastFrame)
            {
                lastFrame = frame;
                stalled = 0;
                return;
            }

            stalled++;
            if (stalled < StalledLimit) return;

            // Полная перезагрузка: замерший проигрыватель часто считает себя
            // работающим, и один Play на нём не делает ничего.
            player.Stop();
            player.frame = 0;
            player.Play();

            stalled = 0;
        }
    }
}
