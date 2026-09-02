using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп: что на самом деле стоит на аниматорах существ в боевой сцене.
    ///
    /// Заведён 02.09.2026, когда Павлон сказал «у кабана тоже не вижу новых
    /// анимаций». Контроллер к тому моменту был пересобран на 22 состояния,
    /// и гадать, доехало это до сцены или нет, дешевле один раз замерить.
    ///
    /// Печатает по каждому существу: контроллер, аватар, число состояний и
    /// параметров. Ничего не меняет.
    /// </summary>
    public static class MobAnimProbe
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        [MenuItem("Tools/IsoRPG/Щуп: аниматоры существ", priority = 46)]
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            var text = new StringBuilder("[IsoRPG] Аниматоры существ:\n");
            var seen = new System.Collections.Generic.HashSet<string>();

            foreach (var target in Object.FindObjectsByType<IsoRPG.Combat.Targetable>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (target == null) continue;

                // По одному представителю на вид: десять волков в отчёте
                // ничего не добавляют.
                string kind = System.Text.RegularExpressions.Regex.Replace(target.name, @"\s*\d+$", "");
                if (!seen.Add(kind)) continue;

                var animator = target.GetComponentInChildren<Animator>(true);

                text.Append("  ").Append(kind.PadRight(18));

                if (animator == null)
                {
                    text.Append("АНИМАТОРА НЕТ\n");
                    continue;
                }

                var controller = animator.runtimeAnimatorController as AnimatorController;

                text.Append(controller == null ? "КОНТРОЛЛЕРА НЕТ" : controller.name.PadRight(18))
                    .Append(animator.avatar == null ? "  АВАТАРА НЕТ" : "  аватар есть")
                    .Append(animator.applyRootMotion ? ", движение из клипа ВКЛ" : "");

                if (controller != null)
                {
                    var machine = controller.layers[0].stateMachine;

                    text.Append(", состояний ").Append(machine.states.Length)
                        .Append(", параметров ").Append(controller.parameters.Length);

                    // Сколько состояний реально с клипом: пустое состояние —
                    // это молчащая анимация, и по числу состояний её не видно.
                    int withClip = machine.states.Count(s => s.state != null && s.state.motion != null);
                    if (withClip != machine.states.Length)
                        text.Append(", БЕЗ КЛИПА ").Append(machine.states.Length - withClip);
                }

                text.Append('\n');
            }

            Debug.Log(text.ToString());
        }
    }
}
