using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп живых тел: из чего они сделаны и чем отличаются от декораций.
    ///
    /// Заведён 01.09.2026 после двух подряд промахов с отбором. Сперва я взял
    /// признаком навигационного агента — первая лошадь его не носит; потом
    /// скелетную сетку — лошадь всё равно осталась стеной. Хватит гадать:
    /// щуп печатает, что на самом деле висит на каждом существе в сцене, и
    /// признак выбирается по фактам, а не по догадке.
    /// </summary>
    public static class CreatureProbe
    {

        /// <summary>
        /// Что стоит рядом с героем и может его удержать.
        ///
        /// Павлон 01.09.2026: «лошадь первая стоит на открытой площадке около
        /// игрока» и отталкивает. Гадать, какой это объект, больше не будем —
        /// печатаем всё, у чего есть физический коллайдер в пятнадцати метрах
        /// от героя, с расстоянием и слоем.
        /// </summary>
        public static void Near()
        {
            var player = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                               .FirstOrDefault(g => g.name == "Player");

            if (player == null) { Debug.LogWarning("[IsoRPG] Игрока в сцене нет."); return; }

            Vector3 at = player.transform.position;

            var near = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(c => !c.isTrigger && Vector3.Distance(c.bounds.center, at) < 15f)
                .OrderBy(c => Vector3.Distance(c.bounds.center, at))
                .Take(25)
                .Select(c =>
                {
                    var owner = c.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
                    var skin = c.GetComponentInParent<Animator>();
                    return $"  {Vector3.Distance(c.bounds.center, at),5:F1} м  {c.gameObject.name,-32} {c.GetType().Name,-16}" +
                           $" слой {c.gameObject.layer,2} | агент выше: {(owner != null ? owner.name : "нет"),-18}" +
                           $" | аниматор выше: {(skin != null ? "да" : "нет")} | размер {c.bounds.size.x:F1}×{c.bounds.size.y:F1}×{c.bounds.size.z:F1}";
                });

            Debug.Log($"[IsoRPG] Рядом с героем ({at.x:F1}, {at.z:F1}):\n" + string.Join("\n", near));
        }
        public static void Report()
        {
            var all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Слои с номерами: без номера имя бесполезно, а решение будет
            // именно про номер слоя.
            var layers = string.Join(", ", Enumerable.Range(0, 32)
                .Select(i => new { i, n = LayerMask.LayerToName(i) })
                .Where(x => !string.IsNullOrEmpty(x.n))
                .Select(x => x.i + ":" + x.n));

            // Существо — то, у чего есть агент, аниматор или скелет. Здесь это
            // не отбор для правки, а перепись: смотрим, на каких слоях лежат
            // их коллайдеры и много ли осталось на общем нулевом.
            var creatures = all.Where(g => g.GetComponent<NavMeshAgent>() != null
                                        || g.GetComponent<Animator>() != null
                                        || g.GetComponentInChildren<SkinnedMeshRenderer>() != null).ToArray();

            var byLayer = creatures
                .SelectMany(g => g.GetComponentsInChildren<Collider>(true))
                .GroupBy(c => c.gameObject.layer)
                .OrderByDescending(g => g.Count())
                .Select(g => $"слой {g.Key} ({LayerMask.LayerToName(g.Key)}): {g.Count()}");

            // Корни существ: у кого есть агент ЛИБО имя выдаёт живого.
            var roots = all.Where(g =>
                g.GetComponent<NavMeshAgent>() != null ||
                g.name.ToLowerInvariant().Contains("horse") ||
                g.name.ToLowerInvariant().Contains("лошад") ||
                g.name.ToLowerInvariant().Contains("талин") ||
                g.name.ToLowerInvariant().Contains("кабан") ||
                g.name.ToLowerInvariant().Contains("npc")).ToArray();

            var rows = roots.Select(g =>
            {
                var cols = g.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
                string where = cols.Length == 0 ? "физических коллайдеров нет"
                    : string.Join(" ", cols.GroupBy(c => c.gameObject.layer).Select(x => $"слой{x.Key}×{x.Count()}"));

                return $"  {g.name,-30} слой {g.layer,2} | агент {(g.GetComponent<NavMeshAgent>() != null ? "да " : "нет")}" +
                       $"| скелет {(g.GetComponentInChildren<SkinnedMeshRenderer>() != null ? "да " : "нет")}| {where}";
            });

            Debug.Log(
                $"[IsoRPG] Щуп существ. Сцена: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n" +
                $"  слои: {layers}\n" +
                $"  тел-существ {creatures.Length}, их коллайдеры по слоям: {string.Join(" | ", byLayer)}\n" +
                $"  корни ({roots.Length}):\n" + string.Join("\n", rows.Take(30)));
        }
    }
}
