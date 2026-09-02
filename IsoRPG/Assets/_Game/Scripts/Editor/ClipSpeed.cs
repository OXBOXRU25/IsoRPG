using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Своя скорость клипа хода: сколько метров в секунду проходит персонаж,
    /// если дать анимации играть как есть.
    ///
    /// Нужна для порогов дерева движения. Порог обязан равняться этой
    /// скорости: поставишь мимо — ноги перебирают быстрее или медленнее, чем
    /// персонаж едет по земле, и это читается как скольжение. Замер
    /// 02.09.2026 по кабану Malbers: шаг 0.87, рысь 1.94, бег 6.34 м/с, а в
    /// контроллере стояло «бег с 3.0» при скорости агента 3.0 — зверь всё
    /// время играл полный бег.
    ///
    /// Одним местом на всех, а не по копии в каждом сборщике: одна и та же
    /// мера, посчитанная в двух местах, однажды разойдётся.
    /// </summary>
    public static class ClipSpeed
    {
        /// <summary>
        /// Скорость клипа, метры в секунду. Ноль — померить не вышло.
        ///
        /// Считаем нетто-смещение корня за клип и делим на длину. Корень
        /// ищем по НАИБОЛЬШЕМУ нетто-смещению, а не по наименьшей глубине
        /// пути: у части наборов верхний узел только качается, а вперёд везёт
        /// узел под ним — так вышло у волка Meshtint, и по глубине замер дал
        /// ноль. Нетто, а не сумма: у циклического шага стопа возвращается в
        /// исходную точку, и корень отличается от конечностей сам собой.
        ///
        /// Если кривых трансформа нет вовсе — движение запечено кривыми
        /// Mecanim, и его отдаёт `clip.averageSpeed`.
        /// </summary>
        public static float Measure(AnimationClip clip)
        {
            if (clip == null || clip.length < 0.01f) return 0f;

            var bindings = AnimationUtility.GetCurveBindings(clip);
            var shifts = new System.Collections.Generic.Dictionary<string, Vector3>();

            foreach (var b in bindings)
            {
                if (!b.propertyName.StartsWith("m_LocalPosition")) continue;

                var curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null || curve.length < 2) continue;

                float delta = curve.Evaluate(clip.length) - curve.Evaluate(0f);

                shifts.TryGetValue(b.path, out var shift);

                switch (b.propertyName[b.propertyName.Length - 1])
                {
                    case 'x': shift.x = delta; break;
                    case 'y': shift.y = delta; break;
                    case 'z': shift.z = delta; break;
                }

                shifts[b.path] = shift;
            }

            float best = 0f;

            foreach (var shift in shifts.Values)
                best = Mathf.Max(best, shift.magnitude);

            float speed = best / clip.length;

            if (speed > 0.05f) return speed;

            float baked = clip.averageSpeed.magnitude;

            return baked > 0.05f ? baked : 0f;
        }
    }
}
