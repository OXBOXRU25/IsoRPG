using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace IsoRPG.Player
{
    /// <summary>
    /// Движение персонажа кликом по земле, как в изометрических РПГ.
    /// Клик — идти в точку, удержание — идти следом за курсором.
    ///
    /// Путь считает NavMeshAgent, поэтому персонаж сам обходит препятствия
    /// и не проходит сквозь них.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ClickToMoveController : MonoBehaviour
    {
        [Header("Куда можно ходить")]
        [Tooltip("Слои, по которым засчитывается клик. Земля и всё проходимое.")]
        [SerializeField] private LayerMask walkableMask = ~0;

        [Tooltip("Длина луча из камеры. Должна перекрывать всю локацию по диагонали.")]
        [SerializeField] private float rayDistance = 500f;

        [Header("Поведение")]
        [Tooltip("Пока кнопка зажата, цель движения обновляется каждый кадр — персонаж идёт за курсором.")]
        [SerializeField] private bool followWhileHeld = true;

        [Tooltip("Как часто обновлять цель при удержании, в секундах. Каждый кадр не нужно — путь считается заново и это дороже.")]
        [SerializeField] private float holdRepathInterval = 0.1f;

        [Tooltip("Насколько далеко от клика разрешено искать ближайшую точку навигации. Спасает клики по краю дороги и по склону.")]
        [SerializeField] private float navSampleRadius = 2f;

        [Header("Отметка места назначения")]
        [Tooltip("Необязательно. Объект, который вспыхивает в точке клика.")]
        [SerializeField] private GameObject destinationMarker;

        [SerializeField] private float markerLifetime = 0.6f;

        private NavMeshAgent agent;
        private Camera cam;
        private float nextRepathTime;
        private float markerHideTime;

        /// <summary>Текущая точка назначения. Пригодится боевой системе — подойти к цели и остановиться.</summary>
        public Vector3 Destination => agent != null ? agent.destination : transform.position;

        /// <summary>Идёт ли персонаж прямо сейчас. Нужно анимации и правилу «нельзя бить на бегу».</summary>
        public bool IsMoving => agent != null
                                && !agent.pathPending
                                && agent.remainingDistance > agent.stoppingDistance + 0.05f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            cam = Camera.main;

            if (destinationMarker != null) destinationMarker.SetActive(false);
        }

        private void OnEnable()
        {
            // Камера могла появиться позже персонажа — например, при сборке
            // сцены из скрипта. Поэтому ищем повторно, а не только в Awake.
            if (cam == null) cam = Camera.main;
        }

        private void Update()
        {
            HandleInput();
            HandleMarker();
        }

        private void HandleInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            bool pressed = mouse.leftButton.wasPressedThisFrame;
            bool held = followWhileHeld
                        && mouse.leftButton.isPressed
                        && Time.time >= nextRepathTime;

            if (!pressed && !held) return;

            if (!TryGetGroundPoint(mouse.position.ReadValue(), out Vector3 point)) return;

            MoveTo(point, showMarker: pressed);
            nextRepathTime = Time.time + holdRepathInterval;
        }

        /// <summary>Отправить персонажа в точку. Публичный — им же пользуется боевая система.</summary>
        public void MoveTo(Vector3 worldPoint, bool showMarker = false)
        {
            if (agent == null || !agent.isOnNavMesh) return;

            // Клик мог прийтись на склон, край дороги или тонкий выступ, где
            // навигационной сетки нет. Ищем ближайшую законную точку рядом,
            // иначе персонаж просто откажется идти и это выглядит как баг.
            if (NavMesh.SamplePosition(worldPoint, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);

                if (showMarker) ShowMarker(hit.position);
            }
        }

        private bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 point)
        {
            point = default;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(screenPosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, walkableMask, QueryTriggerInteraction.Ignore))
                return false;

            point = hit.point;
            return true;
        }

        private void ShowMarker(Vector3 position)
        {
            if (destinationMarker == null) return;

            // Чуть поднимаем над поверхностью: точно на уровне земли отметка
            // дерётся с ней за пиксели и мерцает.
            destinationMarker.transform.position = position + Vector3.up * 0.02f;
            destinationMarker.SetActive(true);
            markerHideTime = Time.time + markerLifetime;
        }

        private void HandleMarker()
        {
            if (destinationMarker == null || !destinationMarker.activeSelf) return;
            if (Time.time >= markerHideTime) destinationMarker.SetActive(false);
        }
    }
}
