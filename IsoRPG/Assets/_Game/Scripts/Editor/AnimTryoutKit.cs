using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Наполняет примерку анимаций вариантами из набора.
    ///
    /// Варианты собираются ЗДЕСЬ, а не в игре: пути к файлам знает редактор,
    /// а в собранной игре ассетов по путям уже нет. Заодно это единственное
    /// место, где перечислено, что у набора вообще есть по каждой части
    /// пластики, — и печатается это вслух, чтобы решение принималось по
    /// списку, а не по тому, что я вспомнил.
    /// </summary>
    public static class AnimTryoutKit
    {
        private const string Root = "Assets/DoubleL/FBX_Animations";

        private const string OneHand = Root + "/One Hand Base/Movement";
        private const string OneHandUp = Root + "/One Hand Up/Movement";
        private const string Peace = Root + "/Base Move";
        private const string Jump = Root + "/One Hand Base/Jump";
        private const string Actions = Root + "/Actions";

        private const string Synty =
            "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine";

        public static void Apply()
        {
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("[IsoRPG] Героя нет."); return; }

            // Бег: три доступные ветки. Четвёртая и пятая (лук, двуручное)
            // лежат в папках с тильдой — Unity их не импортирует вовсе.
            var runs = Clips(
                Synty + "/Locomotion/Run/A_MOD_BL_Run_F_Masc.fbx",
                OneHand + "/Run/Type A/Base/InPlace/OneHand_Base_Run_A_F_InPlace.fbx",
                OneHandUp + "/Run/Type A/Base/InPlace/OneHand_Up_Run_F_InPlace.fbx",
                Peace + "/Run/Base/InPlace/Run_F_InPlace.fbx");

            var idles = Clips(
                Synty + "/Idles/A_MOD_BL_Idle_Standing_Masc.fbx",
                Peace + "/Stand_Idle/Idle/Stand_Idle_A_1.fbx",
                Peace + "/Stand_Idle/Idle/Stand_Idle_A_2.fbx",
                Peace + "/Stand_Idle/Idle/Stand_Idle_A_3.fbx",
                Peace + "/Stand_Idle/Idle/Stand_Idle_A_4.fbx",
                Peace + "/Stand_Idle/Idle/Stand_Idle_B_1.fbx",
                Peace + "/Stand_Idle/Idle/Stand_Idle_B_2.fbx");

            // Боевая стойка: вооружённые покои плюс отдельный раздел
            // Combat Idle — автор нарисовал их специально под бой.
            var combat = Clips(
                OneHand + "/Idle/Idle/OneHand_Base_Stand_Idle_A_1.fbx",
                OneHand + "/Idle/Idle/OneHand_Base_Stand_Idle_A_2.fbx",
                OneHand + "/Idle/Idle/OneHand_Base_Stand_Idle_A_3.fbx",
                OneHand + "/Idle/Idle/OneHand_Base_Stand_Idle_A_4.fbx",
                OneHand + "/Idle/Idle/OneHand_Base_Stand_Idle_B_1.fbx",
                OneHand + "/Idle/Idle/OneHand_Base_Stand_Idle_B_2.fbx",
                OneHand + "/Idle/Idle/OneHand_Base_Stand_Idle_B_3.fbx",
                Actions + "/Combat Idle/Combat_Idle_1.fbx",
                Actions + "/Combat Idle/Combat_Idle_2.fbx",
                Actions + "/Combat Idle/Combat_Idle_3.fbx",
                Actions + "/Combat Idle/Combat_Idle_4.fbx",
                Actions + "/Combat Idle/Combat_Idle_5.fbx",
                Actions + "/Combat Idle/Combat_Idle_6.fbx");

            var landings = Clips(
                Jump + "/InPlace/OneHand_Base_Jump_End_1_InPlace.fbx",
                Jump + "/InPlace/OneHand_Base_Jump_End_2_InPlace.fbx",
                Jump + "/InPlace/OneHand_Base_Jump_End_3_InPlace.fbx",
                Jump + "/InPlace/OneHand_Base_Jump_End_4_InPlace.fbx");

            var tryout = player.GetComponent<IsoRPG.Player.AnimTryout>();
            if (tryout == null) tryout = player.AddComponent<IsoRPG.Player.AnimTryout>();

            // Что подменять — спрашиваем У КОНТРОЛЛЕРА, а не помним по своему
            // списку.
            //
            // Первая версия брала первый клип своего списка и объявляла его
            // «тем, что стоит сейчас». Пока ход был из DoubleL, совпадало
            // случайно; в тот же вечер ход перевели на Synty — и подмена
            // стала уходить в пустоту: контроллер такого клипа больше не
            // содержал. Клавиши нажимались, журнал честно печатал новый
            // вариант, а на экране не менялось ничего. Павлон 04.09.2026:
            // «жму F1, F2, F3 — ничего не меняется».
            //
            // Тот же класс, что список, собранный в двух местах: второе место
            // повторяло первое по памяти. Пока примерка читает контроллер,
            // разойтись они не могут.
            var (curIdle, curWalk, curRun, curSprint) = CurrentStride(player);
            var curLanding = CurrentState(player, "Jump_Land");

            tryout.Setup(runs, idles, combat, landings,
                         curRun, curIdle, curIdle, curLanding);

            Debug.Log($"[IsoRPG] Примерка подменяет: бег «{Name(curRun)}», стойку «{Name(curIdle)}», " +
                      $"приземление «{Name(curLanding)}». Шаг «{Name(curWalk)}», спринт «{Name(curSprint)}» " +
                      "остаются как есть.");

            EditorUtility.SetDirty(tryout);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

            Debug.Log($"[IsoRPG] Примерка анимаций: бег {runs.Length}, стойка {idles.Length}, " +
                      $"боевая стойка {combat.Length}, приземление {landings.Length}. " +
                      "Клавиши в игре: F1, F2, F3, F4.");

            Report("бег", runs);
            Report("стойка", idles);
            Report("боевая стойка", combat);
            Report("приземление", landings);
        }

        private static string Name(AnimationClip clip) => clip != null ? clip.name : "НЕТ";

        /// <summary>
        /// Клипы, которые стоят в дереве хода прямо сейчас: покой, шаг, бег, спринт.
        ///
        /// Дерево у нас двухуровневое — внешнее выбирает фазу, внутренние
        /// аллюр, — поэтому спускаемся рекурсивно и берём клипы по порядку,
        /// в котором их кладёт <see cref="HeroMoveKit"/>.
        /// </summary>
        private static (AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip sprint)
            CurrentStride(GameObject player)
        {
            var clips = new List<AnimationClip>();

            Collect(FindMotion(player, "Locomotion"), clips);

            AnimationClip At(int i) => i < clips.Count ? clips[i] : null;

            return (At(0), At(1), At(2), At(3));
        }

        private static void Collect(Motion motion, List<AnimationClip> into)
        {
            if (motion is AnimationClip clip)
            {
                if (!into.Contains(clip)) into.Add(clip);
                return;
            }

            if (motion is UnityEditor.Animations.BlendTree tree)
                foreach (var child in tree.children) Collect(child.motion, into);
        }

        private static Motion FindMotion(GameObject player, string stateName)
        {
            var animator = player.GetComponentInChildren<Animator>(true);
            var controller = animator != null
                ? animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController
                : null;

            if (controller == null) return null;

            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine == null) continue;

                foreach (var child in layer.stateMachine.states)
                    if (child.state.name == stateName) return child.state.motion;
            }

            return null;
        }

        private static AnimationClip CurrentState(GameObject player, string stateName)
            => FindMotion(player, stateName) as AnimationClip;

        /// <summary>Длительность каждого клипа: по ней видно, есть ли в стойке дыхание.</summary>
        private static void Report(string what, AnimationClip[] list)
        {
            if (list.Length == 0) { Debug.LogWarning("[IsoRPG] " + what + ": ничего не нашлось."); return; }

            var lines = list.Select(c => $"{c.name} {c.length:0.00} с");
            Debug.Log($"[IsoRPG]   {what}: " + string.Join(", ", lines));
        }

        private static AnimationClip[] Clips(params string[] paths)
        {
            var found = new List<AnimationClip>();

            foreach (var path in paths)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                        .OfType<AnimationClip>()
                                        .FirstOrDefault(c => !c.name.StartsWith("__preview"));

                if (clip == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет клипа: " + path);
                    continue;
                }

                found.Add(clip);
            }

            return found.ToArray();
        }
    }
}
