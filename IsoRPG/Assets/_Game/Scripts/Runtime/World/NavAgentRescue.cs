using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.World
{
    /// <summary>
    /// Поднимает агентов, проигравших гонку с загрузкой навигационной сетки.
    ///
    /// <b>Почему гонка неизбежна.</b> Сетку кладёт в мир компонент
    /// NavMeshSurface — обычный скрипт, и его OnEnable приходит вместе с
    /// остальными скриптами сцены. А NavMeshAgent — родной компонент
    /// движка: он поднимается в момент создания объекта, то есть ДО любого
    /// скрипта. Пока агент и поверхность лежат в одной сцене, агент
    /// спрашивает про сетку раньше, чем её положили, и получает отказ:
    /// «Failed to create agent because there is no valid NavMesh».
    ///
    /// Дальше существо стоит столбом навсегда: повторных попыток агент не
    /// делает. В арене таких было 121 — то есть все до единого.
    ///
    /// <b>Что делаем.</b> Через кадр после загрузки — когда поверхность уже
    /// отработала — переключаем каждому неудачнику агент выключить-включить.
    /// Это заставляет его попробовать снова, теперь уже по готовой сетке.
    /// Кто оказался в стороне от сетки, того подтягиваем к ближайшей точке.
    ///
    /// <b>Отчитываемся числами.</b> Молчаливое спасение неотличимо от
    /// молчаливого бездействия, а именно это нас сюда и привело.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class NavAgentRescue : MonoBehaviour
    {
        /// <summary>Насколько далеко искать сетку под тем, кто встал мимо неё.</summary>
        private const float SearchRadius = 8f;

        private IEnumerator Start()
        {
            // Ждём кадр: к следующему все OnEnable отработали, и сетка,
            // если её вообще кладут, уже на месте.
            yield return null;

            var tri = NavMesh.CalculateTriangulation();
            int verts = tri.vertices != null ? tri.vertices.Length : 0;

            if (verts == 0)
            {
                Debug.LogError("[IsoRPG] Спасатель агентов: сетки нет и через кадр " +
                               "после загрузки. Дело не в порядке запуска — " +
                               "сетка не попала в сборку вовсе.");
                yield break;
            }

            var agents = FindObjectsByType<NavMeshAgent>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int broken = 0, revived = 0, warped = 0, lost = 0;

            foreach (var agent in agents)
            {
                if (agent == null || agent.isOnNavMesh) continue;

                broken++;

                // Переподнятие: агент пробует встать на сетку заново.
                agent.enabled = false;
                agent.enabled = true;

                if (agent.isOnNavMesh) { revived++; continue; }

                // Встал мимо сетки — подтягиваем к ближайшей проходимой точке.
                if (NavMesh.SamplePosition(agent.transform.position, out var hit,
                                           SearchRadius, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    if (agent.isOnNavMesh) { warped++; continue; }
                }

                lost++;
            }

            Debug.Log("[IsoRPG] Спасатель агентов: сетка есть (вершин " + verts +
                      "). Агентов " + agents.Length +
                      ", лежачих было " + broken +
                      ", поднято переподключением " + revived +
                      ", подтянуто к сетке " + warped +
                      ", осталось лежать " + lost + ".");
        }
    }
}
