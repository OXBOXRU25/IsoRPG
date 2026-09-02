using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Раздаёт праздное поведение всем, чей аниматор его понимает.
    ///
    /// Признак один: в контроллере есть параметр `Rest`. Так правило
    /// накрывает весь класс — сегодня это босс-кабан, завтра любой зверь, чей
    /// набор принесёт покой, и вспоминать про него не придётся.
    /// </summary>
    public static class IdleKit
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        [MenuItem("Tools/IsoRPG/Существа: раздать праздное поведение", priority = 49)]
        public static void Apply()
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            int added = 0, already = 0, skipped = 0;

            foreach (var target in Object.FindObjectsByType<IsoRPG.Combat.Targetable>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (target == null) continue;

                var animator = target.GetComponentInChildren<Animator>(true);

                if (animator == null || animator.runtimeAnimatorController == null) { skipped++; continue; }

                bool understands = false;

                foreach (var p in animator.parameters)
                    if (p.name == "Rest") { understands = true; break; }

                if (!understands) { skipped++; continue; }

                if (target.GetComponent<IsoRPG.Combat.IdleBehaviour>() != null) { already++; continue; }

                target.gameObject.AddComponent<IsoRPG.Combat.IdleBehaviour>();
                added++;
            }

            // Пометить грязной ОБЯЗАТЕЛЬНО: AddComponent из кода сцену не
            // помечает, а SaveOpenScenes пишет только грязные — правка уходит
            // в никуда при открытии следующей сцены, и отчёт при этом честный.
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[IsoRPG] Праздное поведение: добавлено {added}, уже было {already}, " +
                      $"не понимают покой {skipped}.");
        }
    }
}
