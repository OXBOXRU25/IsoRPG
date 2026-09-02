using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>Кому принадлежит участник боя. Определяет, кто кому враг.</summary>
    public enum Faction
    {
        Player,
        Hostile,
        Neutral,
    }

    /// <summary>
    /// Метка «по мне можно кликнуть и выбрать целью».
    ///
    /// Держит имя для интерфейса, принадлежность и точку, над которой рисуется
    /// полоска здоровья. Отдельный компонент, а не поле в Health, потому что
    /// целью может быть и то, у чего здоровья нет — сундук, ресурсная жила,
    /// квестовый предмет.
    /// </summary>
    public sealed class Targetable : MonoBehaviour
    {
        /// <summary>
        /// Все живые метки на сцене.
        ///
        /// Нужен миникарте: без реестра ей пришлось бы обходить сцену каждый
        /// кадр, а это самый дорогой способ узнать то, что объекты и так могут
        /// сообщить о себе сами. Список статический, потому что сцена одна.
        /// </summary>
        private static readonly System.Collections.Generic.List<Targetable> all =
            new System.Collections.Generic.List<Targetable>();

        public static System.Collections.Generic.IReadOnlyList<Targetable> All => all;

        [SerializeField] private string displayName = "Существо";
        [SerializeField] private Faction faction = Faction.Hostile;

        [Tooltip("Высота, на которой висит полоска здоровья. Считается от точки под ногами.")]
        [SerializeField] private float overheadHeight = 2.2f;

        [Tooltip("Насколько близко нужно подойти, чтобы достать в ближнем бою. Считается от центра до центра.")]
        [SerializeField] private float bodyRadius = 0.5f;

        private Health health;

        public string DisplayName => displayName;
        public Faction Faction => faction;
        public float BodyRadius => bodyRadius;
        public Vector3 OverheadPoint => transform.position + Vector3.up * overheadHeight;

        /// <summary>Здоровье цели. Может отсутствовать — например, у сундука.</summary>
        public Health Health
        {
            get
            {
                if (health == null) health = GetComponent<Health>();
                return health;
            }
        }

        public bool IsAlive => Health == null || Health.IsAlive;

        /// <summary>Враждебна ли цель указанной стороне.</summary>
        public bool IsHostileTo(Faction other)
        {
            if (faction == Faction.Neutral || other == Faction.Neutral) return false;
            return faction != other;
        }

        public void Setup(string name, Faction newFaction)
        {
            displayName = name;
            faction = newFaction;
        }

        /// <summary>
        /// Поднять полоску под рост существа.
        ///
        /// Высота фиксированным числом работает, пока все одного размера.
        /// У крупного противника такая полоска висит на уровне груди, у
        /// мелкого — заметно выше макушки, и оба случая читаются как брак
        /// вёрстки, а не как разный рост.
        /// </summary>
        public void SetOverheadHeight(float value) => overheadHeight = value;

        [Tooltip("Портрет для панели. Пусто — рисуется однотонная плашка.")]
        [SerializeField] private Sprite portrait;

        /// <summary>Портрет существа. Ставится сборщиком сцены.</summary>
        public Sprite Portrait => portrait;

        public void SetPortrait(Sprite value) => portrait = value;

        /// <summary>
        /// Имя файла портрета, если оно не выводится из имени существа.
        ///
        /// Нужно там, где под одним именем ходят разные с виду твари: все
        /// волки зовутся «Волк», а масти у них белая, серая, бурая, чёрная —
        /// и таблица портретов по имени выдаёт всем одно лицо.
        ///
        /// Ключ, а не готовый спрайт. Спрайт я сначала и клал сюда, поменяв
        /// порядок выбора на «своё главнее таблицы», — и тем же ходом отнял
        /// портрет у НПС: у него в этом поле лежит пустая картинка, снятая
        /// когда-то с модели, и она перебила таблицу. Ключ такой беды не
        /// делает: пустая строка ничего не перекрывает.
        /// </summary>
        [Tooltip("Имя файла портрета, если оно не выводится из имени. Для мастей одного зверя.")]
        [SerializeField] private string portraitKey;

        public string PortraitKey => portraitKey;

        public void SetPortraitKey(string value) => portraitKey = value;

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, bodyRadius);
        }
#endif
    }
}
