using System;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Скрытность разбойника: монстры не замечают, скорость ниже.
    ///
    /// Вход мгновенный, но с откатом. Главное ограничение — нельзя уйти в
    /// тень, пока на тебе висит агрессия: иначе бой превращается в бесконечное
    /// «ударил и спрятался», и монстр никогда не догоняет.
    /// </summary>
    public sealed class StealthState : MonoBehaviour
    {
        [Tooltip("Откат входа в скрытность, секунд.")]
        [SerializeField] private float cooldown = 10f;

        [Tooltip("Во сколько раз медленнее движется скрытый персонаж.")]
        [Range(0.3f, 1f)]
        [SerializeField] private float moveSpeedFactor = 0.7f;

        [Tooltip("Насколько прозрачным становится персонаж. Игрок должен видеть себя, но понимать, что он в тени.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float visualAlpha = 0.45f;

        [Tooltip("Сколько секунд после последнего полученного урона считается, что на нас агрессия.")]
        [SerializeField] private float combatMemory = 5f;

        private NavMeshAgent agent;
        private Health health;

        private float normalSpeed;
        private float readyTime;
        private float lastDamageTime = -999f;
        private bool stealthed;

        public bool IsStealthed => stealthed;
        public bool IsInCombat => Time.time - lastDamageTime < combatMemory;
        public float CooldownLeft => Mathf.Max(0f, readyTime - Time.time);
        public bool CanEnter => !stealthed && CooldownLeft <= 0f && !IsInCombat;

        /// <summary>Скрытность включилась или выключилась.</summary>
        public event Action<bool> StealthChanged;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();

            if (agent != null) normalSpeed = agent.speed;
        }

        private void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(int amount, GameObject source)
        {
            lastDamageTime = Time.time;

            // Получил по голове — вышел из тени. Иначе скрытность была бы
            // способом игнорировать бой вообще.
            if (stealthed) Exit();
        }

        /// <summary>Попытка войти в тень. Возвращает причину отказа или пустую строку.</summary>
        public string TryEnter()
        {
            if (stealthed) return "уже в тени";
            if (CooldownLeft > 0f) return "скрытность ещё не готова";
            if (IsInCombat) return "нельзя в бою";

            Enter();
            return string.Empty;
        }

        public void Toggle()
        {
            if (stealthed) Exit();
            else TryEnter();
        }

        private void Enter()
        {
            stealthed = true;
            readyTime = Time.time + cooldown;

            if (agent != null)
            {
                // Талант маскировки возвращает часть отнятой скорости, но не
                // больше обычной: скрытность, в которой бегут быстрее, чем в
                // открытую, ломает саму мысль о выборе.
                var book = GetComponent<IsoRPG.Progression.TalentBook>();

                float factor = moveSpeedFactor;
                if (book != null)
                    factor = Mathf.Min(1f, factor +
                        book.Bonus(IsoRPG.Progression.TalentEffect.StealthSpeed));

                agent.speed = normalSpeed * factor;
            }

            ApplyVisual(true);
            StealthChanged?.Invoke(true);
        }

        public void Exit()
        {
            if (!stealthed) return;

            stealthed = false;
            if (agent != null) agent.speed = normalSpeed;

            ApplyVisual(false);
            StealthChanged?.Invoke(false);
        }

        /// <summary>
        /// Полупрозрачность персонажа.
        ///
        /// Материалы URP по умолчанию непрозрачные, и просто выставить альфу
        /// нельзя — нужно переключить режим на прозрачный. Делаем это на
        /// копиях материалов, чтобы не испортить исходные ассеты.
        /// </summary>
        private void ApplyVisual(bool fade)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    if (!material.HasProperty("_BaseColor")) continue;

                    if (fade)
                    {
                        SetTransparent(material);
                        Color c = material.GetColor("_BaseColor");
                        c.a = visualAlpha;
                        material.SetColor("_BaseColor", c);
                    }
                    else
                    {
                        Color c = material.GetColor("_BaseColor");
                        c.a = 1f;
                        material.SetColor("_BaseColor", c);
                        SetOpaque(material);
                    }
                }
            }
        }

        private static void SetTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);          // 1 = Transparent в URP
            m.SetFloat("_Blend", 0f);            // Alpha
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void SetOpaque(Material m)
        {
            m.SetFloat("_Surface", 0f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            m.SetInt("_ZWrite", 1);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }
    }
}
