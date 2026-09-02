using System;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.Combat
{
    /// <summary>
    /// Оглушение: цель не двигается и не бьёт, пока действует.
    ///
    /// Отдельным компонентом, а не флагом в бою, потому что состояний будет
    /// много — замедление, страх, обездвиживание. Каждое отключает свой набор
    /// возможностей, и разбираться в этом должен один слой, а не каждый
    /// компонент по отдельности.
    /// </summary>
    public sealed class StunReceiver : MonoBehaviour
    {
        [Tooltip("Насколько поднять метку над головой, чтобы значок оглушения не налезал на полоску здоровья.")]
        [SerializeField] private float markerLift = 0.45f;

        private static readonly int StunnedHash = Animator.StringToHash("Stunned");

        private MeleeCombatant combat;
        private MonsterBrain brain;
        private NavMeshAgent agent;
        private Targetable self;
        private GameObject marker;

        /// <summary>Аниматор цели — у кого набор принёс позу оглушения.</summary>
        private Animator animator;
        private bool hasStunnedFlag;

        private float stunUntil;
        private bool applied;

        public bool IsStunned => Time.time < stunUntil;
        public float Remaining => Mathf.Max(0f, stunUntil - Time.time);

        /// <summary>Оглушение началось или закончилось.</summary>
        public event Action<bool> StunChanged;

        private void Awake()
        {
            combat = GetComponent<MeleeCombatant>();
            brain = GetComponent<MonsterBrain>();
            agent = GetComponent<NavMeshAgent>();
            self = GetComponent<Targetable>();

            // Поза оглушения. У босса-кабана она в наборе есть, состояние в
            // контроллере стояло с 02.09.2026 — и не играло ни разу: флаг
            // никто не поднимал, оглушённый босс просто замирал в шаге.
            // Проверяем один раз: перебирать параметры в бою нельзя.
            animator = GetComponentInChildren<Animator>(true);

            if (animator != null && animator.runtimeAnimatorController != null)
                foreach (var p in animator.parameters)
                    if (p.nameHash == StunnedHash && p.type == AnimatorControllerParameterType.Bool)
                    {
                        hasStunnedFlag = true;
                        break;
                    }
        }

        /// <summary>Оглушить на указанное время. Повторное оглушение продлевает, а не складывается.</summary>
        public void Apply(float duration)
        {
            if (duration <= 0f) return;

            stunUntil = Mathf.Max(stunUntil, Time.time + duration);

            if (!applied) BeginStun();
        }

        private void Update()
        {
            if (applied && !IsStunned) EndStun();
        }

        private void BeginStun()
        {
            applied = true;

            // Отключаем то, что заставляет цель действовать. Здоровье и
            // возможность получать урон остаются — оглушённого как раз и бьют.
            if (combat != null) combat.enabled = false;
            if (brain != null) brain.enabled = false;

            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            if (hasStunnedFlag) animator.SetBool(StunnedHash, true);

            ShowMarker(true);
            StunChanged?.Invoke(true);
        }

        private void EndStun()
        {
            applied = false;

            if (combat != null) combat.enabled = true;
            if (brain != null) brain.enabled = true;

            if (agent != null && agent.isOnNavMesh) agent.isStopped = false;

            if (hasStunnedFlag) animator.SetBool(StunnedHash, false);

            ShowMarker(false);
            StunChanged?.Invoke(false);
        }

        /// <summary>
        /// Простой значок над головой — жёлтый кубик. Игрок должен видеть,
        /// что цель обездвижена, иначе оглушение выглядит как «монстр завис».
        /// </summary>
        private void ShowMarker(bool visible)
        {
            if (visible && marker == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "StunMarker";
                Destroy(marker.GetComponent<Collider>());

                marker.transform.SetParent(transform);
                float height = self != null ? self.OverheadPoint.y - transform.position.y : 2.2f;
                marker.transform.localPosition = Vector3.up * (height + markerLift);
                marker.transform.localScale = Vector3.one * 0.22f;
                marker.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);

                var renderer = marker.GetComponent<Renderer>();
                renderer.sharedMaterial = new Material(renderer.sharedMaterial)
                {
                    color = new Color32(0xF0, 0xD0, 0x50, 0xFF)
                };
            }

            if (marker != null) marker.SetActive(visible);
        }

        private void OnDisable()
        {
            // Компонент выключили посреди оглушения — возвращаем всё как было,
            // иначе цель останется парализованной навсегда.
            if (applied) EndStun();
        }
    }
}
