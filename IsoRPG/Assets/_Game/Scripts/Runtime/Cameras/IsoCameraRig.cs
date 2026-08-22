using UnityEngine;
using UnityEngine.InputSystem;

namespace IsoRPG.Cameras
{
    /// <summary>
    /// Изометрическая камера: ортографическая проекция под фиксированным углом,
    /// плавно следует за целью, зум колесом мыши.
    ///
    /// Все углы и расстояния вынесены в инспектор и работают в режиме
    /// редактирования — можно крутить ползунки и сразу видеть картинку,
    /// не запуская игру.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class IsoCameraRig : MonoBehaviour
    {
        [Header("Цель слежения")]
        [Tooltip("За кем ходит камера. Обычно игрок.")]
        [SerializeField] private Transform target;

        [Tooltip("Смещение точки, на которую смотрит камера. Поднимаем на половину роста персонажа, иначе он стоит в самом низу кадра.")]
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1f, 0f);

        [Header("Угол обзора")]
        [Tooltip("Наклон к земле. 30 — низкий, вытянутый вид. 60 — почти сверху. Наши 35 сняты замером с Albion (там 32–36).")]
        [Range(15f, 85f)]
        [SerializeField] private float pitch = 35f;

        [Tooltip("Поворот вокруг вертикали. 45 — каноническая изометрия. Наши 50 сняты замером с Albion (там ~51).")]
        [Range(0f, 360f)]
        [SerializeField] private float yaw = 50f;

        [Header("Дистанция и зум")]
        [Tooltip("Как далеко камера отодвинута. На ортографической проекции не влияет на масштаб — только на то, что попадает в зону отрисовки.")]
        [SerializeField] private float distance = 30f;

        [Tooltip("Половина высоты кадра в мировых единицах. Это и есть настоящий зум ортографической камеры.")]
        [SerializeField] private float orthoSize = 9f;

        [SerializeField] private float minOrthoSize = 5f;
        [SerializeField] private float maxOrthoSize = 18f;

        [Tooltip("На сколько меняется зум за один щелчок колеса.")]
        [SerializeField] private float zoomStep = 1f;

        [Tooltip("Скорость догона зума. Больше — резче.")]
        [SerializeField] private float zoomSmooth = 12f;

        [Header("Следование")]
        [Tooltip("Скорость догона позиции. Больше — жёстче привязка к цели.")]
        [SerializeField] private float followSmooth = 10f;

        [Tooltip("Мгновенно ставить камеру на место при старте, без наезда из точки ноль.")]
        [SerializeField] private bool snapOnStart = true;

        private Camera cam;
        private float desiredOrthoSize;
        private Vector3 smoothedFocus;
        private bool focusInitialised;

        /// <summary>Назначить цель из кода — нужно сборщику сцены и при смене персонажа.</summary>
        public void SetTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            if (snap) focusInitialised = false;
        }

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
            desiredOrthoSize = orthoSize;
            ApplyProjection();
        }

        private void Start()
        {
            if (snapOnStart) focusInitialised = false;
        }

        private void LateUpdate()
        {
            if (Application.isPlaying) ReadZoomInput();

            ApplyProjection();
            UpdatePlacement(Application.isPlaying ? Time.deltaTime : 0f);
        }

        private void ReadZoomInput()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f)) return;

            // Колесо отдаёт 120 за щелчок на большинстве мышей, но не на всех —
            // поэтому берём только знак, иначе зум прыгает на разном железе.
            desiredOrthoSize = Mathf.Clamp(
                desiredOrthoSize - Mathf.Sign(scroll) * zoomStep,
                minOrthoSize,
                maxOrthoSize);
        }

        private void ApplyProjection()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;

            cam.orthographic = true;

            if (Application.isPlaying)
            {
                orthoSize = Mathf.Lerp(orthoSize, desiredOrthoSize, 1f - Mathf.Exp(-zoomSmooth * Time.deltaTime));
            }
            else
            {
                // В редакторе ползунок работает напрямую, без сглаживания.
                desiredOrthoSize = orthoSize;
            }

            cam.orthographicSize = orthoSize;

            // Ортографической камере нужны честные плоскости отсечения, иначе
            // при большом наклоне дальний край земли просто исчезает.
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = distance * 3f;
        }

        private void UpdatePlacement(float deltaTime)
        {
            Vector3 focus = target != null
                ? target.position + lookOffset
                : transform.position + transform.forward * distance;

            if (!focusInitialised || deltaTime <= 0f)
            {
                smoothedFocus = focus;
                focusInitialised = true;
            }
            else
            {
                // Экспоненциальное сглаживание: не зависит от частоты кадров,
                // в отличие от наивного Lerp со скоростью на кадр.
                smoothedFocus = Vector3.Lerp(smoothedFocus, focus, 1f - Mathf.Exp(-followSmooth * deltaTime));
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(
                smoothedFocus - rotation * Vector3.forward * distance,
                rotation);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (minOrthoSize > maxOrthoSize) minOrthoSize = maxOrthoSize;
            orthoSize = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);
            desiredOrthoSize = orthoSize;
            distance = Mathf.Max(1f, distance);
        }
#endif
    }
}
