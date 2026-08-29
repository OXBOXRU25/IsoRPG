using UnityEngine;
using IsoRPG.Player;

namespace IsoRPG.Items
{
    /// <summary>
    /// Поворачивает мирного жителя к подошедшему игроку.
    ///
    /// Торговец, стоящий спиной, читается как декорация: по декорациям не
    /// кликают. Один разворот в момент разговора эту беду лечит наполовину —
    /// пока не заговорил, он всё ещё спиной.
    ///
    /// Поворот плавный и только вокруг вертикали. Мгновенный рывок на месте
    /// выглядит как ошибка, а наклон к игроку сверху вниз — как поломка
    /// скелета.
    /// </summary>
    public sealed class FacePlayer : MonoBehaviour
    {
        [Tooltip("С какого расстояния замечает подошедшего.")]
        [SerializeField] private float noticeRange = 6f;

        [Tooltip("Скорость поворота, градусов в секунду.")]
        [SerializeField] private float turnSpeed = 220f;

        /// <summary>
        /// Как часто искать игрока. Не каждый кадр: поиск по сцене — дорогая
        /// операция, а игрок за десятую долю секунды далеко не убежит.
        /// </summary>
        private const float SearchInterval = 0.4f;

        private Transform player;
        private Quaternion home;
        private float nextSearch;

        /// <summary>
        /// С какого расстояния замечать. Шесть метров, стоявшие тут
        /// изначально, оказались меньше того, что видно на экране: камера
        /// показывает около тридцати, и торговец разворачивался, только
        /// когда игрок подходил вплотную. Всё остальное время он стоял
        /// спиной посреди зала и читался как декорация.
        /// </summary>
        public void SetNoticeRange(float range) => noticeRange = range;

        private void Awake()
        {
            home = transform.rotation;
        }

        private void Update()
        {
            if (player == null && Time.time >= nextSearch)
            {
                nextSearch = Time.time + SearchInterval;

                // Ищем по маршрутизатору ввода — он есть ТОЛЬКО у игрока.
                //
                // Сначала искал по выбору цели, но такой компонент носят и
                // монстры: поиск находил первого попавшегося скелета, и
                // торговец исправно поворачивался к нему.
                var found = FindFirstObjectByType<PlayerInputRouter>();
                if (found != null) player = found.transform;
            }

            if (player == null) return;

            var toPlayer = player.position - transform.position;
            toPlayer.y = 0f;

            // Далеко — возвращаемся в исходную позу, а не застываем вполоборота
            // к тому месту, где игрок был в прошлый раз.
            bool near = toPlayer.sqrMagnitude < noticeRange * noticeRange;

            var target = near && toPlayer.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toPlayer)
                : home;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}
