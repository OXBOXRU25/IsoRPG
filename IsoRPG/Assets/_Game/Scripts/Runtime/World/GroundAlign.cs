using UnityEngine;

namespace IsoRPG.World
{
    /// <summary>
    /// Кладёт фигуру на склон по наклону земли.
    ///
    /// Навигационный агент держит подопечного строго вертикально: он знает
    /// только точку на сетке и направление движения. Человеку это почти не
    /// заметно — он и правда стоит прямо, — а четвероногий на склоне
    /// повисает: передние лапы в воздухе при спуске, задние при подъёме.
    ///
    /// Лечится не высотой, а наклоном. Луч вниз даёт нормаль поверхности,
    /// и мы доворачиваем модель так, чтобы её «вверх» смотрел вдоль этой
    /// нормали, сохраняя направление взгляда.
    ///
    /// <b>Вешать на модель, а не на корень с агентом.</b> Агент каждый кадр
    /// возвращает своему объекту вертикаль, и наклон с ним будет драться:
    /// фигура задёргается. Поэтому корень остаётся вертикальным и ходит, а
    /// наклоняется дочерний узел с мешем.
    /// </summary>
    public sealed class GroundAlign : MonoBehaviour
    {
        [Tooltip("Откуда пускать луч вниз, метров над точкой отсчёта.")]
        [SerializeField] private float rayStart = 1.5f;

        [Tooltip("Насколько далеко искать землю, метров.")]
        [SerializeField] private float rayLength = 4f;

        [Tooltip("Предел наклона, градусы. Дальше фигура выглядит падающей.")]
        [SerializeField] private float maxTilt = 35f;

        [Tooltip("Скорость доворота. Больше — резче, меньше — плавнее.")]
        [SerializeField] private float smooth = 8f;

        [Tooltip("По каким слоям искать землю.")]
        [SerializeField] private LayerMask ground = ~0;

        private Quaternion offset = Quaternion.identity;
        private float drop;
        private Transform root;

        private void Awake()
        {
            // Корень всей фигуры: по нему отличаем свои коллайдеры от земли.
            root = transform.parent != null ? transform.parent : transform;
        }

        private void LateUpdate()
        {
            // Именно LateUpdate: агент и аниматор уже отработали, и наш
            // доворот ложится поверх, а не стирается ими в том же кадре.
            Vector3 from = transform.position + Vector3.up * rayStart;

            // Луч сначала встречает СВОЙ коллайдер: капсула зверя стоит
            // ровно здесь, и её бок прекрасно сходит за «землю» — фигура
            // после такого замера ложится в произвольную сторону. Поэтому
            // перебираем попадания и берём первое чужое.
            var hits = Physics.RaycastAll(from, Vector3.down, rayLength, ground,
                                          QueryTriggerInteraction.Ignore);

            bool found = false;
            RaycastHit hit = default;
            float nearest = float.MaxValue;

            foreach (var candidate in hits)
            {
                if (candidate.collider.transform.IsChildOf(root)) continue;
                if (candidate.distance >= nearest) continue;

                nearest = candidate.distance;
                hit = candidate;
                found = true;
            }

            if (!found)
            {
                // Земли под ногами нет — возвращаемся к вертикали, иначе
                // фигура застынет накренённой в прыжке или над обрывом.
                offset = Quaternion.Slerp(offset, Quaternion.identity,
                                          1f - Mathf.Exp(-smooth * Time.deltaTime));
                transform.localRotation = offset;
                return;
            }

            var normal = hit.normal;

            // Ограничиваем крен: на отвесной скале зверь иначе ложится
            // плашмя и выглядит приклеенным к стене.
            float angle = Vector3.Angle(Vector3.up, normal);

            if (angle > maxTilt)
                normal = Vector3.Slerp(Vector3.up, normal, maxTilt / angle);

            // Поворот считаем в системе родителя: сам родитель уже смотрит
            // куда надо, наше дело — только наклон.
            var wanted = Quaternion.Inverse(transform.parent.rotation) *
                         Quaternion.FromToRotation(Vector3.up, normal) *
                         transform.parent.rotation;

            offset = Quaternion.Slerp(offset, wanted, 1f - Mathf.Exp(-smooth * Time.deltaTime));
            transform.localRotation = offset;

            // Заодно прижимаем к земле — каждый кадр, а не один раз при
            // расстановке.
            //
            // Статическая посадка держится ровно до первого шага: зверь
            // уходит с места, земля под ним меняется, а смещение остаётся
            // от старой точки — на бугре висит, в низине тонет наполовину.
            // Корень при этом стоит на навигационной сетке, которая сама по
            // себе может лежать выше грунта, поэтому равняемся на землю.
            float wantedDrop = hit.point.y - transform.parent.position.y;

            drop = Mathf.Lerp(drop, wantedDrop, 1f - Mathf.Exp(-smooth * Time.deltaTime));

            var local = transform.localPosition;
            transform.localPosition = new Vector3(local.x, drop, local.z);
        }
    }
}
