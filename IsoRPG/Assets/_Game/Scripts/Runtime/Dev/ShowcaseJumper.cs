using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using IsoRPG.Player;
using IsoRPG.Combat;

namespace IsoRPG.Dev
{
    /// <summary>
    /// Перенос героя к площадкам витрины наборов по клавишам F1…F9.
    ///
    /// Зачем вообще: площадки стоят за краем игровой карты, дойти до них
    /// пешком нельзя — между ними и руинами нет ни земли, ни навигации.
    /// Без переноса витрину видно только в редакторе, а смотреть набор надо
    /// с игровой камеры и рядом с собственным персонажем: вопрос ведь не
    /// «красивы ли модели», а «не выглядят ли НАШИ люди в них чужими».
    ///
    /// Живёт только пока в сцене есть витрина: её собирает и убирает пункт
    /// меню, вместе с ней уходит и этот компонент. В игру, собранную без
    /// витрины, не попадает ничего.
    ///
    /// Клавиши именно функциональные: цифры 0…5 заняты умениями, и перенос
    /// на них воровал бы удар посреди боя.
    /// </summary>
    public sealed class ShowcaseJumper : MonoBehaviour
    {
        [Tooltip("Куда ставить героя — по площадке на запись.")]
        public Vector3[] Spots = new Vector3[0];

        [Tooltip("Названия наборов, тем же порядком: пишутся в журнал.")]
        public string[] Titles = new string[0];

        [Tooltip("Куда возвращает F10 — середина зала.")]
        public Vector3 Home;

        [Tooltip("Дополнительная точка на клавише End — пробное подземелье.")]
        public Vector3 Extra;

        [Tooltip("Как называть дополнительную точку в журнале.")]
        public string ExtraTitle = "";

        [Tooltip("Эталонная комната на новом наборе — клавиша Home.")]
        public Vector3 Room;

        [Tooltip("Как называть комнату в журнале.")]
        public string RoomTitle = "";

        [Tooltip("Во сколько раз ускорить героя на время осмотра. F11 — выключить.")]
        public float SpeedBoost = 2f;

        private float baseSpeed = -1f;
        private bool boosted;
        private bool peaceful;
        private NavMeshAgent boostedAgent;
        private Animator boostedAnimator;

        private void Start()
        {
            Boost(true);

            // Монстров НЕ усыпляем сами. Витрина живёт в той же сцене, где
            // идёт обычная игра, и тихий мир посреди боя читается как
            // поломка: «мобы перестали двигаться». Тишина включается только
            // по F12, когда она действительно нужна — на площадках.
        }

        private void OnDisable()
        {
            Boost(false);
            Peace(false);
        }

        /// <summary>
        /// Тишина на время осмотра: монстры перестают преследовать.
        ///
        /// Витрину собирают, чтобы разглядывать декорации, а не отбиваться.
        /// Пока герой стоит и вертит камеру, к нему успевают сойтись скелет,
        /// волк, стрелок и колдун разом — и осмотр превращается в бой,
        /// который никто не хотел.
        ///
        /// Гасим сам мозг, а не урон: монстр остаётся на месте и его
        /// по-прежнему видно и можно рассмотреть — просто он больше не идёт
        /// за героем. Снимаешь витрину — всё возвращается.
        /// </summary>
        private void Peace(bool on)
        {
            if (on == peaceful) return;

            var brains = Object.FindObjectsByType<MonsterBrain>(FindObjectsSortMode.None);

            foreach (var brain in brains) brain.enabled = !on;

            peaceful = on;

            if (brains.Length > 0)
                Debug.Log("[IsoRPG] Осмотр: монстров " + brains.Length +
                          (on ? " усыплено. F12 — разбудить." : " разбужено."));
        }

        /// <summary>
        /// Ускорение героя на время осмотра — временное и обратимое.
        ///
        /// Скорость правится в рантайме, а не в сборщике сцены, ровно по
        /// одной причине: сборщик строит песочницу с нуля и стёр бы витрину
        /// вместе со всеми площадками. Здесь же ускорение живёт ровно
        /// столько, сколько живёт витрина.
        ///
        /// Анимацию разгоняем слабее, чем ноги: ходьба и бег смешиваются по
        /// скорости, и на удвоенной бег уже упёрся в потолок — ноги начинают
        /// скользить по земле. Полтора раза человек читает как быстрый бег,
        /// два — как перемотку, поэтому берём середину.
        /// </summary>
        private void Boost(bool on)
        {
            if (on == boosted) return;

            if (on)
            {
                var router = Object.FindFirstObjectByType<PlayerInputRouter>();
                if (router == null) return;

                boostedAgent = router.GetComponent<NavMeshAgent>();
                boostedAnimator = router.GetComponentInChildren<Animator>();

                if (boostedAgent == null) return;

                baseSpeed = boostedAgent.speed;
                boostedAgent.speed = baseSpeed * SpeedBoost;

                if (boostedAnimator != null)
                    boostedAnimator.speed = Mathf.Lerp(1f, SpeedBoost, 0.6f);

                boosted = true;
                Debug.Log("[IsoRPG] Осмотр: скорость " + baseSpeed.ToString("0.0") + " → " +
                          boostedAgent.speed.ToString("0.0") + ". F11 — вернуть обычную.");
            }
            else
            {
                if (boostedAgent != null && baseSpeed > 0f) boostedAgent.speed = baseSpeed;
                if (boostedAnimator != null) boostedAnimator.speed = 1f;

                boosted = false;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (int i = 0; i < Spots.Length && i < 9; i++)
            {
                if (keyboard[Key.F1 + i].wasPressedThisFrame)
                {
                    Jump(Spots[i], i < Titles.Length ? Titles[i] : "площадка " + (i + 1));
                    return;
                }
            }

            if (keyboard.f10Key.wasPressedThisFrame)
            {
                Jump(Home, "зал");
                return;
            }


            if (keyboard.f12Key.wasPressedThisFrame)
            {
                Peace(!peaceful);
                return;
            }

            if (keyboard.f11Key.wasPressedThisFrame)
            {
                Boost(!boosted);
                Debug.Log("[IsoRPG] Ускорение осмотра: " + (boosted ? "включено" : "выключено"));
            }
        }

        /// <summary>
        /// Переносит героя вместе с его навигационным агентом.
        ///
        /// Через <c>NavMeshAgent.Warp</c>, а не присваиванием позиции: агент
        /// хранит собственное положение на сетке, и после простого
        /// присваивания он утаскивает тело обратно — персонаж «прилипает» к
        /// прежнему месту. Warp переставляет и то и другое разом.
        ///
        /// Если под точкой сетки не оказалось, Warp возвращает false и
        /// герой остаётся на месте. Это не поломка: значит навигацию после
        /// сборки витрины не перепекли.
        /// </summary>
        private void Jump(Vector3 to, string what)
        {
            // Через компонент ввода, а не по тегу: тег Player в сцене
            // никому не назначен, и поиск по нему молча возвращал бы пустоту —
            // клавиша нажимается, ничего не происходит, ошибки нет.
            var router = Object.FindFirstObjectByType<PlayerInputRouter>();
            if (router == null) return;

            var player = router.gameObject;
            var agent = player.GetComponent<NavMeshAgent>();

            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.ResetPath();

                if (!agent.Warp(to))
                {
                    Debug.LogWarning("[IsoRPG] Под точкой " + to + " нет навигационной " +
                                     "сетки — перепеки её пунктом «Витрина наборов: собрать».");
                    return;
                }
            }
            else
            {
                player.transform.position = to;
            }

            Debug.Log("[IsoRPG] Перенёс к набору: " + what);
        }
    }
}
