using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Снимает встроенный обход агентов со ВСЕХ существ сцены.
    ///
    /// 01.09.2026: у лошади стоял HighQualityObstacleAvoidance, и она сама
    /// отходила, завидев агента героя. Павлон: «убери механику отталкивания
    /// вообще — я должен заходить в их текстуры». Расхождение остаётся ровно
    /// одно: отшаг моба, который с тобой дерётся (BodySpace).
    ///
    /// Задание правит сцену, а <c>BodySpace</c> делает то же в игре при старте.
    /// Двух мест не избежать: существ расставляют разные сборщики, и надёжнее
    /// снять настройку и в файле сцены, и в рантайме, чем помнить про каждый.
    /// В конце — щуп: читаем, что получилось, а не верим отчёту.
    /// </summary>
    public static class NoAvoidance
    {
        [MenuItem("Tools/IsoRPG/Существа: снять обход агентов", priority = 36)]
        public static void Apply()
        {
            var agents = Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            int changed = 0;

            foreach (var agent in agents)
            {
                if (agent == null) continue;
                if (agent.obstacleAvoidanceType == ObstacleAvoidanceType.NoObstacleAvoidance) continue;

                agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                EditorUtility.SetDirty(agent);
                changed++;
            }

            EditorSceneManager.SaveOpenScenes();

            var left = Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                             .Where(a => a != null && a.obstacleAvoidanceType != ObstacleAvoidanceType.NoObstacleAvoidance)
                             .Select(a => a.name)
                             .ToArray();

            Debug.Log(
                $"[IsoRPG] Обход агентов снят. Агентов всего {agents.Length}, поправлено {changed}.\n" +
                $"  осталось с обходом: {(left.Length == 0 ? "никого" : string.Join(", ", left.Take(10)))}");
        }
    }
}
