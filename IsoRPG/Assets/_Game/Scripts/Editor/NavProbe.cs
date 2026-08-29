using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп навигации: отвечает на вопрос «сетка вообще есть?» числами.
    ///
    /// Повод. В собранной игре 121 раз за запуск падало
    /// «Failed to create agent because there is no valid NavMesh»: агенты не
    /// создавались, и столько же существ просто стояло. При этом ни в сцене,
    /// ни в журнале ничего не выглядело сломанным — выпечка навигации не
    /// ругается, когда печь было не из чего.
    ///
    /// Поэтому мерим не факт вызова, а результат: сколько вершин в готовой
    /// сетке и какой участок мира она накрывает. Ноль вершин — сетки нет,
    /// каким бы успешным ни выглядел прогон.
    /// </summary>
    public static class NavProbe
    {
        /// <summary>
        /// Что сейчас лежит в навигации сцены.
        /// </summary>
        public static void Report(string when)
        {
            var tri = NavMesh.CalculateTriangulation();

            int verts = tri.vertices != null ? tri.vertices.Length : 0;
            int tris = tri.indices != null ? tri.indices.Length / 3 : 0;

            if (verts == 0)
            {
                Debug.LogError("[IsoRPG] Навигация (" + when + "): СЕТКИ НЕТ. " +
                               "Ноль вершин — агенты создаваться не будут.");
                return;
            }

            var box = new Bounds(tri.vertices[0], Vector3.zero);
            foreach (var v in tri.vertices) box.Encapsulate(v);

            Debug.Log("[IsoRPG] Навигация (" + when + "): вершин " + verts +
                      ", треугольников " + tris +
                      ", охват " + box.size.x.ToString("0") + " x " +
                      box.size.z.ToString("0") + " м, центр " +
                      box.center.ToString("0.0") + ".");
        }

        /// <summary>
        /// Замерить, перепечь, замерить снова.
        ///
        /// Два замера вокруг одной правки — единственный способ отличить
        /// «починили» от «и так работало». Одного замера после недостаточно:
        /// он не показывает, что изменилось.
        /// </summary>
        public static void RebakeAndReport()
        {
            Report("до выпечки");

            // Что вообще есть в сцене, на чём печь.
            var terrains = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            var agents = Object.FindObjectsByType<NavMeshAgent>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Debug.Log("[IsoRPG] В сцене террейнов " + terrains.Length +
                      ", агентов " + agents.Length + ".");

            if (terrains.Length > 0)
            {
                var t = terrains.First();
                var td = t.terrainData;

                Debug.Log("[IsoRPG] Террейн «" + t.name + "»: размер " +
                          td.size.x.ToString("0") + " x " + td.size.z.ToString("0") +
                          " м, позиция " + t.transform.position.ToString("0.0") +
                          ", коллайдер " +
                          (t.GetComponent<TerrainCollider>() != null ? "есть" : "НЕТ") +
                          ".");
            }

            EnsureRescue();

            NavBake.Rebake();

            // Сцену пометить грязной обязательно: NavMeshSurface.BuildNavMesh
            // меняет данные, но сцену такой не помечает, и в пакетном режиме
            // всё это пропадёт вместе с процессом — прогон отчитается
            // успехом, а в игре ничего не изменится.
            EditorSceneManager.MarkAllScenesDirty();

            Report("после выпечки");
        }

        /// <summary>
        /// Посадить в сцену спасателя агентов, если его там ещё нет.
        ///
        /// Отдельным корневым объектом, а не на террейне: террейн
        /// пересобирается, а спасатель должен пережить пересборку.
        /// </summary>
        private static void EnsureRescue()
        {
            var existing = Object.FindFirstObjectByType<IsoRPG.World.NavAgentRescue>(
                FindObjectsInactive.Include);

            if (existing != null)
            {
                Debug.Log("[IsoRPG] Спасатель агентов уже в сцене.");
                return;
            }

            var go = new GameObject("NavAgentRescue");
            go.AddComponent<IsoRPG.World.NavAgentRescue>();

            Debug.Log("[IsoRPG] Спасатель агентов посажен в сцену.");
        }
    }
}
