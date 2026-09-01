using UnityEngine;
using IsoRPG.Combat;

namespace IsoRPG.Quests
{
    /// <summary>
    /// NPC, выдающий и принимающий квест.
    ///
    /// Знак над головой — не украшение, а единственный способ отличить того,
    /// с кем стоит говорить, от декорации. В играх этого жанра он читается
    /// мгновенно и без обучения: восклицательный — есть работа, вопросительный
    /// — работа сделана, приходи за наградой.
    /// </summary>
    public sealed class QuestGiver : MonoBehaviour
    {
        // Два разных жёлтых, а не оттенки одного: «есть работа» и «работа
        // сделана» — разные сообщения, и различаться они должны и формой, и
        // цветом. Вопросительный ярче: он зовёт вернуться, и его надо
        // заметить с другого конца локации.
        private static readonly Color OfferColor = new Color(1f, 0.78f, 0.15f);
        private static readonly Color TurnInColor = new Color(1f, 0.95f, 0.25f);

        [SerializeField] private QuestDefinition quest;

        [Tooltip("Высота знака над головой.")]
        [SerializeField] private float markerHeight = 2.35f;

        [Tooltip("Материал знака. Ассетом, иначе шейдер может не попасть в сборку.")]
        [SerializeField] private Material markerMaterial;

        public void SetupMarkerMaterial(Material material) => markerMaterial = material;

        [Tooltip("Ближе этого расстояния с NPC можно говорить.")]
        [SerializeField] private float talkRange = 3.4f;

        [Tooltip("Насколько высоко подпрыгивает знак.")]
        [SerializeField] private float bobAmplitude = 0.3f;

        [Tooltip("Прыжков в секунду.")]
        [SerializeField] private float bobSpeed = 1.15f;

        [Tooltip("Скорость поворота к собеседнику.")]
        [SerializeField] private float turnSpeed = 240f;

        [Tooltip("Поправка, если модель смотрит не по оси Z. Обычно 0 или 180.")]
        [SerializeField] private float modelYawOffset = 0f;

        private GameObject marker;
        private Transform watched;
        private Transform markerBody;
        private QuestLog log;

        public QuestDefinition Quest => quest;
        public float TalkRange => talkRange;

        public void Setup(QuestDefinition definition) => quest = definition;

        /// <summary>
        /// Как зовут собеседницу. Имя объекта в сцене, а не отдельное поле:
        /// сборщик сцены называет объект осмысленно, и второе имя рядом
        /// разошлось бы с первым при первой же правке.
        /// </summary>
        public string DisplayName => name;

        private void Start()
        {
            BuildMarker();
            Refresh();
        }

        private void OnEnable()
        {
            // Журнал ищем у игрока: NPC про игрока ничего не знает, а знак
            // над ним зависит именно от состояния квеста у КОНКРЕТНОГО игрока.
            // В одиночной игре он один, в сетевой это место придётся менять —
            // и лучше, чтобы оно было одно.
            log = FindAnyObjectByType<QuestLog>();

            if (log != null) log.Changed += Refresh;
        }

        private void OnDisable()
        {
            if (log != null) log.Changed -= Refresh;
        }

        private void Update()
        {
            // Общее правило поведения НПС — одно на всех, см. NpcBehaviour.
            if (IsoRPG.World.NpcBehaviour.FaceHero) TurnToSpeaker();
            if (markerBody == null) return;

            // Знак заметно подпрыгивает, а не колышется. Плавное качание
            // взгляд не ловит: движение замечается по РЫВКУ, а не по
            // амплитуде. Отсюда кривая — быстрый подъём и медленное падение,
            // как у подпрыгивающего мячика.
            float phase = (Time.time * bobSpeed) % 1f;
            float jump = Mathf.Sin(phase * Mathf.PI);

            markerBody.localPosition = new Vector3(0f, jump * jump * bobAmplitude, 0f);
            markerBody.Rotate(Vector3.up, 55f * Time.deltaTime, Space.Self);
        }

        /// <summary>
        /// Провожает игрока взглядом, пока тот рядом.
        ///
        /// Постоянно, а не только по клику: NPC, поворачивающийся лишь в
        /// момент разговора, выглядит механизмом, который включили. А тот,
        /// что следит за подходящим, читается как живой — и заодно сам
        /// показывает, что с ним можно говорить.
        ///
        /// Радиус чуть больше дистанции разговора: поворот должен начаться
        /// ДО того, как игрок подойдёт вплотную, иначе он дёргается в
        /// последний момент.
        /// </summary>
        private void TurnToSpeaker()
        {
            if (watched == null)
            {
                var log = FindAnyObjectByType<QuestLog>();
                if (log == null) return;

                watched = log.transform;
            }

            Vector3 direction = watched.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.04f) return;
            if (direction.magnitude > talkRange * 2.2f) return;

