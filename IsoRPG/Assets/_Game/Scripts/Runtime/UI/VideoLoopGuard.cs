using UnityEngine;
using UnityEngine.Video;

namespace IsoRPG.UI
{
    /// <summary>
    /// Следит, чтобы фоновое видео не останавливалось.
    ///
    /// У проигрывателя есть собственное зацикливание, и оно включено. Но ролик
    /// всё равно замирал после первого прохода, причём проигрыватель при этом
    /// продолжал считать себя играющим — поэтому первая версия сторожа,
    /// смотревшая на «играет или нет», ничего не находила.
    ///
    /// Смотрим на то, что видит человек: меняется ли кадр. Если номер кадра
    /// стоит на месте несколько проверок подряд — ролик замер, чем бы это ни
    /// было вызвано, и его надо перезапустить.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class VideoLoopGuard : MonoBehaviour
    {
        private VideoPlayer player;

        /// <summary>
        /// Как часто проверять. Полсекунды: при двадцати четырёх кадрах
        /// в секунду за это время номер обязан измениться, а нагрузки такая
        /// проверка не создаёт.
        /// </summary>
        private const float Interval = 0.5f;

        /// <summary>
        /// Сколько проверок подряд кадр должен стоять, чтобы счесть ролик
        /// замершим. Три — это полторы секунды: короткая заминка при загрузке
        /// не считается поломкой, а настоящая остановка не успевает надоесть.
        /// </summary>
        private const int StalledLimit = 3;

        private float nextCheck;
        private long lastFrame = -1;
        private int stalled;

        private void Awake()
        {
            player = GetComponent<VideoPlayer>();

            if (player == null) return;

            // Ставим и отсюда: настройка могла не доехать до сборки, а
            // проверить это в готовой игре нечем.
            player.isLooping = true;

            // Конец ролика — самое место перезапустить его вручную, не
            // полагаясь на встроенную петлю.
            player.loopPointReached += OnLoopPoint;
        }

        private void OnDestroy()
        {
            if (player != null) player.loopPointReached -= OnLoopPoint;
        }

        private void OnLoopPoint(VideoPlayer source)
        {
            // Перемотка в начало и запуск. При работающей встроенной петле это
            // ничего не меняет, а при сломанной — чинит.
            source.frame = 0;
            source.Play();
        }

        private void Update()
        {
            if (player == null) return;
            if (Time.unscaledTime < nextCheck) return;

            nextCheck = Time.unscaledTime + Interval;

            if (!player.isPrepared) return;

            long frame = player.frame;

            if (frame == lastFrame)
            {
                stalled++;

                if (stalled >= StalledLimit)
                {
                    // Полная перезагрузка, а не просто Play: замерший
                    // проигрыватель часто считает себя работающим, и Play на
                    // нём не делает ничего.
                    player.Stop();
                    player.frame = 0;
                    player.Play();

                    stalled = 0;
                }
            }
            else
            {
                stalled = 0;
                lastFrame = frame;
            }
        }
    }
}
