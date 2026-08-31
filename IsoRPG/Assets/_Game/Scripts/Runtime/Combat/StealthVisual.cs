using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Как выглядит скрытность: персонаж становится полупрозрачным.
    ///
    /// Почему именно так. Скрытность — состояние, которое игрок не видит
    /// никак: походка меняется, но это заметно только если приглядываться, а
    /// «работает или нет» надо понимать мгновенно. Полупрозрачность — приём
    /// из World of Warcraft, и он читается без обучения: сквозь героя видно
    /// траву, значит он в тени.
    ///
    /// Прозрачность делается КОПИЯМИ материалов, а не правкой исходных.
    /// Материал в Unity — общий ассет: правка на герое ушла бы во всё, что
    /// им покрашено, и осталась бы там навсегда, включая файлы проекта.
    /// Копии живут в памяти объекта и умирают вместе с ним.
    ///
    /// Прозрачность 0.45, а не сильнее: при 0.2 герой теряется на пёстром
    /// лугу настолько, что игрок перестаёт понимать, где он стоит.
    /// </summary>
    public sealed class StealthVisual : MonoBehaviour
    {
        [Tooltip("Насколько видим в тени. 1 — как обычно, 0 — совсем прозрачный.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float alpha = 0.45f;

        private StealthState stealth;

        private readonly List<Renderer> parts = new List<Renderer>();
        private readonly Dictionary<Renderer, Material[]> solid = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<Renderer, Material[]> ghost = new Dictionary<Renderer, Material[]>();

        private void Awake()
        {
            stealth = GetComponent<StealthState>();
            Collect();
        }

        private void OnEnable()
        {
            if (stealth != null)
            {
                stealth.StealthChanged += Show;
                Show(stealth.IsStealthed);
            }
        }

        private void OnDisable()
        {
            if (stealth != null) stealth.StealthChanged -= Show;
        }

        private void OnDestroy()
        {
            // Копии материалов создаём мы — нам их и убирать. Иначе каждая
            // смерть с последующим возрождением оставляла бы в памяти по
            // комплекту материалов на героя.
            foreach (var set in ghost.Values)
                foreach (var material in set)
                    if (material != null) Destroy(material);
        }

        // ------------------------------------------------------------------

        private void Collect()
        {
            parts.Clear();
            solid.Clear();

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                // Кольцо цели, полоски и прочие служебные плоскости прятать
                // не надо: они и так рисуются поверх мира.
                if (renderer is ParticleSystemRenderer) continue;

                parts.Add(renderer);
                solid[renderer] = renderer.sharedMaterials;
            }
        }

        private void Show(bool stealthed)
        {
            // Модель могла смениться (переодели оружие, пересобрали героя).
            if (parts.Count == 0) Collect();

            foreach (var renderer in parts)
            {
                if (renderer == null) continue;

                if (!stealthed)
                {
                    if (solid.TryGetValue(renderer, out var original))
                        renderer.sharedMaterials = original;

                    continue;
                }

                if (!ghost.TryGetValue(renderer, out var transparent))
                {
                    transparent = MakeGhost(solid[renderer]);
                    ghost[renderer] = transparent;
                }

                renderer.sharedMaterials = transparent;
            }
        }

        /// <summary>
        /// Прозрачная копия набора материалов.
        ///
        /// Прозрачность в Universal включается НАБОРОМ свойств и ключевым
        /// словом сразу: хоть одно несовпадение — и альфа игнорируется молча,
        /// материал остаётся плотным. Тот же урок, что с кольцом цели.
        /// </summary>
        private Material[] MakeGhost(Material[] source)
        {
            var result = new Material[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null) continue;

                var copy = new Material(source[i]);

                copy.SetFloat("_Surface", 1f);            // Transparent
                copy.SetFloat("_Blend", 0f);              // Alpha
                copy.SetFloat("_ZWrite", 0f);
                copy.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                copy.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                copy.SetFloat("_AlphaClip", 0f);

                copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                copy.DisableKeyword("_ALPHATEST_ON");
                copy.SetOverrideTag("RenderType", "Transparent");
                copy.renderQueue = (int)RenderQueue.Transparent;

                if (copy.HasProperty("_BaseColor"))
                {
                    var colour = copy.GetColor("_BaseColor");
                    colour.a = alpha;
                    copy.SetColor("_BaseColor", colour);
                }

                if (copy.HasProperty("_Color"))
                {
                    var colour = copy.GetColor("_Color");
                    colour.a = alpha;
                    copy.SetColor("_Color", colour);
                }

                result[i] = copy;
            }

            return result;
        }
    }
}
