using System.Collections.Generic;
using UnityEngine;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Силуэт персонажа, когда его закрывает препятствие.
    ///
    /// В изометрии камера привязана к углу, и обойти дерево, чтобы увидеть
    /// себя, игрок не может. Скрытый за кроной персонаж — это потерянное
    /// управление: не видно ни где ты, ни что с тобой происходит.
    ///
    /// Материал силуэта включается только тогда, когда между камерой и
    /// персонажем действительно что-то есть. Держать его постоянно нельзя:
    /// проверка глубины не отличает «за стеной» от «за собственным плечом»,
    /// и на открытом персонаже проступают куски его же модели — рука
    /// поверх тела, макушка поверх капюшона.
    /// </summary>
    public sealed class SilhouetteVisual : MonoBehaviour
    {
        [Tooltip("Материал силуэта. Пусто — компонент ничего не делает.")]
        [SerializeField] private Material silhouette;

        [Tooltip("Как часто проверять, закрыт ли персонаж. Каждый кадр не нужно.")]
        [SerializeField] private float checkInterval = 0.1f;

        [Tooltip("Высота точки, по которой проверяем перекрытие. Примерно грудь.")]
        [SerializeField] private float checkHeight = 1.1f;

        private readonly List<Renderer> targets = new List<Renderer>();
        private readonly Dictionary<Renderer, Material[]> plain = new Dictionary<Renderer, Material[]>();

        private Camera view;
        private float nextCheck;
        private bool shown;

        public void Setup(Material material) => silhouette = material;

        /// <summary>
        /// Высота объекта для проверки перекрытия. У персонажа это рост, у
        /// лежащего на земле мешка — полметра: проверять его на уровне головы
        /// человека значит мерить не то место.
        /// </summary>
        public void SetHeight(float value) => checkHeight = value;

        private void Start()
        {
            if (silhouette == null) return;

            // Здоровье — чтобы не подсвечивать труп. Может отсутствовать:
            // силуэт носят и мешки с добычей, у которых здоровья нет.
            health = GetComponent<Health>();

            view = Camera.main;

            // Собираем рендереры один раз: модель после сборки не меняется,
            // а перебирать иерархию по десять раз в секунду незачем.
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                // Интерфейсные рендереры пропускаем: полоска здоровья над
                // головой не должна светиться сквозь стену отдельным пятном.
                if (renderer.GetComponentInParent<Canvas>() != null) continue;

                targets.Add(renderer);
                plain[renderer] = renderer.sharedMaterials;
            }
        }

        private Health health;

        private void Update()
        {
            if (silhouette == null || targets.Count == 0) return;
            if (Time.time < nextCheck) return;

            nextCheck = Time.time + checkInterval;

            if (view == null) view = Camera.main;
            if (view == null) return;

            // Мёртвого не подсвечиваем.
            //
            // Силуэт означает «здесь опасность, которую ты не видишь».
            // Скелет, убитый за стеной, продолжал светиться красным до
            // самого возрождения — то есть полторы минуты сообщал об угрозе,
            // которой нет, и заодно прятал настоящую.
            bool alive = health == null || health.IsAlive;
            bool blocked = alive && IsBlocked();

            if (blocked != shown)
            {
                shown = blocked;
                Apply(shown);
            }
        }

        /// <summary>
        /// Есть ли препятствие между камерой и персонажем.
        ///
        /// Живые тела за препятствия не считаем: заслонённый союзником
        /// персонаж не должен вспыхивать силуэтом — он и так виден, просто
        /// частично.
        /// </summary>
        private bool IsBlocked()
        {
            // Две точки, а не одна: низ и верх объекта.
            //
            // Одной точки мало, и ошибается она в обе стороны. Точка у земли
            // цепляется за каждый бугор, и силуэт вспыхивает на ровном месте.
            // Точка на высоте роста проходит поверх низкой стены — и лежащий
            // за ней мешок остаётся невидимым, хотя закрыт целиком.
            //
            // Закрытым считаем, когда закрыты ОБЕ: значит объект не виден
            // ни целиком, ни частью.
            return Blocked(transform.position + Vector3.up * (checkHeight * 0.25f))
                && Blocked(transform.position + Vector3.up * checkHeight);
        }

        private bool Blocked(Vector3 point)
        {
            Vector3 fromCamera = point - view.transform.position;
            float distance = fromCamera.magnitude;

            if (distance < 0.01f) return false;

            var hits = Physics.RaycastAll(view.transform.position, fromCamera.normalized,
                                          distance, ~0, QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.collider.GetComponentInParent<Targetable>() != null) continue;

                return true;
            }

            return false;
        }

        private void Apply(bool withSilhouette)
        {
            foreach (var renderer in targets)
            {
                if (renderer == null) continue;
                if (!plain.TryGetValue(renderer, out var original)) continue;

                if (!withSilhouette)
                {
                    renderer.sharedMaterials = original;
                    continue;
                }

                var materials = new List<Material>(original) { silhouette };
                renderer.sharedMaterials = materials.ToArray();
            }

            // Мы только что переписали материалы персонажа — а вместе с ними
            // стёрли чужую прозрачность. Просим скрытность вернуть свою.
            //
            // Два компонента, правящих одни и те же материалы, обречены друг
            // друга затирать; вопрос только в том, кто из них последний. Здесь
            // порядок задан явно: силуэт решает, ЧТО рисовать, скрытность —
            // насколько прозрачно.
            var stealth = GetComponent<StealthState>();
            if (stealth != null) stealth.RefreshVisual();
        }
    }
}