            // Смещение на случай, если модель смотрит не по оси Z. У наборов
            // это встречается, и заметить можно только глазами — поэтому
            // поправка вынесена в настройку, а не зашита числом.
            var wanted = Quaternion.LookRotation(direction.normalized) *
                         Quaternion.Euler(0f, modelYawOffset, 0f);

            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted,
                                                          turnSpeed * Time.deltaTime);
        }

        /// <summary>Что показать в окне разговора прямо сейчас.</summary>
        public string CurrentText()
        {
            if (quest == null || log == null) return "Не сегодня, путник.";

            switch (log.StateOf(quest))
            {
                case QuestState.Available:     return quest.offerText;
                case QuestState.Active:        return quest.inProgressText;
                case QuestState.ReadyToTurnIn: return quest.completeText;
                default:                       return "Спасибо ещё раз.";
            }
        }

        public QuestState State => quest == null || log == null
            ? QuestState.Completed
            : log.StateOf(quest);

        /// <summary>
        /// Повернуться к тому, с кем говорим.
        ///
        /// NPC, отвечающий спиной, читается как декорация, которая случайно
        /// заговорила. Поворот стоит трёх строк и меняет ощущение целиком.
        /// </summary>
        public void FaceTo(Vector3 point)
        {
            // Оставлено для явного разворота по клику: сам поворот идёт
            // постоянно, но клик издалека должен сработать сразу.
            Vector3 direction = point - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.04f) return;

            transform.rotation = Quaternion.LookRotation(direction.normalized) *
                                 Quaternion.Euler(0f, modelYawOffset, 0f);
        }

        public void Accept()
        {
            if (log != null) log.Accept(quest);
        }

        public void TurnIn() => TurnIn(null);

        /// <summary>Сдать, взяв выбранную награду. Null — если выбора нет.</summary>
        public void TurnIn(IsoRPG.Items.ItemDefinition chosen)
        {
            if (log != null) log.TurnIn(quest, chosen);
        }

        // ------------------------------------------------------------------

        private void Refresh()
        {
            if (marker == null) return;

            var state = quest != null && log != null ? log.StateOf(quest) : QuestState.Completed;

            bool show = state == QuestState.Available || state == QuestState.ReadyToTurnIn;
            marker.SetActive(show);

            if (!show) return;

            // Форма знака меняется вместе со смыслом: восклицательный зовёт
            // взять работу, вопросительный — сдать. Один только цвет тут не
            // годится, разница должна читаться силуэтом.
            BuildShape(state == QuestState.Available);
        }

        private void BuildMarker()
        {
            marker = new GameObject("QuestMarker");
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = Vector3.up * markerHeight;

            markerBody = new GameObject("Body").transform;
            markerBody.SetParent(marker.transform, false);
        }

        private void BuildShape(bool exclamation)
        {
            // Пересобираем целиком: знаки разной формы, и переиспользовать
            // куски дороже, чем создать три примитива заново. Происходит это
            // считанные разы за игру.
            for (int i = markerBody.childCount - 1; i >= 0; i--)
                Destroy(markerBody.GetChild(i).gameObject);

            var color = exclamation ? OfferColor : TurnInColor;

            if (exclamation)
            {
                Bar(new Vector3(0f, 0.22f, 0f), new Vector3(0.12f, 0.42f, 0.12f), color);
                Bar(new Vector3(0f, -0.12f, 0f), new Vector3(0.12f, 0.12f, 0.12f), color);
                return;
            }

            // Вопросительный: крючок из трёх кусочков плюс точка. Грубо, но
            // на расстоянии от камеры силуэт узнаётся, а это и нужно.
            Bar(new Vector3(0f, 0.34f, 0f), new Vector3(0.30f, 0.11f, 0.11f), color);
            Bar(new Vector3(0.13f, 0.22f, 0f), new Vector3(0.11f, 0.22f, 0.11f), color);
            Bar(new Vector3(0f, 0.10f, 0f), new Vector3(0.11f, 0.20f, 0.11f), color);
            Bar(new Vector3(0f, -0.12f, 0f), new Vector3(0.12f, 0.12f, 0.12f), color);
        }

        private void Bar(Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(markerBody, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<Renderer>();
            // Материал берём ГОТОВЫМ ассетом, а не копируем у примитива.
            //
            // Копия живёт только в памяти, и её шейдер попадает в сборку
            // лишь по счастливой случайности: Unity включает туда шейдеры,
            // которые видит в сценах. Не попал — знак становится розовым,
            // и выглядит это как поломка модели, а не как отсутствие
            // шейдера.
            var material = markerMaterial != null
                ? new Material(markerMaterial) { color = color }
                : new Material(renderer.sharedMaterial) { color = color };

            // Знак светится сам: в вечерней сцене обычный материал уходит в
            // тень вместе со всем остальным, а метка обязана быть видна.
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.6f);
            }

            renderer.sharedMaterial = material;
        }
    }
}
