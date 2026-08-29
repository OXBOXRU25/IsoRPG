using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает волку контроллер анимаций под НАШИ параметры.
    ///
    /// У набора есть свой `Polygonal Wolf.controller`, но он знает свои
    /// состояния (`Bite`, `Howl`, `Pound`) и переключается сам по себе. Наш
    /// боевой код говорит с аниматором иначе: пишет `Speed`, дёргает
    /// триггер `Attack`, поднимает `Dead`. Чужой контроллер этих имён не
    /// знает — волк стоял бы столбом при живом ИИ, и было бы непонятно,
    /// сломан мозг или анимации.
    ///
    /// Поэтому строим свой: те же клипы набора, но наши имена и наша
    /// логика переходов. Ровно то же самое я сделал герою.
    ///
    /// Клипы берём в версии <b>WO Root</b> — без корневого движения. С
    /// корневым анимация тянет волка сама, и он уезжает от навигационного
    /// агента: агент считает, что зверь в одном месте, а видим мы его в
    /// другом.
    /// </summary>
    public static class WolfAnimations
    {
        private const string Clips = "Assets/Polygonal Wolf/FBX";
        private const string Target = "Assets/_Game/Art/Animations/Controllers/AC_Wolf.controller";

        /// <summary>Пороги дерева смешивания, метры в секунду.</summary>
        private const float WalkAt = 1.2f, RunAt = 3.4f;

        [MenuItem("Tools/IsoRPG/Волк: собрать контроллер", priority = 37)]
        public static AnimatorController Build()
        {
            var idle = Clip("Polygonal Wolf@Idle.FBX");
            var walk = Clip("Polygonal Wolf@Walk Forward WO Root.FBX");
            var run = Clip("Polygonal Wolf@Run Forward WO Root.FBX");
            var bite = Clip("Polygonal Wolf@Bite Attack.FBX");
            var die = Clip("Polygonal Wolf@Die.FBX");

            if (idle == null || walk == null || run == null || bite == null || die == null)
            {
                Debug.LogError("[IsoRPG] Клипы волка не нашлись — контроллер не собран.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Target) != null)
                AssetDatabase.DeleteAsset(Target);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(Target);

            // Все параметры, которые дёргает наш водитель анимаций. Лишние
            // не мешают, а недостающие сыпали бы предупреждениями в каждом
            // кадре и прятали настоящие ошибки.
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

            var attack = machine.AddState("Attack");
            attack.motion = bite;

            var death = machine.AddState("Death");
            death.motion = die;

            // Удар: по триггеру и обратно сам, когда клип отыграл.
            var toAttack = move.AddTransition(attack);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.hasExitTime = false;
            toAttack.duration = 0.05f;

            var fromAttack = attack.AddTransition(move);
            fromAttack.hasExitTime = true;
            fromAttack.exitTime = 0.9f;
            fromAttack.duration = 0.1f;

            // Смерть — из любого состояния и без возврата.
            var toDeath = machine.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            toDeath.hasExitTime = false;
            toDeath.duration = 0.1f;
            toDeath.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Контроллер волка собран: ход (стойка/шаг " + WalkAt +
                      "/бег " + RunAt + "), укус по триггеру, смерть из любого состояния.");

            return controller;
        }

        private static AnimationClip Clip(string file)
        {
            string path = Clips + "/" + file;

            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null) Debug.LogWarning("[IsoRPG] Клип волка не найден: " + path);

            return clip;
        }
    }
}
