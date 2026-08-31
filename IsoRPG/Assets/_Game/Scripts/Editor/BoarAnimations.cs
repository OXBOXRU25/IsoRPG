using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает кабану контроллер анимаций под НАШИ параметры — тот же
    /// приём, что и у волка (<see cref="WolfAnimations"/>): свои клипы, но
    /// наши имена параметров и наша логика переходов.
    ///
    /// Набор Malbers Forest Pack — не Synty: клипы движения несут
    /// КОРНЕВОЕ смещение (собственный контроллер набора его читает сам). У
    /// волка эта проблема была решена выбором версии клипа «без корня»;
    /// здесь такой версии нет вовсе, поэтому корень запекается В ПОЗУ
    /// прямо в настройках импорта FBX — <see cref="BakeRootIntoPose"/>.
    /// Без этого кабан уезжал бы от своего навигационного агента вперёд
    /// при каждом шаге.
    /// </summary>
    public static class BoarAnimations
    {
        private const string Clips = "Assets/Malbers Animations/Animals Packs/01 Forest Pack/Boar/Anims";
        private const string Target = "Assets/_Game/Art/Animations/Controllers/AC_Boar.controller";

        private const float WalkAt = 1.0f, RunAt = 3.0f;

        [MenuItem("Tools/IsoRPG/Кабан: собрать контроллер", priority = 39)]
        public static AnimatorController Build()
        {
            var idle = Clip("Boar Idle.FBX");
            var attack = Clip("Boar Attack.FBX");
            var die = Clip("Boar Death.FBX");

            // Ход — не прямые клипы FBX, а их автономные клоны без корневого
            // смещения (см. StripRootMotion). Первая попытка правила кривые
            // ПРЯМО на суб-ассете FBX — кабан продолжал уезжать вперёд
            // рывками и откатываться назад циклом ровно как раньше. Правка
            // суб-ассета модели ненадёжна: Unity вправе пересобрать клип из
            // источника при следующем импорте и тихо стереть правку. Клон —
            // обычный файл в НАШЕЙ папке, независимый от FBX.
            var walk = StripRootMotion(Clip("Boar Walk.FBX"), "Boar_Walk_NoRoot");
            var run = StripRootMotion(Clip("Boar Run.FBX"), "Boar_Run_NoRoot");

            if (idle == null || walk == null || run == null || attack == null || die == null)
            {
                Debug.LogError("[IsoRPG] Клипы кабана не нашлись — контроллер не собран.");
                return null;
            }

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

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Контроллер кабана собран: ход (шаг " + WalkAt +
                      "/бег " + RunAt + "), удар по триггеру, смерть из любого состояния.");

            return controller;
        }

        /// <summary>
        /// Клонировать клип в отдельный файл БЕЗ кривых смещения корня.
        ///
        /// Путь "" (Synty, волк) тут не годится — Malbers строит риг иначе:
        /// корень движения — не тот трансформ, на котором висит аниматор, а
        /// первый сустав скелета ПОД ним, с собственным именем («CG» у
        /// кабана). Вместо угадывания имени берём путь с МЕНЬШЕЙ глубиной
        /// среди костей, что вообще двигают позицию, — это и есть корень,
        /// как его ни назови.
        ///
        /// Клип не правится на месте: это суб-ассет FBX, и правка кривых
        /// прямо на нём уже один раз молча пропадала (Unity вправе
        /// пересобрать клип из источника при следующем импорте). Копия —
        /// обычный файл, ничьим импортом не перезаписывается.
        /// </summary>
        private static AnimationClip StripRootMotion(AnimationClip source, string cloneName)
        {
            if (source == null) return null;

            string path = "Assets/_Game/Art/Animations/" + cloneName + ".anim";
            var clone = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clone == null)
            {
                clone = Object.Instantiate(source);
                clone.name = cloneName;
                AssetDatabase.CreateAsset(clone, path);
            }
            else
            {
                clone.ClearCurves();
                EditorUtility.CopySerialized(source, clone);
                clone.name = cloneName;
            }

            var bindings = AnimationUtility.GetCurveBindings(source);

            string rootPath = null;
            int shallowest = int.MaxValue;

            foreach (var b in bindings)
            {
                if (!b.propertyName.StartsWith("m_LocalPosition")) continue;

                int depth = string.IsNullOrEmpty(b.path) ? 0 : b.path.Split('/').Length;
                if (depth < shallowest) { shallowest = depth; rootPath = b.path; }
            }

            int removed = 0;

            if (rootPath != null)
            {
                foreach (var b in bindings)
                {
                    if (b.path != rootPath) continue;
                    if (!b.propertyName.StartsWith("m_LocalPosition")) continue;

                    AnimationUtility.SetEditorCurve(clone, b, null);
                    removed++;
                }
            }

            EditorUtility.SetDirty(clone);
            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Клон «" + cloneName + "» сохранён файлом, снято кривых смещения корня («" +
                      (string.IsNullOrEmpty(rootPath) ? "(корень)" : rootPath) + "»): " + removed + ".");

            return clone;
        }

        private static AnimationClip Clip(string file)
        {
            string path = Clips + "/" + file;

            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null) Debug.LogWarning("[IsoRPG] Клип кабана не найден: " + path);

            return clip;
        }
    }
}
