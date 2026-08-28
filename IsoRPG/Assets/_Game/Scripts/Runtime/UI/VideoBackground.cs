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

            // Пока кадра нет — картинка прозрачная.
            //
            // Картинка без текстуры рисуется белым непрозрачным
            // прямоугольником и накрывает всё, что под ней. Пока кадры шли
            // через отдельную текстуру, она стояла с самого начала и белого
            // не было; теперь текстура появляется только с первым кадром,
            // и до него фон становился белым листом.
            if (image != null) image.color = new Color(1f, 1f, 1f, 0f);

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

        /// <summary>
        /// Выход из игры: проигрыватель остановить ДО того, как движок начнёт
        /// сносить графику.
        ///
        /// Режим APIOnly означает, что кадры берутся прямо из декодера
        /// Media Foundation, минуя промежуточную текстуру. Если ролик всё ещё
        /// идёт, когда Unity разбирает графическое устройство, декодер пишет
        /// в то, чего уже нет, — и процесс падает без управляемого стека,
        /// уже на выходе. Наружу это выглядит как окно с ошибкой при
        /// закрытии игры.
        ///
        /// OnApplicationQuit приходит раньше OnDestroy и раньше выгрузки
        /// сцены, поэтому останавливаем здесь, а не там.
        /// </summary>
        private bool quitting;

        private void OnApplicationQuit()
        {
            quitting = true;
            StopPlayback();
        }

        private void OnDestroy()
        {
            StopPlayback();
        }

        private void StopPlayback()
        {
            if (player == null) return;

            player.loopPointReached -= OnLoopPoint;
            player.errorReceived -= OnError;

            if (player.isPlaying) player.Stop();
        }

        private void Start()
        {
            if (player != null && !player.isPlaying) player.Play();
        }

        private void OnLoopPoint(VideoPlayer source)
        {
            // Сами не перематываем.
            //
            // Раньше здесь стояло frame = 0 и Play — на случай сломанной
            // встроенной петли. Но при исправной петле это второй прыжок
            // поверх первого: движок уже вернулся к началу, а мы посылаем его
            // туда ещё раз, и на экране это видно рывком.
            //
            // Если петля всё-таки встанет, её поднимет сторож в Update —
            // он отличает настоящую остановку от обычного перехода по кругу.
        }

        private void OnError(VideoPlayer source, string message)
        {
            // Ошибка декодирования не всплывает никуда сама: ролик просто
            // перестаёт идти. Пишем её, иначе искать причину будет негде.
            Debug.LogWarning("[IsoRPG] Видео фона: " + message);
        }

        private void Update()
        {
            // После команды на выход сторож молчит: иначе он увидит
            // остановленный нами ролик как «замерший» и заново его запустит —
            // ровно в тот момент, когда останавливать и надо было.
            if (quitting) return;

            if (player == null || image == null) return;

            // Кадр отдаётся как текстура и меняется каждый раз — присваиваем
            // без проверок, это дешевле сравнения.
            if (player.texture != null)
            {
                image.texture = player.texture;

                // Первый пришедший кадр проявляет картинку. До него под ней
                // видна неподвижная заставка — она и служит первым кадром.
                if (image.color.a < 1f) image.color = Color.white;
            }

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
