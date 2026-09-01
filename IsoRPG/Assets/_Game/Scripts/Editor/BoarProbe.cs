using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп по кабанам: что на самом деле играет и сколько длится.
    ///
    /// Заведён 01.09.2026. Правка «не слать вздрагивание чаще, чем оно
    /// длится» не помогла — значит причина не в частоте, и следующую догадку
    /// проверяем замером, а не правкой. Печатаем то, чего из кода не видно:
    /// длину каждого клипа, есть ли в нём собственное движение корня, куда
    /// возвращается состояние и с каким интервалом бьёт зверь.
    ///
    /// Ничего не меняет — только читает.
    /// </summary>
    public static class BoarProbe
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        [MenuItem("Tools/IsoRPG/Щуп: анимации кабанов", priority = 38)]
        public static void Run()
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            Report("Assets/_Game/Art/Animations/Controllers/AC_Boar.controller", "МЕЛКИЙ КАБАН");
            Report("Assets/_Game/Art/Animations/Controllers/AC_BoarBoss.controller", "БОСС");

            Fighters();
        }

        private static void Report(string path, string who)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            if (controller == null)
            {
                Debug.LogError("[IsoRPG] Нет контроллера " + path);
                return;
            }

            var root = controller.layers[0].stateMachine;
            var text = new StringBuilder();

            text.Append("[IsoRPG] ").Append(who).Append(" — ").Append(controller.name)
                .Append(", состояние по умолчанию: ")
                .Append(root.defaultState != null ? root.defaultState.name : "НЕТ")
                .Append('\n');

            foreach (var child in root.states)
            {
                var state = child.state;
                var clip = state.motion as AnimationClip;

                text.Append("  ").Append(state.name.PadRight(14));

                if (clip == null)
                {
                    text.Append(state.motion == null ? "— клипа нет" : "— смесь: " + state.motion.name);
                }
                else
                {
                    text.Append(clip.length.ToString("0.00")).Append(" с");
                    if (clip.hasRootCurves) text.Append(", своё движение корня");
                    if (clip.isLooping) text.Append(", зациклен");
                }

                // Куда уходит и по какому условию: залипание видно отсюда.
                foreach (var t in state.transitions)
                {
                    text.Append("\n      → ")
                        .Append(t.isExit ? "ВЫХОД" : (t.destinationState != null ? t.destinationState.name : "?"))
                        .Append(t.hasExitTime ? $" по концу {t.exitTime:0.00}" : " без ожидания")
                        .Append(", смена ").Append(t.duration.ToString("0.00"));

                    if (t.conditions.Length > 0)
                        text.Append(", если ").Append(string.Join(" и ", t.conditions.Select(c => c.parameter)));
                }

                text.Append('\n');
            }

            // Вход из Any State: он не принадлежит состояниям и в списке выше не виден.
            foreach (var t in root.anyStateTransitions)
            {
                text.Append("  ЛЮБОЕ → ").Append(t.destinationState != null ? t.destinationState.name : "?")
                    .Append(t.conditions.Length > 0 ? ", если " + string.Join(" и ", t.conditions.Select(c => c.parameter)) : "")
                    .Append(", смена ").Append(t.duration.ToString("0.00"))
                    .Append(t.canTransitionToSelf ? ", можно в себя" : "")
                    .Append('\n');
            }

            Debug.Log(text.ToString());
        }

        /// <summary>Что стоит на самих зверях сцены: интервал удара и движение из клипа.</summary>
        private static void Fighters()
        {
            var text = new StringBuilder("[IsoRPG] Звери на сцене:\n");

            foreach (var fighter in Object.FindObjectsByType<IsoRPG.Combat.MeleeCombatant>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (fighter == null) continue;
                if (!fighter.name.Contains("абан")) continue;

                var animator = fighter.GetComponentInChildren<Animator>(true);

                var interval = new SerializedObject(fighter).FindProperty("attackInterval");

                text.Append("  ").Append(fighter.name.PadRight(14))
                    .Append("удар раз в ").Append(interval != null ? interval.floatValue.ToString("0.00") : "?")
                    .Append(" с, движение из клипа ")
                    .Append(animator != null && animator.applyRootMotion ? "ВКЛ" : "выкл")
                    .Append(", контроллер ")
                    .Append(animator != null && animator.runtimeAnimatorController != null
                                ? animator.runtimeAnimatorController.name : "НЕТ")
                    .Append('\n');
            }

            Debug.Log(text.ToString());
        }
    }
}
