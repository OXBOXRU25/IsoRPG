using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп невидимых стен: карта навигации вокруг точки.
    ///
    /// Жалоба «преград нет, а персонаж упирается» неразрешима на глаз:
    /// смотришь на пустую траву и видишь пустую траву. А герой ходит не по
    /// траве, а по навигационной сетке, и упирается он в её край — которого
    /// в кадре не видно вовсе.
    ///
    /// Поэтому печатаем сетку текстом: точка — можно стоять, решётка —
    /// нельзя. Стена сразу видна ФОРМОЙ. Круглое пятно — что-то одно
    /// круглое; полоса — стена или обрыв; рваные острова — навигация не
    /// достроилась.
    ///
    /// И отдельно: чем именно занята каждая дырка. Навигация строится по
    /// НАРИСОВАННЫМ мешам, а не по коллайдерам, поэтому снятый коллайдер
    /// дырку не лечит — это мы уже проходили с ветками деревьев.
    /// </summary>
    public static class WallProbe
    {
        /// <summary>Куда смотреть. Точка с миникарты, где упёрся герой.</summary>
        private static Vector3 Spot = new Vector3(-1f, 0f, 30f);

        /// <summary>Полуширина карты в метрах и шаг клетки.</summary>
        private const int Half = 16;
        private const float Step = 1f;

        [MenuItem("Tools/IsoRPG/Невидимая стена: карта навигации", priority = 69)]
        public static void Probe() => Probe(null);

        /// <summary>
        /// Та же карта, но в заданной точке: «wall -1 30» из очереди заданий.
        /// Без координат берётся точка по умолчанию.
        /// </summary>
        public static void Probe(string where)
        {
            if (!string.IsNullOrWhiteSpace(where))
            {
                var parts = where.Split(new[] { ' ', ',', ';' },
                                        System.StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 2 &&
                    float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float z))
                {
                    Spot = new Vector3(x, 0f, z);
                }
                else
                {
                    Debug.LogWarning("[IsoRPG] Не разобрал координаты «" + where +
                                     "» — щуп встал в точку по умолчанию.");
                }
            }

            var report = new StringBuilder();

            report.AppendLine("КАРТА НАВИГАЦИИ вокруг X " + Spot.x + "  Z " + Spot.z);
            report.AppendLine("'.' можно идти   '#' нельзя   '@' сама точка");
            report.AppendLine("Север (+Z) сверху, восток (+X) справа.");
            report.AppendLine();

            // Счётчик виновников: что чаще всего стоит в дырках.
            var blame = new Dictionary<string, int>();
            int holes = 0;

            for (int z = Half; z >= -Half; z--)
            {
                var line = new StringBuilder();

                for (int x = -Half; x <= Half; x++)
                {
                    Vector3 at = new Vector3(Spot.x + x * Step, Spot.y, Spot.z + z * Step);

                    // Пробу берём У ЗЕМЛИ, а не над ней.
                    //
                    // Здесь была тихая ложь щупа, и она едва не стоила
                    // перестройки целой сцены. Проба бралась в метре над
                    // точкой с допуском 0.75 — то есть до сетки, лежащей на
                    // земле, не дотягивалась НИКОГДА. Щуп рапортовал «сетки
                    // нет нигде» на совершенно исправной карте, и виноватой
                    // выглядела выпечка.
                    //
                    // Поэтому сначала ищем землю лучом сверху, а пробу берём
                    // ровно там, где она нашлась. Тогда допуск можно держать
                    // маленьким, и «сетка есть ЗДЕСЬ» остаётся честным.
                    //
                    // Урок общий: щуп, ужесточённый и не перепроверенный на
                    // заведомо целом случае, опаснее отсутствующего — ему
                    // верят.
                    Vector3 probeAt = at + Vector3.up * 0.2f;

                    if (Physics.Raycast(at + Vector3.up * 60f, Vector3.down,
                                        out var floor, 200f, ~0,
                                        QueryTriggerInteraction.Ignore))
                    {
                        probeAt = floor.point + Vector3.up * 0.2f;
                    }

                    bool walkable = NavMesh.SamplePosition(
                        probeAt, out _, 0.6f, NavMesh.AllAreas);

                    if (x == 0 && z == 0) line.Append('@');
                    else line.Append(walkable ? '.' : '#');

                    if (walkable) continue;

                    holes++;

                    // Что стоит в этой клетке. Берём столб от земли до трёх
                    // метров: именно эта высота мешает агенту.
                    foreach (var found in Physics.OverlapBox(
                                 at + Vector3.up * 1.5f,
                                 new Vector3(Step * 0.5f, 1.5f, Step * 0.5f),
                                 Quaternion.identity, ~0,
                                 QueryTriggerInteraction.Collide))
                    {
                        string who = Root(found.transform);
                        blame[who] = blame.TryGetValue(who, out int had) ? had + 1 : 1;
                    }
                }

                report.AppendLine(line.ToString());
            }

            report.AppendLine();
            report.AppendLine("Клеток без навигации: " + holes +
                              " из " + ((Half * 2 + 1) * (Half * 2 + 1)));
            report.AppendLine();
            report.AppendLine("КТО СТОИТ В ДЫРКАХ (клеток, объект):");

            foreach (var pair in blame.OrderByDescending(p => p.Value).Take(15))
                report.AppendLine("    " + pair.Value.ToString().PadLeft(4) + "  " + pair.Key);

            if (blame.Count == 0)
                report.AppendLine("    ничего — значит дырка не от коллайдера, " +
                                  "а от нарисованного меша или от края земли");

            // Препятствия навигации — они карвят сетку и коллайдера не имеют.
            var obstacles = Object.FindObjectsByType<NavMeshObstacle>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(o => Vector3.Distance(
                    new Vector3(o.transform.position.x, 0f, o.transform.position.z),
                    new Vector3(Spot.x, 0f, Spot.z)) < Half * Step + 6f)
                .ToArray();

            report.AppendLine();
            report.AppendLine("NavMeshObstacle рядом: " + obstacles.Length);

            foreach (var obstacle in obstacles.Take(20))
                report.AppendLine("    " + Path(obstacle.transform) +
                                  "   carve=" + obstacle.carving +
                                  "   размер " + obstacle.size);

            // Нарисованные меши, висящие низко над этой площадкой: навигация
            // строится по ним, и низкая крона режет сетку, даже если у неё
            // давно снят коллайдер.
            report.AppendLine();
            report.AppendLine("НИЗКО ВИСЯЩИЕ МЕШИ (низ ниже 3 м над землёй, центр в круге):");

            var low = Object.FindObjectsByType<MeshRenderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => Vector3.Distance(
                    new Vector3(r.bounds.center.x, 0f, r.bounds.center.z),
                    new Vector3(Spot.x, 0f, Spot.z)) < Half * Step)
                .Where(r => r.bounds.min.y < 3f && r.bounds.max.y > 0.4f)
                .Where(r => r.bounds.size.x > 1.5f || r.bounds.size.z > 1.5f)
                .OrderBy(r => Vector3.Distance(
                    new Vector3(r.bounds.center.x, 0f, r.bounds.center.z),
                    new Vector3(Spot.x, 0f, Spot.z)))
                .Take(25)
                .ToArray();

            foreach (var renderer in low)
                report.AppendLine("    " + Path(renderer.transform) +
                                  "   низ " + renderer.bounds.min.y.ToString("0.0") +
                                  "   верх " + renderer.bounds.max.y.ToString("0.0") +
                                  "   охват " + renderer.bounds.size.x.ToString("0.0") +
                                  "x" + renderer.bounds.size.z.ToString("0.0"));

            Debug.Log("[IsoRPG]\n" + report);
        }

        /// <summary>Имя самого верхнего осмысленного предка — кто это вообще.</summary>
        private static string Root(Transform t)
        {
            var top = t;
            while (top.parent != null && top.parent.parent != null) top = top.parent;
            return top.name + "  <-  " + t.name;
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            var cursor = t;

            while (cursor != null) { parts.Add(cursor.name); cursor = cursor.parent; }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
