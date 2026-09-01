using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает боссу-кабану контроллер под наши параметры — тот же приём,
    /// что у волка и рядового кабана. У набора (Blink) есть свой готовый
    /// «BoarBossAnimator.controller», но он говорит своими именами
    /// параметров; наш боевой код их не знает, поэтому строим свой поверх
    /// тех же клипов.
    ///
    /// Атак у босса семь штук — берём одну (Attack1), остальные лишние для
    /// одного акцентного врага: усложнять ради разнообразия анимации одного
    /// экземпляра смысла нет.
    /// </summary>
    public static class BoarBossAnimations
    {
        private const string Clips =
            "Assets/Blink/Art/Animals/Stylized/BoarBoss/BoarBoss_Animations";
        private const string Target = "Assets/_Game/Art/Animations/Controllers/AC_BoarBoss.controller";

        private const float WalkAt = 1.0f, RunAt = 3.2f;

        [MenuItem("Tools/IsoRPG/Босс-кабан: собрать контроллер", priority = 41)]
        public static AnimatorController Build()
        {
            var idle = Clip("BoarBoss_Idle.fbx");
            var walk = Clip("BoarBoss_WalkForward.fbx");
            var run = Clip("BoarBoss_RunForward.fbx");
            var attack = Clip("BoarBoss_Attack1.fbx");
            var die = Clip("BoarBoss_Death.fbx");

            if (idle == null || walk == null || run == null || attack == null || die == null)
            {
                Debug.LogError("[IsoRPG] Клипы босса-кабана не нашлись — контроллер не собран.");
                return null;
            }

            // Та же беда, что у рядового кабана: клипы хода несут корневое
            // смещение, зверь уезжал бы от навигационного агента.
            StripRootMotion(walk);
            StripRootMotion(run);

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Target) != null)
                AssetDatabase.DeleteAsset(Target);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(Target);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("AttackSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("StealthKill", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Eating", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            var machine = controller.layers[0].stateMachine;

            var tree = new BlendTree
            {
                name = "Ход",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.children = new[]
            {
                new ChildMotion { motion = idle, threshold = 0f,     timeScale = 1f },
                new ChildMotion { motion = walk, threshold = WalkAt, timeScale = 1f },
                new ChildMotion { motion = run,  threshold = RunAt,  timeScale = 1f },
            };

            var move = machine.AddState("Locomotion");
            move.motion = tree;
            machine.defaultState = move;

            var attackState = machine.AddState("Attack");
            attackState.motion = attack;

            var death = machine.AddState("Death");
            death.motion = die;

            var toAttack = move.AddTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.hasExitTime = false;
            toAttack.duration = 0.05f;

            var fromAttack = attackState.AddTransition(move);
            fromAttack.hasExitTime = true;
            fromAttack.exitTime = 0.9f;
            fromAttack.duration = 0.1f;

            var toDeath = machine.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            toDeath.hasExitTime = false;
            toDeath.duration = 0.1f;
            toDeath.canTransitionToSelf = false;

            // Обратно — когда возродился. Без этого воскресший босс остаётся
            // лежать и бьёт из положения трупа, а его удары идут без
            // анимации: переход в атаку выходит только из «движения», а из
            // «смерти» выхода не было вовсе. Павлон 01.09.2026: «у кабана
            // босса анимация боя пропала, я возродился — и она появилась».
            // У обычных зверей это давно починено в BeastBuilder, сюда
            // правку не донесли: тот же список собран в двух местах.
            var revive = death.AddTransition(move);
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            revive.hasExitTime = false;
            revive.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Контроллер босса-кабана собран: ход (шаг " + WalkAt +
                      "/бег " + RunAt + "), удар Attack1 по триггеру, смерть из любого состояния.");

            return controller;
        }

        private static void StripRootMotion(AnimationClip clip)
        {
            if (clip == null) return;

            var bindings = AnimationUtility.GetCurveBindings(clip);

            string rootPath = null;
            int shallowest = int.MaxValue;

            foreach (var b in bindings)
            {
                if (!b.propertyName.StartsWith("m_LocalPosition")) continue;

                int depth = string.IsNullOrEmpty(b.path) ? 0 : b.path.Split('/').Length;
                if (depth < shallowest) { shallowest = depth; rootPath = b.path; }
            }

            if (rootPath == null) return;

            int removed = 0;

            foreach (var b in bindings)
            {
                if (b.path != rootPath) continue;
                if (!b.propertyName.StartsWith("m_LocalPosition")) continue;

                AnimationUtility.SetEditorCurve(clip, b, null);
                removed++;
            }

            if (removed > 0)
            {
                EditorUtility.SetDirty(clip);
                Debug.Log("[IsoRPG] С клипа «" + clip.name + "» снято кривых смещения корня («" +
                          (rootPath == string.Empty ? "(корень)" : rootPath) + "»): " + removed + ".");
            }
        }

        private static AnimationClip Clip(string file)
        {
            string path = Clips + "/" + file;

            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null) Debug.LogWarning("[IsoRPG] Клип босса-кабана не найден: " + path);

            return clip;
        }
    }
}
