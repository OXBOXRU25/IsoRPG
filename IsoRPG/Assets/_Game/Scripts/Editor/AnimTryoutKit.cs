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

        public static void Apply()
        {
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("[IsoRPG] Героя нет."); return; }

            // Бег: три доступные ветки. Четвёртая и пятая (лук, двуручное)
            // лежат в папках с тильдой — Unity их не импортирует вовсе.
            var runs = Clips(
                OneHand + "/Run/Type A/Base/InPlace/OneHand_Base_Run_A_F_InPlace.fbx",
                OneHandUp + "/Run/Type A/Base/InPlace/OneHand_Up_Run_F_InPlace.fbx",
                Peace + "/Run/Base/InPlace/Run_F_InPlace.fbx");

            var idles = Clips(
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

            tryout.Setup(runs, idles, combat, landings,
                         runs.FirstOrDefault(), idles.FirstOrDefault(),
                         combat.FirstOrDefault(), landings.FirstOrDefault());

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
