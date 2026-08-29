using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп дыр в навигационной сетке вокруг точки.
    ///
    /// «Упираюсь в невидимую стену, а зверь тут ходит» — верный признак
    /// дыры: ищущий путь обходит её и выглядит здоровым, а игрок, который
    /// правит клавишами напрямую, утыкается в край и стоит.
    ///
    /// Печатает карту проходимости квадратом вокруг места жалобы: точка
    /// есть — сетка под ней, нет — дыра. Смотреть глазами бесполезно, дыра
    /// невидима.
    /// </summary>
    public static class NavHoleProbe
    {
        /// <summary>Куда смотреть. Место, где Павлон упирается.</summary>
        private static readonly Vector2 Centre = new Vector2(44f, -21f);

        /// <summary>Полуразмер квадрата и шаг, метры.</summary>
        private const float Half = 12f, Step = 2f;

        [MenuItem("Tools/IsoRPG/Щуп: дыры в навигации", priority = 48)]
        public static void Run()
        {
            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)[0];

            var text = new StringBuilder();
            int holes = 0, total = 0;

            for (float z = Centre.y + Half; z >= Centre.y - Half; z -= Step)
            {
                var line = new StringBuilder();

                for (float x = Centre.x - Half; x <= Centre.x + Half; x += Step)
                {
                    float y = terrain.SampleHeight(new Vector3(x, 0f, z)) +
                              terrain.transform.position.y;

                    // Радиус поиска маленький: нас интересует сетка ИМЕННО
                    // здесь, а не ближайшая в шести метрах.
                    bool ok = NavMesh.SamplePosition(new Vector3(x, y, z),
                                                     out _, 0.6f, NavMesh.AllAreas);

                    line.Append(ok ? '.' : '#');
                    total++;
                    if (!ok) holes++;
                }

                text.Append(line).Append('\n');
            }

            Debug.Log("[IsoRPG] Навигация вокруг (" + Centre.x + ", " + Centre.y + "), " +
                      "квадрат " + (Half * 2) + " м, шаг " + Step + " м. " +
                      "Точка — сетка есть, решётка — дыра. Дыр " + holes + " из " + total +
                      " (" + (100f * holes / total).ToString("0") + "%).\n" + text);
        }
    }
}
