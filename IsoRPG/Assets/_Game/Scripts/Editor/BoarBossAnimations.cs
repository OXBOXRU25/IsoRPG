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
    /// **Берём ВСЕ 36 клипов набора.** Раньше здесь стояло два — ход и один
    /// удар, — и в комментарии было написано, что остальные «лишние для
    /// одного акцентного врага». Это было моё решение, принятое молча:
    /// 02.09.2026 Павлон сам нашёл библиотеку и сказал прямо — «ты
    /// проигнорировал богатую библиотеку анимаций, это ппц». Он прав: набор
    /// покупался ради этого, и решать, что из него нужно, не мне.
    ///
    /// Что появилось:
    ///   - семь ударов по кругу вместо одного (`AttackVariant`);
    ///   - боевая стойка `IdleCombat` — от неё и шло «босс стоит замерев»;
    ///   - вздрагивание с четырёх сторон по `HitDir`;
    ///   - оглушение `StunnedLoop`, усиление `Buff`, прыжок;
    ///   - движение двумерным деревом: вперёд, назад, вбок, с наклонами;
    ///   - кружение вокруг цели `CirclingLeft/Right`;
    ///   - покой вне боя: еда, сидение, сон.
    /// </summary>
    public static class BoarBossAnimations
    {
        private const string Clips =
            "Assets/Blink/Art/Animals/Stylized/BoarBoss/BoarBoss_Animations";
        private const string Target = "Assets/_Game/Art/Animations/Controllers/AC_BoarBoss.controller";

        /// <summary>Сколько ударов в серии. У набора их семь — берём все.</summary>
        public const int AttackVariants = 7;

        private const float WalkAt = 1.0f, RunAt = 3.2f;

        [MenuItem("Tools/IsoRPG/Босс-кабан: собрать контроллер", priority = 41)]
        public static AnimatorController Build()
        {
            // --- клипы ---
            var idle = Clip("BoarBoss_Idle.fbx");
            var idleCombat = Clip("BoarBoss_IdleCombat.fbx");
            var die = Clip("BoarBoss_Death.fbx");

            if (idle == null || die == null)
            {
                Debug.LogError("[IsoRPG] Клипы босса-кабана не нашлись — контроллер не собран.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Target) != null)
                AssetDatabase.DeleteAsset(Target);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(Target);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Strafe", AnimatorControllerParameterType.Float);
            controller.AddParameter("AttackSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitDir", AnimatorControllerParameterType.Int);
            controller.AddParameter("Stunned", AnimatorControllerParameterType.Bool);
            controller.AddParameter("CastBuff", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("InCombat", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Rest", AnimatorControllerParameterType.Int);
            controller.AddParameter("StealthKill", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Eating", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            var machine = controller.layers[0].stateMachine;

            // --- ход: два дерева, мирное и боевое ---
            //
            // Различаются они только тем, что стоит в центре: спокойная поза
            // или боевая стойка. Всё остальное — те же клипы шага и бега.
            var peace = machine.AddState("Locomotion");
            peace.motion = MoveTree(controller, "Ход", idle);
            machine.defaultState = peace;

            var combat = machine.AddState("LocomotionCombat");
            combat.motion = MoveTree(controller, "Ход в бою", idleCombat ?? idle);

            Switch(peace, combat, "InCombat", true);
            Switch(combat, peace, "InCombat", false);

            // --- семь ударов ---
            //
            // Вход из любого состояния по триггеру и номеру: так удар не
            // теряется, если зверь в этот миг шагал или вздрагивал.
            for (int i = 1; i <= AttackVariants; i++)
            {
                var clip = Clip($"BoarBoss_Attack{i}.fbx");
                if (clip == null) continue;

                var state = machine.AddState("Attack_" + i);
                state.motion = clip;
                state.speedParameterActive = true;
                state.speedParameter = "AttackSpeed";

                var any = machine.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                any.AddCondition(AnimatorConditionMode.Equals, i, "AttackVariant");
                any.duration = 0.05f;
                any.canTransitionToSelf = false;

                Back(state, 0.9f);
            }

            // --- вздрагивание с четырёх сторон ---
            //
            // Сторона приходит числом в `HitDir`: 0 спереди, 1 сзади,
            // 2 слева, 3 справа. Выход короткий — 0.40 с, как у всех
            // (см. задание flinch-tune и разбор в памяти проекта).
            AddHit(machine, "GetHit_Front", Clip("BoarBoss_GetHitFromFront.fbx"), 0);
            AddHit(machine, "GetHit_Back", Clip("BoarBoss_GetHitFromBack.fbx"), 1);
            AddHit(machine, "GetHit_Left", Clip("BoarBoss_GetHitLeft.fbx"), 2);
            AddHit(machine, "GetHit_Right", Clip("BoarBoss_GetHitRight.fbx"), 3);

            // --- оглушение: держится, пока стоит флаг ---
            var stun = machine.AddState("Stunned");
            stun.motion = Clip("BoarBoss_StunnedLoop.fbx");

            var toStun = machine.AddAnyStateTransition(stun);
            toStun.AddCondition(AnimatorConditionMode.If, 0f, "Stunned");
            toStun.duration = 0.1f;
            toStun.canTransitionToSelf = false;

            var fromStun = stun.AddTransition(peace);
            fromStun.AddCondition(AnimatorConditionMode.IfNot, 0f, "Stunned");
            fromStun.hasExitTime = false;
            fromStun.duration = 0.15f;

            // --- усиление и прыжок ---
            OneShot(machine, "Buff", Clip("BoarBoss_Buff.fbx"), "CastBuff", 0.85f);
            OneShot(machine, "Jump", Clip("BoarBoss_Jump.fbx"), "Jump", 0.85f);

            // --- кружение вокруг цели ---
            //
            // Боец в кольце боя не стоит столбом, а переминается по дуге.
            // Сторона — знаком `Strafe`, вход только из боевого хода.
            AddCircle(combat, machine, "Circle_Left", Clip("BoarBoss_CirclingLeft.fbx"), -0.35f);
            AddCircle(combat, machine, "Circle_Right", Clip("BoarBoss_CirclingRight.fbx"), 0.35f);

            // --- покой вне боя ---
            //
            // `Rest`: 1 — ест, 2 — сидит, 3 — спит. Возврат по нулю, поэтому
            // зверь встаёт сразу, как его потревожили.
            AddRest(peace, machine, "Rest_1", Clip("BoarBoss_Eat.fbx"), 1);
            AddRest(peace, machine, "Rest_2", Clip("BoarBoss_Sit.fbx"), 2);
            AddRest(peace, machine, "Rest_3", Clip("BoarBoss_Sleep.fbx"), 3);

            // --- смерть и возвращение ---
            var death = machine.AddState("Death");
            death.motion = die;

            var toDeath = machine.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            toDeath.hasExitTime = false;
            toDeath.duration = 0.1f;
            toDeath.canTransitionToSelf = false;

            // Обратно — когда возродился. Без этого воскресший босс остаётся
            // лежать и бьёт из положения трупа.
            var revive = death.AddTransition(peace);
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            revive.hasExitTime = false;
            revive.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            int states = machine.states.Length;

            Debug.Log($"[IsoRPG] Контроллер босса-кабана собран: состояний {states}, " +
                      $"ударов {AttackVariants}, вздрагиваний 4, движение двумерным деревом, " +
                      "покой (еда, сидение, сон), кружение, оглушение, усиление, прыжок.");

            return controller;
        }

        /// <summary>
        /// Дерево движения: вперёд-назад по `Speed`, вбок по `Strafe`.
        ///
        /// Двумерное, потому что клипы у набора направленные: есть не только
        /// бег вперёд, но и назад, вбок и с наклонами. Плоское одномерное
        /// дерево выбросило бы девять клипов из двенадцати.
        /// </summary>
        private static BlendTree MoveTree(AnimatorController controller, string name, Motion centre)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "Strafe",
                blendParameterY = "Speed",
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            var children = new System.Collections.Generic.List<ChildMotion>
            {
                new ChildMotion { motion = centre, position = Vector2.zero, timeScale = 1f },
            };

            Add(children, "BoarBoss_WalkForward.fbx", 0f, WalkAt);
            Add(children, "BoarBoss_WalkForwardLeft.fbx", -WalkAt, WalkAt);
            Add(children, "BoarBoss_WalkForwardRight.fbx", WalkAt, WalkAt);

            Add(children, "BoarBoss_RunForward.fbx", 0f, RunAt);
            Add(children, "BoarBoss_RunForwardLeft.fbx", -RunAt * 0.5f, RunAt);
            Add(children, "BoarBoss_RunForwardRight.fbx", RunAt * 0.5f, RunAt);

            Add(children, "BoarBoss_WalkBackward.fbx", 0f, -WalkAt);
            Add(children, "BoarBoss_WalkBackwardLeft.fbx", -WalkAt, -WalkAt);
            Add(children, "BoarBoss_WalkBackwardRight.fbx", WalkAt, -WalkAt);

            Add(children, "BoarBoss_RunBackward.fbx", 0f, -RunAt);
            Add(children, "BoarBoss_RunBackwardLeft.fbx", -RunAt * 0.5f, -RunAt);
            Add(children, "BoarBoss_RunBackwardRight.fbx", RunAt * 0.5f, -RunAt);

            Add(children, "BoarBoss_StrafeLeft.fbx", -WalkAt, 0f);
            Add(children, "BoarBoss_StrafeRight.fbx", WalkAt, 0f);

            tree.children = children.ToArray();

            return tree;
        }

        private static void Add(System.Collections.Generic.List<ChildMotion> list,
                                string file, float x, float y)
        {
            var clip = Clip(file);
            if (clip == null) return;

            // Клипы хода несут корневое смещение: зверь уезжал бы от своего
            // навигационного агента, а тот тянул бы его обратно.
            StripRootMotion(clip);

            list.Add(new ChildMotion { motion = clip, position = new Vector2(x, y), timeScale = 1f });
        }

        /// <summary>Вздрагивание с одной стороны: вход по триггеру и номеру стороны.</summary>
        private static void AddHit(AnimatorStateMachine machine, string name,
                                   AnimationClip clip, int direction)
        {
            if (clip == null) return;

            var state = machine.AddState(name);
            state.motion = clip;

            var any = machine.AddAnyStateTransition(state);
            any.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            any.AddCondition(AnimatorConditionMode.Equals, direction, "HitDir");
            any.duration = 0.06f;
            any.canTransitionToSelf = false;

            // Доля от клипа, дающая те же 0.40 с, что у всех остальных.
            float share = clip.length > 0.45f ? 0.40f / clip.length : 0.9f;
            Back(state, Mathf.Clamp(share, 0.1f, 0.9f));
        }

        /// <summary>Разовое действие по триггеру: усиление, прыжок.</summary>
        private static void OneShot(AnimatorStateMachine machine, string name,
                                    AnimationClip clip, string trigger, float exitAt)
        {
            if (clip == null) return;

            var state = machine.AddState(name);
            state.motion = clip;

            var any = machine.AddAnyStateTransition(state);
            any.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            any.duration = 0.08f;
            any.canTransitionToSelf = false;

            Back(state, exitAt);
        }

        /// <summary>Кружение по дуге: держится, пока боец смещается вбок и стоит на месте.</summary>
        private static void AddCircle(AnimatorState from, AnimatorStateMachine machine,
                                      string name, AnimationClip clip, float threshold)
        {
            if (clip == null) return;

            StripRootMotion(clip);

            var state = machine.AddState(name);
            state.motion = clip;

            var into = from.AddTransition(state);
            into.hasExitTime = false;
            into.duration = 0.12f;
            into.AddCondition(threshold < 0f ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater,
                              threshold, "Strafe");
            into.AddCondition(AnimatorConditionMode.Less, 0.6f, "Speed");

            var back = state.AddTransition(from);
            back.hasExitTime = false;
            back.duration = 0.12f;
            back.AddCondition(threshold < 0f ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less,
                              threshold * 0.5f, "Strafe");
        }

        /// <summary>Покой вне боя: держится, пока `Rest` равен своему номеру.</summary>
        private static void AddRest(AnimatorState from, AnimatorStateMachine machine,
                                    string name, AnimationClip clip, int kind)
        {
            if (clip == null) return;

            var state = machine.AddState(name);
            state.motion = clip;

            var into = from.AddTransition(state);
            into.hasExitTime = false;
            into.duration = 0.2f;
            into.AddCondition(AnimatorConditionMode.Equals, kind, "Rest");

            var back = state.AddTransition(from);
            back.hasExitTime = false;
            back.duration = 0.2f;
            back.AddCondition(AnimatorConditionMode.NotEqual, kind, "Rest");
        }

        /// <summary>Возврат в ход по концу клипа — через выход стейт-машины.</summary>
        private static void Back(AnimatorState state, float exitAt)
        {
            var back = state.AddExitTransition();
            back.hasExitTime = true;
            back.exitTime = exitAt;
            back.duration = 0.12f;
        }

        /// <summary>Переключение между мирным и боевым ходом по флагу.</summary>
        private static void Switch(AnimatorState from, AnimatorState to, string flag, bool value)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.2f;
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, flag);
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

            if (removed > 0) EditorUtility.SetDirty(clip);
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
