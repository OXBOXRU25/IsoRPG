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

        /// <summary>
        /// Что на самом деле висит на герое и как он движется.
        ///
        /// Заведён 01.09.2026, когда лошадь продолжила отталкивать после трёх
        /// правок физики. Прежде чем чинить четвёртый раз, надо убедиться, что
        /// правки вообще применены: если мотора на герое нет, он ходит
        /// навигационным агентом, а агенты расталкиваются своим встроенным
        /// обходом — и физика тут ни при чём.
        /// </summary>
        public static void Hero()
        {
            var player = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                               .FirstOrDefault(g => g.name == "Player");

            if (player == null) { Debug.LogWarning("[IsoRPG] Игрока в сцене нет."); return; }

            var body = player.GetComponent<CharacterController>();
            var agent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
            var motor = player.GetComponents<MonoBehaviour>().FirstOrDefault(m => m != null && m.GetType().Name == "PlayerMotor");

            string avoidance = agent == null ? "агента нет"
                : $"качество обхода {agent.obstacleAvoidanceType}, приоритет {agent.avoidancePriority}, радиус {agent.radius:F2}";

            Debug.Log(
                $"[IsoRPG] Герой в сцене {player.scene.name}:\n" +
                $"  CharacterController: {(body != null ? $"есть, радиус {body.radius:F2}, рост {body.height:F2}" : "НЕТ")}\n" +
                $"  PlayerMotor: {(motor != null ? "есть" : "НЕТ")}\n" +
                $"  агент двигает позицию: {(agent != null ? agent.updatePosition.ToString() : "-")}\n" +
                $"  обход препятствий агентом: {avoidance}\n" +
                $"  компоненты: {string.Join(", ", player.GetComponents<Component>().Select(c => c.GetType().Name))}");
        }

        /// <summary>
        /// Состояние аниматоров у существ: кто чем анимирован и работает ли это.
        ///
        /// Заведён 01.09.2026: лошадь «встала как камень». Контроллер и клип на
        /// месте, значит дело в самом объекте — печатаем то, что обычно и
        /// ломается: выключенный аниматор, отсутствующий аватар, пустой
        /// контроллер, чужая культя вместо модели.
        /// </summary>
        public static void Animators()
        {
            var rows = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(a => a != null)
                .Take(30)
                .Select(a =>
                {
                    string ctrl = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "НЕТ";
                    string avatar = a.avatar != null ? (a.avatar.isValid ? "есть" : "негоден") : "НЕТ";
                    return $"  {a.gameObject.name,-28} включён {(a.enabled ? "да " : "НЕТ")}| объект {(a.gameObject.activeInHierarchy ? "жив" : "выкл")}" +
                           $" | контроллер {ctrl,-18} | аватар {avatar,-8} | культя {(a.isHuman ? "человек" : "прочее")}";
                });

            Debug.Log("[IsoRPG] Аниматоры сцены:\n" + string.Join("\n", rows));
        }
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

            var agents = Object.FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(a => Vector3.Distance(a.transform.position, at) < 20f)
                .OrderBy(a => Vector3.Distance(a.transform.position, at))
                .Select(a => $"{a.name}: обход {a.obstacleAvoidanceType}, приоритет {a.avoidancePriority}, радиус {a.radius:F2}, останов {a.stoppingDistance:F1}");
            Debug.Log("[IsoRPG] Агенты рядом (кто кого обходит): " + string.Join(" | ", agents));

            var obstacles = Object.FindObjectsByType<UnityEngine.AI.NavMeshObstacle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(o => Vector3.Distance(o.transform.position, at) < 20f)
                .Select(o => $"{o.name} (вырезает: {o.carving}, размер {o.size.x:F1}×{o.size.z:F1}, {Vector3.Distance(o.transform.position, at):F1} м)");

            Debug.Log("[IsoRPG] Помехи навигации рядом: " + string.Join(" | ", obstacles));

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
