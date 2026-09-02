using UnityEngine;

namespace IsoRPG.World
{
    /// <summary>
    /// Гасит качание растительности, когда камера отъезжает.
    ///
    /// Павлон 02.09.2026: «тяжело смотреть на это поле издалека, в глазах
    /// рябит». Так и есть: вблизи качание читается как ветер, а с высоты
    /// тысячи мелких кустов колышутся вразнобой, каждый в несколько пикселей,
    /// и поле начинает мельтешить.
    ///
    /// Правим ОДИН глобальный параметр шейдера, а не материалы: у нас 143
    /// вида растений, и трогать каждый значит получить 143 места, где можно
    /// забыть. Шейдер Synty читает силу ветра из `_TotalWindAmount`.
    ///
    /// Висит на камере: сила ветра зависит от того, как далеко она стоит.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CalmDistantWind : MonoBehaviour
    {
        private static readonly int WindAmount = Shader.PropertyToID("_TotalWindAmount");

        [Tooltip("Приближение, при котором ветер в полную силу (размер ортокамеры).")]
        [SerializeField] private float closeSize = 9f;

        [Tooltip("Отдаление, при котором ветер стихает совсем.")]
        [SerializeField] private float farSize = 16f;

        [Tooltip("Сила ветра вблизи. Родное значение шейдера — 0.5.")]
        [SerializeField] private float nearWind = 0.5f;

        [Tooltip("Сила ветра при полном отдалении.")]
        [SerializeField] private float farWind = 0.08f;

        private Camera eye;
        private float applied = -1f;

        private void Awake() => eye = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (eye == null) return;

            float size = eye.orthographic ? eye.orthographicSize : (transform.position.y * 0.5f);
            float t = Mathf.InverseLerp(closeSize, farSize, size);
            float wind = Mathf.Lerp(nearWind, farWind, t);

            // Пишем только при заметном изменении: установка глобального
            // свойства шейдера каждый кадр — расход на пустом месте, а это
            // ММО, где всё повторяющееся умножается на число игроков.
            if (Mathf.Abs(wind - applied) < 0.01f) return;

            applied = wind;
            Shader.SetGlobalFloat(WindAmount, wind);
        }
    }
}
