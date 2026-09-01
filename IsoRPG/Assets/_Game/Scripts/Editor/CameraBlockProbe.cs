using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп: чем камера может зажиматься в мире.
    ///
    /// Павлон 01.09.2026: «стою за деревом, делаю шаг — камера придвигается
    /// почти вплотную, ещё шаг — возвращается». Прежде чем править камеру,
    /// надо знать, во что именно упирается её луч: в ствол или в крону.
    /// Ствол — законное препятствие, как в WoW; крона — нет, у неё объём в
    /// несколько метров, и в лесу камера будет дёргаться постоянно.
    ///
    /// Печатаем по видам растительности: какой коллайдер, какого размера, и
    /// сколько таких в сцене. Ничего не меняет.
    /// </summary>
    public static class CameraBlockProbe
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        [MenuItem("Tools/IsoRPG/Щуп: чем зажимается камера", priority = 40)]
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            // Группируем по виду: имя без номера копии. Иначе получим
            // простыню из тысячи строк про одно и то же дерево.
            var kinds = new Dictionary<string, (int count, string collider, Vector3 size, int layer)>();

            foreach (var col in Object.FindObjectsByType<Collider>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (col == null || col.isTrigger) continue;

                // Живые уже на своём слое и камеру не блокируют — их не считаем.
                if (col.gameObject.layer == LayerMask.NameToLayer("Characters")) continue;
                if (col is TerrainCollider) continue;

                string kind = Kind(col.gameObject.name);
                var size = col.bounds.size;

                if (kinds.TryGetValue(kind, out var had))
                    kinds[kind] = (had.count + 1, had.collider, had.size, had.layer);
                else
                    kinds[kind] = (1, col.GetType().Name, size, col.gameObject.layer);
            }

            var text = new StringBuilder("[IsoRPG] Что блокирует камеру (без живых и террейна):\n");

            foreach (var pair in kinds.OrderByDescending(p => p.Value.count).Take(25))
            {
                var v = pair.Value;

                text.Append("  ").Append(pair.Key.PadRight(34))
                    .Append(v.count.ToString().PadLeft(5)).Append(" шт, ")
                    .Append(v.collider.PadRight(14))
                    .Append(v.size.x.ToString("0.0")).Append('×')
                    .Append(v.size.y.ToString("0.0")).Append('×')
                    .Append(v.size.z.ToString("0.0")).Append(" м, слой ")
                    .Append(LayerMask.LayerToName(v.layer))
                    .Append('\n');
            }

            text.Append("  видов всего ").Append(kinds.Count)
                .Append(", коллайдеров ").Append(kinds.Sum(p => p.Value.count));

            Debug.Log(text.ToString());
        }

        /// <summary>Имя вида: отбрасываем номер копии и суффиксы уровней детализации.</summary>
        private static string Kind(string name)
        {
            int bracket = name.IndexOf(" (");
            if (bracket > 0) name = name.Substring(0, bracket);

            int lod = name.IndexOf("_LOD");
            if (lod > 0) name = name.Substring(0, lod);

            return name;
        }
    }
}
