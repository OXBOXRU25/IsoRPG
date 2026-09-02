using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает грибу-монстру контроллер под наши параметры.
    ///
    /// У набора (InfinityPBR) свой контроллер, но он говорит своими именами
    /// параметров, которых наш боевой код не знает. Строим свой поверх тех же
    /// клипов — тот же приём, что у волка, кабана и босса-кабана.
    ///
    /// Клипы лежат ВНУТРИ `Mushroom_LP.fbx` — двадцать штук, включая три
    /// статичные позы обычного гриба. Это и есть главная находка набора:
    /// зверь прикидывается декорацией, пока игрок не подойдёт. Павлон
    /// 02.09.2026 просил босса на поляне — засада подходит ему лучше, чем
    /// зверь, который бежит навстречу через полкарты.
    ///
    /// Что используем:
    ///   - `MushStatic01` — поза гриба, пока игрок далеко (флаг `Asleep`);
    ///   - `MushIdle` и `MushIdleBreak` — покой, когда проснулся;
    ///   - `MushWalk` и `MushWalkBack` — ход вперёд и назад по `Speed`;
    ///   - `MushAttack01..03` — три удара по `AttackVariant`;
    ///   - `MushAttack04start/loop/end` — долгий удар с зарядкой (`CastBuff`);
    ///   - `MushBlock01..03` — блок по `Blocking`;
    ///   - `MushHit` — вздрагивание, `MushDeath` — смерть.
    /// </summary>
    public static class MushroomAnimations
    {
        private const string Model =
            "Assets/InfinityPBR/_InfinityPBR - Mushroom Monster/Models/Mushroom_LP.fbx";
        private const string Target = "Assets/_Game/Art/Animations/Controllers/AC_Mushroom.controller";

        /// <summary>Три обычных удара. Долгий — отдельно, у него три фазы.</summary>
        public const int AttackVariants = 3;

        private const float WalkAt = 1.0f;

        [MenuItem("Tools/IsoRPG/Гриб: собрать контроллер", priority = 45)]
        public static AnimatorController Build()
        {
            var idle = Clip("MushIdle");
            var statue = Clip("MushStatic01");
            var walk = Clip("MushWalk");
            var back = Clip("MushWalkBack");
            var die = Clip("MushDeath");

            if (idle == null || die == null)
            {
                Debug.LogError("[IsoRPG] Клипы гриба не нашлись — контроллер не собран. " +
                               "Проверь, что набор InfinityPBR разложен и клипы нарезаны.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Target) != null)
                AssetDatabase.DeleteAsset(Target);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(Target);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("AttackSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("CastBuff", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Blocking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("BlockVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("Asleep", AnimatorControllerParameterType.Bool);
            controller.AddParameter("StaticVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("DeathVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("StealthKill", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Eating", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            var machine = controller.layers[0].stateMachine;

            // --- ход: назад, покой, вперёд ---
            var tree = new BlendTree
            {
                name = "Ход",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            var children = new System.Collections.Generic.List<ChildMotion>();

            if (back != null)
            {
                StripRootMotion(back);
                children.Add(new ChildMotion { motion = back, threshold = -WalkAt, timeScale = 1f });
            }

            children.Add(new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f });

            if (walk != null)
            {
                StripRootMotion(walk);
                children.Add(new ChildMotion { motion = walk, threshold = WalkAt, timeScale = 1f });

                // Бега у гриба нет вовсе — он и не должен бегать. На высокой
                // скорости ускоряем шаг, иначе он поедет по земле, перебирая
                // ногами вдвое медленнее движения.
                children.Add(new ChildMotion { motion = walk, threshold = WalkAt * 3f, timeScale = 2f });
            }

            tree.children = children.ToArray();

            var move = machine.AddState("Locomotion");
            move.motion = tree;
            machine.defaultState = move;

            // --- засада: стоит обычным грибом, пока не разбудили ---
            //
            // Поз три, и выбор случайный по `StaticVariant`: два гриба рядом
            // в одинаковой позе читались бы как копии одной модели, а порознь
            // — как заросли. Пока гриб один, но правило пусть будет сразу.
            if (statue != null)
            {
                for (int i = 1; i <= 3; i++)
                {
                    var pose = Clip($"MushStatic0{i}") ?? statue;

                    var sleeping = machine.AddState("Statue_" + i);
                    sleeping.motion = pose;

                    var toStatue = machine.AddAnyStateTransition(sleeping);
                    toStatue.AddCondition(AnimatorConditionMode.If, 0f, "Asleep");
                    toStatue.AddCondition(AnimatorConditionMode.Equals, i, "StaticVariant");
                    toStatue.duration = 0.25f;
                    toStatue.canTransitionToSelf = false;

                    var wake = sleeping.AddTransition(move);
                    wake.AddCondition(AnimatorConditionMode.IfNot, 0f, "Asleep");
                    wake.hasExitTime = false;

                    // Просыпается не мгновенно: полсекунды на то, чтобы игрок
                    // успел понять, что декорация ожила.
                    wake.duration = 0.5f;

                    if (i == 1) machine.defaultState = sleeping;
                }
            }

            // --- скучающая вставка ---
            //
            // Гриб, простоявший в покое достаточно долго, разминается. Вход
            // по концу цикла хода: пока он идёт или дерётся, до этого перехода
            // очередь не доходит.
            var idleBreak = Clip("MushIdleBreak");

            if (idleBreak != null)
            {
                var state = machine.AddState("IdleBreak");
                state.motion = idleBreak;

                var into = move.AddTransition(state);
                into.hasExitTime = true;
                into.exitTime = 6f;          // шесть проходов покоя
                into.duration = 0.3f;
                into.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

                Back(state, 0.9f);
            }

            // --- три удара ---
            for (int i = 1; i <= AttackVariants; i++)
            {
                var clip = Clip($"MushAttack0{i}");
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

            // --- долгий удар из трёх фаз ---
            //
            // Зарядка переходит в удержание, удержание — в завершение. Такой
            // приём в бою читается как «сейчас будет больно»: у игрока есть
            // время отойти, и это единственная механика набора, которую нам
            // не пришлось придумывать.
            var start = Clip("MushAttack04start");
            var loop = Clip("MushAttack04loop");
            var end = Clip("MushAttack04end");

            if (start != null && loop != null && end != null)
            {
                var charge = machine.AddState("Charge_Start");
                charge.motion = start;

                var hold = machine.AddState("Charge_Hold");
                hold.motion = loop;

                var release = machine.AddState("Charge_End");
                release.motion = end;

                var any = machine.AddAnyStateTransition(charge);
                any.AddCondition(AnimatorConditionMode.If, 0f, "CastBuff");
                any.duration = 0.06f;
                any.canTransitionToSelf = false;

                var toHold = charge.AddTransition(hold);
                toHold.hasExitTime = true;
                toHold.exitTime = 0.9f;
                toHold.duration = 0.1f;

                // Держит два прохода удержания и бьёт.
                var toRelease = hold.AddTransition(release);
                toRelease.hasExitTime = true;
                toRelease.exitTime = 2f;
                toRelease.duration = 0.1f;

                Back(release, 0.9f);
            }

            // --- блок, три варианта ---
            //
            // Выбор по `BlockVariant`: блок держится долго, и одна и та же
            // поза при каждом ударе превращает защиту в стоп-кадр.
            for (int i = 1; i <= 3; i++)
            {
                var block = Clip($"MushBlock0{i}");
                if (block == null) continue;

                var state = machine.AddState("Block_" + i);
                state.motion = block;

                var into = machine.AddAnyStateTransition(state);
                into.AddCondition(AnimatorConditionMode.If, 0f, "Blocking");
                into.AddCondition(AnimatorConditionMode.Equals, i, "BlockVariant");
                into.duration = 0.1f;
                into.canTransitionToSelf = false;

                var outOf = state.AddTransition(move);
                outOf.AddCondition(AnimatorConditionMode.IfNot, 0f, "Blocking");
                outOf.hasExitTime = false;
                outOf.duration = 0.15f;
            }

            // --- вздрагивание ---
            var hit = Clip("MushHit");

            if (hit != null)
            {
                var state = machine.AddState("GetHit");
                state.motion = hit;

                var any = machine.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                any.duration = 0.06f;
                any.canTransitionToSelf = false;

                // Те же 0.40 с, что у всех остальных зверей.
                float share = hit.length > 0.45f ? 0.40f / hit.length : 0.9f;
                Back(state, Mathf.Clamp(share, 0.1f, 0.9f));
            }

            // --- смерть: обычная и расплющивание ---
            //
            // `MushSquash` — второй вид гибели, для сильного удара. Выбор по
            // `DeathVariant`: 1 обычная, 2 расплющивание. Ставит его тот, кто
            // наносит смертельный урон, — гриб от кинжала оседает, а от
            // тяжёлого удара его сплющивает.
            var death = machine.AddState("Death");
            death.motion = die;

            var toDeath = machine.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            toDeath.AddCondition(AnimatorConditionMode.NotEqual, 2, "DeathVariant");
            toDeath.hasExitTime = false;
            toDeath.duration = 0.1f;
            toDeath.canTransitionToSelf = false;

            var squash = Clip("MushSquash");

            if (squash != null)
            {
                var flat = machine.AddState("Death_Squash");
                flat.motion = squash;

                var toFlat = machine.AddAnyStateTransition(flat);
                toFlat.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                toFlat.AddCondition(AnimatorConditionMode.Equals, 2, "DeathVariant");
                toFlat.hasExitTime = false;
                toFlat.duration = 0.1f;
                toFlat.canTransitionToSelf = false;

                var up = flat.AddTransition(move);
                up.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                up.hasExitTime = false;
                up.duration = 0.1f;
            }

            var revive = death.AddTransition(move);
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            revive.hasExitTime = false;
            revive.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[IsoRPG] Контроллер гриба собран: состояний {machine.states.Length}, " +
                      $"ударов {AttackVariants} плюс долгий из трёх фаз, блок, вздрагивание, " +
                      "засада статуей, ход вперёд и назад.");

            return controller;
        }

        private static void Back(AnimatorState state, float exitAt)
        {
            var back = state.AddExitTransition();
            back.hasExitTime = true;
            back.exitTime = exitAt;
            back.duration = 0.12f;
        }

        /// <summary>Клип по имени — все они лежат внутри одного FBX.</summary>
        private static AnimationClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(Model)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => c.name == name);

            if (clip == null) Debug.LogWarning("[IsoRPG] Клип гриба не найден: " + name);

            return clip;
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

            foreach (var b in bindings)
            {
                if (b.path != rootPath) continue;
                if (!b.propertyName.StartsWith("m_LocalPosition")) continue;

                AnimationUtility.SetEditorCurve(clip, b, null);
            }

            EditorUtility.SetDirty(clip);
        }
    }
}
