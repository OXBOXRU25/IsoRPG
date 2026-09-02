using UnityEngine;
using IsoRPG.Quests;

namespace IsoRPG.World
{
    /// <summary>
    /// Жесты НПС: машет при первой встрече и размахивает руками в разговоре.
    ///
    /// Замысел Павлона 02.09.2026: «пусть машет герою когда тот к нему первый
    /// раз подойдёт, и в процессе диалога использует жестикуляцию, а в режиме
    /// ожидания переминается и утирает лоб».
    ///
    /// Разделение тут не косметическое. Приветствие — событие: оно должно
    /// случиться ОДИН раз, при первой встрече, иначе НПС машет каждому проходу
    /// мимо и превращается в заводную игрушку. Жест разговора — тоже событие, и
    /// тоже одно: непрерывная жестикуляция без мимики читается как тик, а не
    /// как речь (правка Павлона по первому же показу). Новый жест выпадает при
    /// следующем открытии окна. А переминание — занятие,
    /// его ведёт общий механизм праздного поведения
    /// (<see cref="IsoRPG.Combat.IdleBehaviour"/>), тот же, что у зверей.
    ///
    /// Цена в кадре: пока не поздоровался — одно сравнение расстояний раз в
    /// полсекунды; после — ноль, проверка выключается совсем. Разговор
    /// приходит событием, мерить его не надо.
    /// </summary>
    public sealed class NpcGesture : MonoBehaviour
    {
        private static readonly int GreetHash = Animator.StringToHash("Greet");
        private static readonly int TalkingHash = Animator.StringToHash("Talking");
        private static readonly int TalkHash = Animator.StringToHash("Talk");
        private static readonly int TalkVariantHash = Animator.StringToHash("TalkVariant");

        [Tooltip("С какого расстояния здоровается при первой встрече.")]
        [SerializeField] private float greetRange = 6f;

        [Tooltip("Как часто проверяет, подошёл ли игрок. Реже — дешевле.")]
        [SerializeField] private float checkEvery = 0.5f;

        [Tooltip("Сколько разных жестов разговора есть в контроллере.")]
        [SerializeField] private int gestureCount = 54;

        private Animator animator;
        private QuestGiver giver;
        private Transform player;

        private float nextCheck;
        private bool wasFar;
        private bool greeted;
        private bool talking;

        /// <summary>Сколько жестов разговора собрано. Ставит задание сборки.</summary>
        public void SetGestures(int count) => gestureCount = Mathf.Max(1, count);

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>(true);
            giver = GetComponent<QuestGiver>();
        }

        private void Start()
        {
            // Игрока ищем ОДИН раз: поиск по сцене в повторяющемся коде — то,
            // чем мы уже роняли игру.
            var router = Object.FindFirstObjectByType<IsoRPG.Player.PlayerInputRouter>();
            if (router != null) player = router.transform;
        }

        private void OnEnable()
        {
            DialogueWindow.Started += OnTalkStarted;
            DialogueWindow.Ended += OnTalkEnded;
        }

        private void OnDisable()
        {
            DialogueWindow.Started -= OnTalkStarted;
            DialogueWindow.Ended -= OnTalkEnded;
        }

        private void Update()
        {
            if (animator == null) return;

            if (talking) return;

            // Поздоровался — больше здесь делать нечего вовсе.
            if (greeted || player == null) return;
            if (Time.time < nextCheck) return;

            nextCheck = Time.time + checkEvery;

            bool near = (player.position - transform.position).sqrMagnitude <= greetRange * greetRange;

            // Здороваться можно только с ПРИШЕДШИМ, а не с тем, кто уже
            // стоит рядом при загрузке.
            //
            // Талин стоит ровно в той точке, где герой появляется по
            // сохранению: расстояние около нуля, и приветствие отыгрывало в
            // первые полсекунды после загрузки, пока экран ещё гаснет.
            // Снаружи это выглядело как «перестал махать» — Павлон так и
            // сказал.
            if (!wasFar)
            {
                if (!near) wasFar = true;
                return;
            }

            if (!near) return;

            greeted = true;
            animator.SetTrigger(GreetHash);
        }

        private void OnTalkStarted(QuestGiver who)
        {
            // Событие общее на всех НПС — разбираем, к нам ли пришли.
            if (who == null || who != giver) return;

            talking = true;

            // Чистая случайность, без защиты от повтора: решение Павлона —
            // «не страшно если будут повторения, шанс выпадения одинаковых
            // должен остаться». При полусотне жестов совпадение подряд редко
            // и читается как случайность, а не как поломка.
            animator.SetInteger(TalkVariantHash, Random.Range(1, gestureCount + 1));
            animator.SetBool(TalkingHash, true);

            // Триггером, а не одним лишь флагом: по флагу переход из любого
            // состояния верен всё время разговора, и жест, доиграв, входил
            // сам в себя снова и снова. Триггер расходуется — жест играет
            // ровно один раз.
            animator.SetTrigger(TalkHash);
        }

        private void OnTalkEnded(QuestGiver who)
        {
            if (who == null || who != giver) return;

            talking = false;
            animator.SetBool(TalkingHash, false);
        }
    }
}
