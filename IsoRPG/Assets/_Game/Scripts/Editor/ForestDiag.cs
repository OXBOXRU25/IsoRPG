using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп по лесу: почему деревья кривые и почему листва стоит.
    ///
    /// Пишется отдельно от починки намеренно. Обе жалобы я уже пробовал
    /// закрыть догадкой — «наклон скопирован с прежних деревьев» и «нет
    /// контроллера ветра», — и обе догадки оказались мимо: выпрямление нашло
    /// ноль объектов, а контроллер встал и ничего не изменил. Значит место
    /// поломки я не знаю и должен сначала посмотреть.
    ///
    /// Печатает три вещи, каждая отвечает на свой вопрос:
    ///   • повороты деревьев и их родителей — откуда берётся наклон;
    ///   • шейдеры на кронах — умеет ли материал качаться вообще;
    ///   • контроллер ветра — стоит ли, включён ли, что раздаёт.
    /// </summary>
    public static class ForestDiag
    {
        [MenuItem("Tools/IsoRPG/Лес: диагностика наклона и ветра", priority = 60)]
        public static void Run()
        {
            var trees = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                              .Where(g => g.name.StartsWith("P_FFE_"))
                              .ToList();

            Debug.Log("[IsoRPG] Деревьев TriForge в сцене: " + trees.Count);

            if (trees.Count == 0)
            {
                Debug.LogWarning("[IsoRPG] Ни одного P_FFE_ не найдено — смотрю, что вообще стоит.");

                var names = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                                  .Select(g => g.name)
                                  .GroupBy(n => n)
                                  .OrderByDescending(g => g.Count())
                                  .Take(15);

                foreach (var g in names)
                    Debug.Log("   " + g.Count() + " x " + g.Key);

                return;
            }

            // ---- наклон -------------------------------------------------

            int tilted = 0;
            var parents = new Dictionary<string, int>();

            foreach (var t in trees.Take(400))
            {
                var w = t.transform.eulerAngles;
                float tiltX = Mathf.Abs(Mathf.DeltaAngle(w.x, 0f));
                float tiltZ = Mathf.Abs(Mathf.DeltaAngle(w.z, 0f));

                if (tiltX > 1f || tiltZ > 1f) tilted++;

                var p = t.transform.parent;
                string key = p == null
                    ? "(нет родителя)"
                    : p.name + " поворот " + Fmt(p.eulerAngles) +
                      " масштаб " + Fmt(p.localScale);

                parents[key] = parents.TryGetValue(key, out int n) ? n + 1 : 1;
            }

            Debug.Log("[IsoRPG] Наклонённых (по мировому повороту): " + tilted +
                      " из " + Mathf.Min(trees.Count, 400));

            foreach (var kv in parents.OrderByDescending(k => k.Value).Take(8))
                Debug.Log("   родитель: " + kv.Value + " x " + kv.Key);

            // Три первых дерева целиком: локальный поворот, мировой и путь.
            foreach (var t in trees.Take(3))
                Debug.Log("   " + Path(t.transform) +
                          "\n      локальный " + Fmt(t.transform.localEulerAngles) +
                          ", мировой " + Fmt(t.transform.eulerAngles) +
                          ", масштаб " + Fmt(t.transform.localScale));

            // ---- шейдеры крон -------------------------------------------

            var shaders = new Dictionary<string, int>();

            foreach (var t in trees.Take(60))
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null || m.shader == null) continue;
                        string key = m.shader.name + "   ← " + m.name;
                        shaders[key] = shaders.TryGetValue(key, out int n) ? n + 1 : 1;
                    }

            Debug.Log("[IsoRPG] Материалы деревьев:");
            foreach (var kv in shaders.OrderByDescending(k => k.Value))
                Debug.Log("   " + kv.Value + " x " + kv.Key);

            // ---- ветер ---------------------------------------------------

            var wind = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                             .FirstOrDefault(m => m != null &&
                                                  m.GetType().Name.Contains("WindController"));

            if (wind == null)
            {
                Debug.LogWarning("[IsoRPG] Контроллера ветра в сцене НЕТ.");
            }
            else
            {
                var type = wind.GetType();
                string vals = string.Join(", ", type.GetFields()
                    .Where(f => f.IsPublic)
                    .Select(f => f.Name + "=" + f.GetValue(wind)));

                Debug.Log("[IsoRPG] Ветер: объект «" + wind.gameObject.name +
                          "», активен " + wind.gameObject.activeInHierarchy +
                          ", компонент включён " + wind.enabled +
                          ", поворот " + Fmt(wind.transform.eulerAngles) +
                          "\n      " + vals);
            }

            // Глобальные переменные шейдера читаем прямо сейчас: если их
            // никто не выставил, они нули, и качаться нечему.
            Debug.Log("[IsoRPG] Глобальные ветра: сила " +
                      Shader.GetGlobalFloat("FFE_Wind_Strength") +
                      ", флаттер " + Shader.GetGlobalFloat("FFE_Leaf_Flutter") +
                      ", скорость " + Shader.GetGlobalFloat("FFE_Wind_Speed") +
                      ", направление " + Shader.GetGlobalVector("FFE_Wind_Direction"));
        }

        private static string Fmt(Vector3 v) =>
            "(" + v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ", " +
            v.z.ToString("0.0") + ")";

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null; c = c.parent) parts.Insert(0, c.name);
            return string.Join(" / ", parts);
        }
    }
}
