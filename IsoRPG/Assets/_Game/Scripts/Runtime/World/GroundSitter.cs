using UnityEngine;

namespace IsoRPG.World
{
    /// <summary>
    /// Посадить растительность на грунт один раз при старте игры.
    ///
    /// Зачем это в рантайме, а не при посеве. Габариты объекта, прочитанные
    /// в редакторе сразу после того, как ему задали поворот и масштаб,
    /// приходят от ПРЕЖНЕГО состояния. Посадка, посчитанная по ним, выходит
    /// неверной, и куст висит краем — при том, что формула правильная.
    /// В игре такой беды нет: к первому кадру всё пересчитано.
    ///
    /// Почему один раз, а не каждый кадр, как у зверей. Волков трое, а
    /// растений тысяча шестьсот. Ежекадровый луч на каждое растение — это
    /// заметная доля кадра ни за что: трава не ходит, посадка ей нужна
    /// однократная. Один проход при старте занимает миллисекунды.
    ///
    /// Землю ищем лучом, а не высотой террейна: под кустом может лежать
    /// камень или корень, и «высота грунта» там выше карты высот.
    /// </summary>
    public sealed class GroundSitter : MonoBehaviour
    {
        [Tooltip("Насколько выше макушки начинать луч, метров.")]
        [SerializeField] private float rayStart = 4f;

        [Tooltip("Длина луча, метров.")]
        [SerializeField] private float rayLength = 14f;

        [Tooltip("Утопить на долю собственной высоты: иначе виден плоский низ.")]
        [SerializeField] private float sinkShare = 0.15f;

        [Tooltip("По каким слоям искать землю.")]
        [SerializeField] private LayerMask ground = ~0;

        private void Start()
        {
            int moved = 0, missed = 0, oddPivot = 0, stillHigh = 0;
            float gapSum = 0f;
            float sum = 0f, worst = 0f;

            for (int i = 0; i < transform.childCount; i++)
            {
                var plant = transform.GetChild(i);

                var rs = plant.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;

                var box = rs[0].bounds;
                for (int r = 1; r < rs.Length; r++) box.Encapsulate(rs[r].bounds);

                // Луч пускаем от макушки, а не от точки отсчёта: у покупных
                // префабов она бывает где угодно, в том числе ниже земли.
                var from = new Vector3(plant.position.x, box.max.y + rayStart, plant.position.z);

                var hits = Physics.RaycastAll(from, Vector3.down, rayLength, ground,
                                              QueryTriggerInteraction.Ignore);

                bool found = false;
                float nearest = float.MaxValue;
                RaycastHit hit = default;

                foreach (var candidate in hits)
                {
                    // Своё не считаем землёй.
                    if (candidate.collider.transform.IsChildOf(plant)) continue;
                    if (candidate.distance >= nearest) continue;

                    nearest = candidate.distance;
                    hit = candidate;
                    found = true;
                }

                if (!found)
                {
                    missed++;
                    continue;
                }

                // Сажаем по ТОЧКЕ ОСНОВАНИЯ, а не по низу габаритной коробки.
                //
                // Это и была ошибка первого захода. Земля меряется лучом под
                // центром растения, а низ коробки на склоне лежит ниже по
                // склону — там земля своя, ниже. Приравняв одно к другому, мы
                // задираем куст ровно на перепад склона под ним: чем шире
                // куст и круче место, тем сильнее висит. То есть прежний баг
                // воспроизводится другой формулой.
                //
                // Точке отсчёта префаба доверяем не слепо: у покупных наборов
                // она бывает где угодно, и папоротник с точкой отсчёта в
                // кроне улетел бы в небо. Если она далеко от низа коробки —
                // возвращаемся к низу.
                float baseY = plant.position.y;

                if (Mathf.Abs(baseY - box.min.y) > box.size.y * 0.25f)
                {
                    baseY = box.min.y;
                    oddPivot++;
                }

                float sink = box.size.y * sinkShare;
                float shift = hit.point.y - baseY - sink;

                plant.position += new Vector3(0f, shift, 0f);

                // КОНТРОЛЬНЫЙ ЗАМЕР. Сообщение «посажено» печатает тот же
                // код, который сажал, и подтверждает лишь то, что он дошёл
                // до строки. Проверяем результат отдельным лучом: сколько
                // кустов после посадки всё ещё висит над землёй.
                var after = Physics.RaycastAll(
                    new Vector3(plant.position.x, plant.position.y + 0.5f, plant.position.z),
                    Vector3.down, 6f, ground, QueryTriggerInteraction.Ignore);

                float best = float.MaxValue;
                bool ok = false;

                foreach (var c in after)
                {
                    if (c.collider.transform.IsChildOf(plant)) continue;
                    if (c.distance >= best) continue;
                    best = c.distance; ok = true;
                }

                if (ok)
                {
                    // Зазор между точкой отсчёта растения и землёй под ней.
                    float gapNow = 0.5f - best;

                    if (gapNow > 0.1f) { stillHigh++; gapSum += gapNow; }
                }

                moved++;
                sum += shift;

                if (Mathf.Abs(shift) > Mathf.Abs(worst)) worst = shift;
            }

            // Отчёт числами, а не «готово»: по среднему и наибольшему сдвигу
            // сразу видно, была ли посадка вообще кривой и насколько.
            Debug.Log("[IsoRPG] Растительность посажена на грунт: сдвинуто " + moved +
                      ", без земли под собой " + missed +
                      ", точка отсчёта не в основании у " + oddPivot +
                      ", средний сдвиг " + (moved > 0 ? sum / moved : 0f).ToString("0.00") +
                      " м, наибольший " + worst.ToString("0.00") + " м. " +
                      "ПРОВЕРКА: утоплены глубже 10 см — " + stillHigh +
                      ", среднее заглубление точки отсчёта " +
                      (stillHigh > 0 ? gapSum / stillHigh : 0f).ToString("0.00") + " м.");
        }
    }
}
