using UnityEngine;

namespace IsoRPG.Player
{
    /// <summary>
    /// Редкие позы ожидания, когда герой долго стоит.
    ///
    /// Выбор Павла 04.09.2026: основная боевая стойка одна, «а если долго
    /// стоим — можно проигрывать иногда 9, 10, 11, 12». Это отдельный раздел
    /// набора, `Combat Idle`, нарисованный именно под ожидание боя.
    ///
    /// Работает СЛОЕМ поверх, а не подменой клипа. Подмена дёрнула бы
    /// персонажа: позы начинаются с разных положений рук, и переход между
    /// ними получился бы скачком. Слой же вводится и убирается весом — это
    /// то же плавное смешивание, что и между аллюрами.
    ///
    /// Промежуток нарочно большой и случайный. Раз в пять секунд, как сперва
    /// прикидывали, поза менялась бы на глазах постоянно и читалась бы как
    /// дёрганье; в больших РПГ такие вставки идут раз в полминуты и именно
    /// поэтому воспринимаются как жизнь, а не как анимация.
    /// </summary>
    public sealed class IdleFidget : MonoBehaviour
    {
        [Tooltip("Через сколько секунд покоя может пойти поза ожидания, минимум.")]
        [SerializeField] private float minGap = 30f;

        [Tooltip("То же, максимум. Промежуток случайный, иначе вставки идут по метроному.")]
        [SerializeField] private float maxGap = 50f;

        [Tooltip("Сколько длится сама поза, секунд.")]
        [SerializeField] private float hold = 3.2f;

        [Tooltip("За сколько секунд слой вводится и убирается.")]
        [SerializeField] private float blend = 0.35f;

        [Tooltip("Сколько поз в слое. Ставит задание hero-move.")]
        [SerializeField] private int poses = 4;

        private static readonly int FidgetHash = Animator.StringToHash("FidgetPick");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private Animator animator;
        private CharacterAnimatorDriver driver;
        private int layer = -1;

        private float nextAt;
        private float until;
        private float weight;

        public void Setup(int poseCount) => poses = Mathf.Max(1, poseCount);

        private void Start()
        {
            animator = GetComponentInChildren<Animator>(true);

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                enabled = false;
                return;
            }

            layer = animator.GetLayerIndex("Оживление");

            if (layer <= 0)
            {
                // Молчать нельзя: слоя нет — значит задание сборки его не
                // положило, и снаружи это неотличимо от «поз не нашлось».
                Debug.LogWarning("[IsoRPG] Нет слоя «Оживление» — редкие позы ожидания не заиграют.");
                enabled = false;
                return;
            }

            Schedule();
        }

        private void Update()
        {
            // Стоит ли герой. Двигается — никаких поз: вставка посреди бега
            // выглядит как сбой, а не как характер.
            // В бою поз ожидания не бывает.
            //
            // Слой играет ПОВЕРХ всего, включая удар: Павлон 04.09.2026
            // «анимации боя прерываются накладывающимися сверху анимациями
            // состояния ожидания». Так и есть — замах перекрывался позой,
            // потому что проверка была одна: стоит ли герой. А стоит он и
            // между ударами тоже.
            //
            // Позы — про безделье, а не про паузу в драке.
            if (driver == null) driver = GetComponent<CharacterAnimatorDriver>();

            bool fighting = driver != null && driver.InCombat;

            bool still = !fighting && animator.GetFloat(SpeedHash) < 0.05f;

            if (!still)
            {
                Fade(0f);
                if (weight <= 0.01f) Schedule();
                return;
            }

            bool playing = Time.time < until;

            // Новую позу заводим ТОЛЬКО когда слой полностью убран.
            //
            // Первая версия перепланировала время после угасания, а проверку
            // делала до него — и следующая поза стартовала в том же кадре,
            // когда кончилась предыдущая: позы шли нон-стоп и менялись при
            // поднятом весе, то есть скачком. Павлон 04.09.2026: «меняются
            // резко одномоментно, получается дёргание».
            //
            // Теперь срок следующей ставится СРАЗУ при запуске текущей.
            // Пока он не наступил, слой лежит на нуле и герой стоит в своей
            // основной стойке — ровно как просили: подождали, показали одну
            // позу, плавно вернулись.
            if (!playing && weight <= 0.01f && Time.time >= nextAt)
            {
                animator.SetFloat(FidgetHash, Random.Range(0, poses));

                // С начала, а не с того места, где слой погас в прошлый раз.
                //
                // Состояние на слое продолжает крутиться и при нулевом весе:
                // без перезапуска вторая поза показалась бы с середины —
                // герой дёрнулся бы в неё, вместо того чтобы войти плавно.
                animator.Play("Fidget", layer, 0f);

                until = Time.time + hold;
                nextAt = until + Random.Range(minGap, maxGap);
            }

            Fade(playing ? 1f : 0f);
        }

        private void Schedule()
        {
            nextAt = Time.time + Random.Range(minGap, maxGap);
            until = 0f;
        }

        private void Fade(float target)
        {
            if (Mathf.Approximately(weight, target)) return;

            weight = Mathf.MoveTowards(weight, target, Time.deltaTime / Mathf.Max(0.01f, blend));
            animator.SetLayerWeight(layer, weight);
        }
    }
}
