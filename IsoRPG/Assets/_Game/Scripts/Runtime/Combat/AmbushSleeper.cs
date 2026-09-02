using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Засада: существо стоит неподвижной декорацией, пока игрок не подойдёт.
    ///
    /// Заведено 02.09.2026 для гриба-монстра. В его наборе есть три статичные
    /// позы обычного гриба — автор рисовал их именно под это. Босс, который
    /// бежит навстречу через полполяны, читается как обычный моб покрупнее;
    /// гриб, оказавшийся живым, запоминается.
    ///
    /// Пока спит: аниматор держит статичную позу, навигация выключена, бой не
    /// ищет цель. Проснулся — всё включается разом и обратно уже не гаснет:
    /// зверь, который снова прикинулся грибом посреди драки, выглядел бы
    /// поломкой, а не хитростью.
    ///
    /// Цена: одно сравнение расстояний раз в полсекунды, и только пока спит.
    /// Это ММО — постоянных проверок в кадре мы себе не позволяем.
    /// </summary>
    // Аниматор НЕ требуем.
    //
    // Стояло `[RequireComponent(typeof(Animator))]`, и Unity добавляла пустой
    // аниматор на корень существа — рядом с настоящим, который живёт на
    // модели. Дальше любой поиск «первого аниматора в ветке» находил пустой:
    // зверь ездил по земле молча, а щупы показывали «контроллера нет» при
    // живой ссылке на контроллер в файле сцены. Мы ищем аниматор сами, ниже
    // по дереву, и в требовании не нуждаемся.
    public sealed class AmbushSleeper : MonoBehaviour
    {
        private static readonly int AsleepHash = Animator.StringToHash("Asleep");
        private static readonly int StaticHash = Animator.StringToHash("StaticVariant");
        private static readonly int BlockVariantHash = Animator.StringToHash("BlockVariant");

        [Tooltip("На каком расстоянии просыпается.")]
        [SerializeField] private float wakeRange = 7f;

        [Tooltip("Как часто проверяет, далеко ли игрок. Реже — дешевле.")]
        [SerializeField] private float checkEvery = 0.5f;

        [Tooltip("Подавать голос при пробуждении. Тот самый момент, ради которого засада и делалась.")]
        [SerializeField] private bool voiceOnWake = true;

        private Animator animator;
        private NavMeshAgent agent;
        private MonsterBrain brain;
        private Transform player;

        private float nextCheck;
        private bool awake;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            agent = GetComponent<NavMeshAgent>();
            brain = GetComponent<MonsterBrain>();

            Sleep();
        }

        private void Start()
        {
            // Игрока ищем ОДИН раз при старте, а не в каждой проверке: поиск
            // по сцене в повторяющемся коде — то, чем мы уже уронили игру.
            var router = Object.FindFirstObjectByType<IsoRPG.Player.PlayerInputRouter>();
            if (router != null) player = router.transform;
        }

        private void Update()
        {
            if (awake || player == null) return;
            if (Time.time < nextCheck) return;

            nextCheck = Time.time + checkEvery;

            // Сравниваем квадраты — корень тут не нужен.
            if ((player.position - transform.position).sqrMagnitude > wakeRange * wakeRange) return;

            Wake();
        }

        private void Sleep()
        {
            awake = false;

            // Поза «обычного гриба» выбирается случайно из трёх: одинаковые
            // позы у соседних грибов выдали бы копии одной модели.
            if (animator != null && Has(StaticHash))
                animator.SetInteger(StaticHash, Random.Range(1, 4));

            // Заодно вариант блока — он понадобится уже в бою, а ставить его
            // тут дешевле: один раз вместо проверки в каждом ударе.
            if (animator != null && Has(BlockVariantHash))
                animator.SetInteger(BlockVariantHash, Random.Range(1, 4));

            if (animator != null && Has(AsleepHash)) animator.SetBool(AsleepHash, true);
            if (agent != null) agent.enabled = false;
            if (brain != null) brain.enabled = false;
        }

        /// <summary>Разбудить принудительно — например, если ударили издалека.</summary>
        public void Wake()
        {
            if (awake) return;

            awake = true;

            if (animator != null && Has(AsleepHash)) animator.SetBool(AsleepHash, false);
            if (agent != null) agent.enabled = true;
            if (brain != null) brain.enabled = true;

            // Голос при пробуждении. Именно здесь, а не в мозге зверя: мозг
            // подаёт голос при захвате цели, а это уже второе событие — к тому
            // времени гриб успел встать, и «оказался живым» прозвучало бы
            // после того, как это стало видно.
            if (voiceOnWake) IsoRPG.Audio.Sfx.MushroomWake(transform.position);
        }

        private bool Has(int hash)
        {
            foreach (var p in animator.parameters)
                if (p.nameHash == hash) return true;

            return false;
        }
    }
}
