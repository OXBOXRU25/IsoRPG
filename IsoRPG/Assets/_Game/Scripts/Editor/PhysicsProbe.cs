using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп физики мира: что в сцене стоит без коллайдера и чем это накрывать.
    ///
    /// Заведён 01.09.2026. Причина: залезание в мобов, невидимые стены, кривой
    /// прыжок и застревание на камнях оказались не четырьмя багами, а одним
    /// следствием — движение живёт на навигационной сетке, а физики препятствий
    /// в мире нет вовсе. Прежде чем накрывать мир коллайдерами, надо знать, что
    /// именно накрывать: сеточный коллайдер на тысячах объектов уронит
    /// производительность, а коробка на дереве соберёт крону.
    ///
    /// Щуп ничего не меняет. Он читает сцену и печатает таблицу групп:
    /// сколько объектов, какие габариты, что уже накрыто, и какой тип
    /// коллайдера просится по форме.
    /// </summary>
    public static class PhysicsProbe
    {
        /// <summary>Объекты ниже этого — мусор под ногами, физика им не нужна.</summary>
        private const float SkipHeight = 0.35f;

        /// <summary>Имена, которые физикой не накрываем ни при каких габаритах.</summary>
        private static readonly string[] Skip =
        {
            "terrain", "water", "sky", "skydome", "cloud", "backdrop",
            "grass", "plant", "flower", "fern", "bush_small", "groundcover",
            "player", "wolf", "boar", "horse", "npc", "camera", "light",
        };


        /// <summary>
        /// Прямая перепись коллайдеров сцены — контроль к оценке выше.
        ///
        /// Заведена сразу за первым замером: он показал 80% мира накрытым, а
        /// прошлая сессия докладывала обратное. Оценка «накрыто» смотрит на
        /// предков объекта, и один коллайдер на корне префаба зачитывает всех
        /// детей; выключенный коллайдер и триггер тоже прошли бы за физику.
        /// Эта перепись считает сами компоненты и разбирает их по состоянию.
        /// </summary>
        public static void Census()
        {
            var all = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            var byType = all.GroupBy(c => c.GetType().Name)
                            .OrderByDescending(g => g.Count())
                            .Select(g => $"{g.Key} {g.Count()}");

            int off = all.Count(c => !c.enabled);
            int trig = all.Count(c => c.isTrigger);
            int onCreature = all.Count(c => c.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null);
            int inactive = all.Count(c => !c.gameObject.activeInHierarchy);

            // Слой Ignore Raycast и выключенные объекты физику тоже не дают.
            var live = all.Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy).ToArray();

            Debug.Log(
                $"[IsoRPG] Перепись коллайдеров. Сцена: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n" +
                $"  всего компонентов {all.Length}: {string.Join(", ", byType)}\n" +
                $"  выключено {off}, триггеров {trig}, на объекте с агентом {onCreature}, объект неактивен {inactive}\n" +
                $"  РАБОЧИХ препятствий {live.Length}\n" +
                $"  примеры рабочих: {string.Join(" | ", live.Take(8).Select(c => c.gameObject.name + "/" + c.GetType().Name))}");
        }
        public static void Report()
        {
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            var groups = new Dictionary<string, List<MeshRenderer>>();
            int skipped = 0, tiny = 0;

            foreach (var r in renderers)
            {
                string lower = r.gameObject.name.ToLowerInvariant();
                if (Skip.Any(s => lower.Contains(s))) { skipped++; continue; }
                if (r.bounds.size.y < SkipHeight) { tiny++; continue; }

                string key = Regex.Replace(r.gameObject.name, @"\s*\(\d+\)|\(Clone\)|_\d+$", "").Trim();
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<MeshRenderer>();
                list.Add(r);
            }

            int total = 0, covered = 0;
            var rows = new List<string>();

            foreach (var pair in groups.OrderByDescending(p => p.Value.Count))
            {
                var list = pair.Value;
                int withCollider = list.Count(r => r.GetComponentInParent<Collider>() != null);
                total += list.Count;
                covered += withCollider;

                var b = list[0].bounds.size;
                float w = Mathf.Max(b.x, b.z), h = b.y;
                // Высокое и узкое — капсула; приземистое или угловатое — коробка.
                string suggest = h > w * 1.6f ? "капсула" : "коробка";

                rows.Add($"  {pair.Key,-34} {list.Count,4} шт  {w,6:F2}×{h,6:F2} м  накрыто {withCollider,4}  → {suggest}");
            }

            Debug.Log(
                $"[IsoRPG] Щуп физики. Сцена: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n" +
                $"  мешей всего {renderers.Length}, пропущено по имени {skipped}, ниже {SkipHeight:F2} м {tiny}\n" +
                $"  кандидатов на физику {total} в {groups.Count} группах, из них уже накрыто {covered}\n" +
                string.Join("\n", rows.Take(45)) +
                (rows.Count > 45 ? $"\n  ...ещё {rows.Count - 45} групп" : ""));
        }
    }
}
