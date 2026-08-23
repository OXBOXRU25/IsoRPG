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

        private void Start()
        {
            if (silhouette == null) return;

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

        private void Update()
        {
            if (silhouette == null || targets.Count == 0) return;
            if (Time.time < nextCheck) return;

            nextCheck = Time.time + checkInterval;

            if (view == null) view = Camera.main;
            if (view == null) return;

            bool blocked = IsBlocked();

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
            Vector3 point = transform.position + Vector3.up * checkHeight;
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
        }
    }
}
