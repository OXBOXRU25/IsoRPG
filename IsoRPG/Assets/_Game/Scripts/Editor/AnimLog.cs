using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Вешает и снимает щуп <see cref="IsoRPG.Combat.AnimatorProbe"/> на боссов.
    ///
    /// Нужен, когда заказчик видит одно, а замеры в редакторе — другое: щуп
    /// пишет в журнал самой игры, какое состояние аниматора играет на самом
    /// деле. Ставится на акцентных врагов, а не на всех: десять волков
    /// зальют журнал.
    /// </summary>
    public static class AnimLog
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        private static readonly string[] Watched = { "Босс-кабан", "Гриб-исполин" };

        [MenuItem("Tools/IsoRPG/Щуп: журнал анимаций боссам", priority = 47)]
        public static void On() => Apply(true);

        [MenuItem("Tools/IsoRPG/Щуп: снять журнал анимаций", priority = 48)]
        public static void Off() => Apply(false);

        private static void Apply(bool on)
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            int done = 0;

            foreach (var name in Watched)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;

                var probe = go.GetComponent<IsoRPG.Combat.AnimatorProbe>();

                if (on && probe == null) { go.AddComponent<IsoRPG.Combat.AnimatorProbe>(); done++; }
                else if (!on && probe != null) { Object.DestroyImmediate(probe); done++; }
            }

            // Пометить грязной ОБЯЗАТЕЛЬНО: AddComponent из кода сцену не
            // помечает, а SaveOpenScenes пишет только грязные — правка уходит
            // в никуда при открытии следующей сцены, и отчёт при этом честный.
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[IsoRPG] Журнал анимаций " + (on ? "включён" : "снят") + " у " + done + " существ.");
        }
    }
}
