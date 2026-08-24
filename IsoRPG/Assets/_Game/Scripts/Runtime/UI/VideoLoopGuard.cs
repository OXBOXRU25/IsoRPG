using UnityEngine;
using UnityEngine.Video;

namespace IsoRPG.UI
{
    /// <summary>
    /// Следит, чтобы фоновое видео не останавливалось.
    ///
    /// У проигрывателя есть собственное зацикливание, и оно включено. Но на
    /// деле ролик всё равно замирал после первого прохода: проигрыватель
    /// останавливается и по другим поводам — не хватило подготовленных кадров,
    /// окно потеряло фокус, система придержала декодер. Ни один из этих случаев
    /// не сообщает о себе ошибкой, а выглядят все одинаково — застывшая
    /// картинка вместо живого фона.
    ///
    /// Поэтому вместо разбирательства с причиной проверяем следствие: если
    /// проигрыватель готов, но не играет — запускаем снова.
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public sealed class VideoLoopGuard : MonoBehaviour
    {
        private VideoPlayer player;

        /// <summary>
        /// Как часто проверять. Каждый кадр незачем: остановку глаз замечает
        /// за доли секунды, а лишние обращения к проигрывателю бесплатными
        /// не бывают.
        /// </summary>
        private const float Interval = 0.5f;

        private float nextCheck;

        private void Awake()
        {
            player = GetComponent<VideoPlayer>();

            // Зацикливание на всякий случай ставим и отсюда: настройка могла
            // не доехать до сборки, а проверять это в готовой игре нечем.
            if (player != null) player.isLooping = true;
        }

        private void Update()
        {
            if (player == null) return;
            if (Time.unscaledTime < nextCheck) return;

            nextCheck = Time.unscaledTime + Interval;

            // Не готов — значит ещё раскручивается, это нормально.
            if (!player.isPrepared) return;

            if (!player.isPlaying) player.Play();
        }
    }
}
