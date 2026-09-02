using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Переводит ход и прыжок героя на новый набор анимаций.
    ///
    /// Первый шаг перехода, и намеренно осторожный: **машину состояний не
    /// трогаем, меняем только клипы**. Дерево хода как было одномерным, так и
    /// осталось; прыжок как был трёхфазным, так и остался — у автора набора
    /// ровно та же схема (`Jump_Start` → `Jump_Air_Loop` → `Jump_End`).
    /// Значит ломаться нечему, а разница в пластике видна сразу.
    ///
    /// Инерция — то, ради чего набор и брали, — сюда НЕ входит. Клипы
    /// переходов между направлениями (`Run_F_L90_A_To_F_R90_B` и ещё 53 таких)
    /// требуют другого дерева и новых параметров, которые герою пока никто не
    /// считает. Это отдельный заход: смешивать подмену клипов с переписыванием
    /// машины значит потом гадать, что из двух сломалось.
    ///
    /// Играем версии `_InPlace` — без корневого движения: героя ведёт капсула,
    /// и клип, который везёт сам, уехал бы от неё. А пороги дерева меряем по
    /// ОБЫЧНЫМ версиям: там корневое движение есть, и оно говорит, с какой
    /// скоростью автор нарисовал этот шаг.
    /// </summary>
    public static class HeroMoveKit
    {
        private const string ControllerPath =
            "Assets/_Game/Art/Animations/Controllers/AC_Hero_Sidekick.controller";

        private const string Move = "Assets/DoubleL/FBX_Animations/Base Move";
        private const string Jump = "Assets/DoubleL/FBX_Animations/One Hand Base/Jump";

        [MenuItem("Tools/IsoRPG/Герой: ход и прыжок из нового набора", priority = 43)]
        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                Debug.LogError("[IsoRPG] Нет контроллера героя: " + ControllerPath);
                return;
            }

            LoopMovement();

            var idle = Clip(Move + "/Stand_Idle/Idle/Stand_Idle_A_1.fbx");
            var walk = Clip(Move + "/Walk/Base/InPlace/Walk_F_InPlace.fbx");
            var run = Clip(Move + "/Run/Base/InPlace/Run_F_InPlace.fbx");
            var sprint = Clip(Move + "/Sprint/Base/InPlace/Sprint_F_InPlace.fbx");

            if (idle == null || walk == null || run == null)
            {
                Debug.LogError("[IsoRPG] Клипы хода не нашлись — ход не переведён.");
                return;
            }

            // Скорость каждого аллюра — из клипа С корневым движением.
            float walkAt = Speed(Move + "/Walk/Base/Walk_F.fbx", 1.8f);
            float runAt = Speed(Move + "/Run/Base/Run_F.fbx", 4.0f);
            float sprintAt = Speed(Move + "/Sprint/Base/Sprint_F.fbx", 6.5f);

            var tree = new BlendTree
            {
                name = "Ход",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            var children = new System.Collections.Generic.List<ChildMotion>
            {
                new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = walk, threshold = walkAt, timeScale = 1f },
                new ChildMotion { motion = run, threshold = runAt, timeScale = 1f },
            };

            // Спринт — отдельная ступень, а не растянутый бег: у героя есть
            // способность на +70% скорости, и на растянутом беге она читалась
            // бы как ускоренная перемотка.
            if (sprint != null && sprintAt > runAt + 0.2f)
                children.Add(new ChildMotion { motion = sprint, threshold = sprintAt, timeScale = 1f });

            tree.children = children.ToArray();

            // --- подмена в состояниях ------------------------------------
            var jumpStart = Clip(Jump + "/InPlace/OneHand_Base_Jump_Start_InPlace.fbx")
                            ?? Clip(Jump + "/OneHand_Base_Jump_Start.fbx");

            var jumpAir = Clip(Jump + "/InPlace/OneHand_Base_Jump_Air_Loop_InPlace.fbx")
                          ?? Clip(Jump + "/OneHand_Base_Jump_Air_Loop.fbx");

            var jumpLand = Clip(Jump + "/InPlace/OneHand_Base_Jump_End_1_InPlace.fbx")
                           ?? Clip(Jump + "/OneHand_Base_Jump_End_1.fbx");

            int moved = 0, jumped = 0;

            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine == null) continue;

                foreach (var child in layer.stateMachine.states)
                {
                    var state = child.state;

                    switch (state.name)
                    {
                        case "Locomotion":
                            state.motion = tree;
                            moved++;
                            break;

                        case "Jump_Start":
                            if (jumpStart != null) { state.motion = jumpStart; jumped++; }
                            break;

                        case "Jump_Air":
                            if (jumpAir != null) { state.motion = jumpAir; jumped++; }
                            break;

                        case "Jump_Land":
                            if (jumpLand != null) { state.motion = jumpLand; jumped++; }
                            break;
                    }
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[IsoRPG] Ход героя переведён на новый набор: деревьев {moved}, " +
                      $"фаз прыжка {jumped} из 3.\n" +
                      $"  Пороги сняты с клипов: шаг {walkAt:0.00}, бег {runAt:0.00}, " +
                      $"спринт {(sprint != null ? sprintAt.ToString("0.00") : "нет")} м/с.");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Зациклить ход и зависание в воздухе.
        ///
        /// Клипы приезжают из набора незацикленными — та же ловушка, что была
        /// со стойками НПС: шаг отыгрывает один раз и замирает.
        /// </summary>
        private static void LoopMovement()
        {
            (string Path, bool Loop)[] files =
            {
                (Move + "/Walk/Base/InPlace/Walk_F_InPlace.fbx", true),
                (Move + "/Run/Base/InPlace/Run_F_InPlace.fbx", true),
                (Move + "/Sprint/Base/InPlace/Sprint_F_InPlace.fbx", true),
                (Move + "/Stand_Idle/Idle/Stand_Idle_A_1.fbx", true),
                (Jump + "/InPlace/OneHand_Base_Jump_Air_Loop_InPlace.fbx", true),
                (Jump + "/OneHand_Base_Jump_Air_Loop.fbx", true),
            };

            foreach (var (path, loop) in files)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                var takes = importer.clipAnimations;
                if (takes == null || takes.Length == 0) takes = importer.defaultClipAnimations;
                if (takes.Length == 0) continue;

                bool changed = false;

                for (int i = 0; i < takes.Length; i++)
                {
                    if (takes[i].loopTime == loop) continue;

                    takes[i].loopTime = loop;
                    changed = true;
                }

                if (!changed) continue;

                importer.clipAnimations = takes;
                importer.SaveAndReimport();

                Debug.Log("[IsoRPG] Зациклен клип хода: " + System.IO.Path.GetFileName(path));
            }
        }

        private static float Speed(string path, float fallback)
        {
            float measured = ClipSpeed.Measure(Clip(path));

            if (measured > 0.05f) return measured;

            Debug.LogWarning("[IsoRPG] Скорость не померилась у " + path + " — порог запасной.");
            return fallback;
        }

        private static AnimationClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null) Debug.LogWarning("[IsoRPG] Клип не найден: " + path);

            return clip;
        }
    }
}
