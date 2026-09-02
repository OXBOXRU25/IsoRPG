using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает волку контроллер анимаций под НАШИ параметры.
    ///
    /// У набора есть свой `Polygonal Wolf.controller`, но он говорит своими
    /// именами (`Bite`, `Howl`, `Pound`) и переключается сам по себе. Наш
    /// боевой код пишет `Speed`, дёргает `Attack`, поднимает `Dead` — чужой
    /// контроллер этих имён не знает, и волк стоял бы столбом при живом ИИ.
    /// Поэтому строим свой: те же клипы набора, но наши имена.
    ///
    /// **Берём весь набор, а не четыре клипа.** До 02.09.2026 отсюда шли
    /// стойка, шаг, бег, укус и смерть — пять из пятнадцати. Полная опись
    /// (по одному клипу на файл):
    ///
    ///   бой        Bite Attack, Pound Attack, Breath Attack, Take Damage
    ///   движение   Idle, Walk Forward, Run Forward, Walk Backward, Jump
    ///   покой      Eating, Resting, Look Around, Howl
    ///   конец      Die
    ///
    /// Вой попал сюда не случайно. Он лежал в банке звуков с прошлого захода
    /// и не играл ни разу — не было повода. Повод нашёлся в самом наборе: раз
    /// есть клип, значит вой — это занятие, такое же как еда и отдых. Волк
    /// садится, задирает морду и воет; звук вешает
    /// <see cref="IsoRPG.Audio.RestVoice"/> на то же занятие, и картинка со
    /// звуком совпадают.
    ///
    /// Клипы движения берём в версии <b>WO Root</b> — без корневого движения.
    /// С корневым анимация тянет волка сама, и он уезжает от навигационного
    /// агента: агент считает, что зверь в одном месте, а видим мы его в другом.
    /// </summary>
    public static class WolfAnimations
    {
        private const string Clips = "Assets/Polygonal Wolf/FBX";
        private const string Target = "Assets/_Game/Art/Animations/Controllers/AC_Wolf.controller";

        /// <summary>Сколько ударов в серии. Столько же состояний Attack_N.</summary>
        public const int AttackVariants = 3;

        /// <summary>Номер занятия «повыть». К нему цепляется голос.</summary>
        public const int HowlKind = 4;

        /// <summary>Пороги дерева смешивания, метры в секунду.</summary>
        private const float WalkAt = 1.2f, RunAt = 3.4f;

        [MenuItem("Tools/IsoRPG/Волк: собрать контроллер", priority = 37)]
        public static AnimatorController Build()
        {
            var idle = Clip("Polygonal Wolf@Idle.FBX");
            var walk = Clip("Polygonal Wolf@Walk Forward WO Root.FBX");
            var run = Clip("Polygonal Wolf@Run Forward WO Root.FBX");
            var die = Clip("Polygonal Wolf@Die.FBX");

            if (idle == null || walk == null || run == null || die == null)
            {
                Debug.LogError("[IsoRPG] Клипы волка не нашлись — контроллер не собран.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Target) != null)
                AssetDatabase.DeleteAsset(Target);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(Target);

            // Все параметры, которые дёргает наш водитель анимаций. Лишние не
            // мешают, а недостающие сыпали бы предупреждениями в каждом кадре
            // и прятали настоящие ошибки.
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("AttackSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Rest", AnimatorControllerParameterType.Int);
            controller.AddParameter("StealthKill", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Eating", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            var machine = controller.layers[0].stateMachine;

            // --- ход --------------------------------------------------------
            //
            // Одномерное дерево, а не двумерное как у кабана: у набора нет ни
            // наклонов, ни бокового шага — только вперёд и назад. Задний ход
            // тоже не берём: скорость к нам приходит модулем, отрицательной
            // она не бывает, и клип остался бы недостижимым украшением.
            var tree = new BlendTree
            {
                name = "Ход",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            // Пороги берём ИЗ САМИХ КЛИПОВ, а не из головы.
            //
            // У набора каждый клип хода есть в двух версиях: с корневым
            // движением и без. Играем версию БЕЗ (иначе волк уезжает от
            // своего агента), а меряем по версии С — она и говорит, с какой
            // скоростью автор нарисовал этот шаг. Порог мимо неё читается как
            // скольжение по земле.
            float walkSpeed = NaturalSpeed("Polygonal Wolf@Walk Forward W Root.FBX", WalkAt);
            float runSpeed = NaturalSpeed("Polygonal Wolf@Run Forward W Root.FBX", RunAt);

            // Клип бега у набора идёт 2.47 м/с, а волк гонится на 3.6 —
            // выше верхнего порога дерево упирается в потолок и играет бег
            // как есть, то есть лапы отстают от земли почти наполовину.
            // Лечится растяжением: порог поднимаем до боевой скорости, а клип
            // проигрываем во столько же раз быстрее. Тогда лапы снова
            // совпадают с землёй.
            float runScale = 1f;

            if (runSpeed > 0.05f && WolfPack.ChaseSpeed > runSpeed)
            {
                runScale = WolfPack.ChaseSpeed / runSpeed;
                runSpeed = WolfPack.ChaseSpeed;
            }

            tree.children = new[]
            {
                new ChildMotion { motion = idle, threshold = 0f,        timeScale = 1f },
                new ChildMotion { motion = walk, threshold = walkSpeed, timeScale = 1f },
                new ChildMotion { motion = run,  threshold = runSpeed,  timeScale = runScale },
            };

            var move = machine.AddState("Locomotion");
            move.motion = tree;
            machine.defaultState = move;

            // --- три удара по кругу -----------------------------------------
            //
            // Вход из любого состояния по триггеру и номеру: так удар не
            // теряется, если волк в этот миг бежал или вздрагивал. Раньше
            // укус был один, и драка звучала метрономом.
            string[] attacks =
            {
                "Polygonal Wolf@Bite Attack.FBX",
                "Polygonal Wolf@Pound Attack WO Root.FBX",
                "Polygonal Wolf@Breath Attack.FBX",
            };

            for (int i = 0; i < attacks.Length; i++)
            {
                var clip = Clip(attacks[i]);
                if (clip == null) continue;

                var state = machine.AddState("Attack_" + (i + 1));
                state.motion = clip;
                state.speedParameterActive = true;
                state.speedParameter = "AttackSpeed";

                var any = machine.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                any.AddCondition(AnimatorConditionMode.Equals, i + 1, "AttackVariant");
                any.duration = 0.05f;
                any.canTransitionToSelf = false;

                Back(state, 0.9f);
            }

            // --- вздрагивание -----------------------------------------------
            //
            // Одно на все стороны: направленных клипов у набора нет, и
            // параметра `HitDir` мы не заводим — водитель проверяет наличие
            // сам и молча пропускает.
            var hurt = Clip("Polygonal Wolf@Take Damage.FBX");

            if (hurt != null)
            {
                var state = machine.AddState("GetHit");
                state.motion = hurt;

                var any = machine.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                any.duration = 0.06f;
                any.canTransitionToSelf = false;

                // Те же 0.40 с реакции, что у всех: длиннее — и зверь
                // проводит в ней весь бой (разбор 01.09.2026).
                float share = hurt.length > 0.45f ? 0.40f / hurt.length : 0.9f;
                Back(state, Mathf.Clamp(share, 0.1f, 0.9f));
            }

            // --- прыжок ------------------------------------------------------
            OneShot(machine, "Jump", Clip("Polygonal Wolf@Jump WO Root.FBX"), "Jump", 0.85f);

            // --- покой вне боя ----------------------------------------------
            //
            // Входное состояние зовётся `Rest_N`: по имени праздное поведение
            // узнаёт, что этот зверь умеет.
            AddRest(machine, move, 1, Clip("Polygonal Wolf@Eating.FBX"));
            AddRest(machine, move, 2, Clip("Polygonal Wolf@Resting.FBX"));
            AddRest(machine, move, 3, Clip("Polygonal Wolf@Look Around.FBX"));
            AddRest(machine, move, HowlKind, Clip("Polygonal Wolf@Howl.FBX"));

            // --- смерть -------------------------------------------------------
            var death = machine.AddState("Death");
            death.motion = die;

            var toDeath = machine.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            toDeath.hasExitTime = false;
            toDeath.duration = 0.1f;
            toDeath.canTransitionToSelf = false;

            // Обратно — когда возродился. Без этого воскресший волк бегает лёжа.
            var revive = death.AddTransition(move);
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            revive.hasExitTime = false;
            revive.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[IsoRPG] Контроллер волка собран: состояний {machine.states.Length}, " +
                      $"ударов {AttackVariants} (укус, наскок, рык), вздрагивание, прыжок, " +
                      "покой (еда, лежит, озирается, воет), смерть с возвращением. " +
                      "НЕ взят задний ход: скорость приходит модулем, клип был бы недостижим.\n" +
                      $"  Скорости, снятые с самих клипов: шаг {walkSpeed:0.00}, " +
                      $"бег {runSpeed:0.00} м/с при растяжении клипа x{runScale:0.00}.");

            return controller;
        }

        // ------------------------------------------------------------------

        /// <summary>Занятие вне боя: держится, пока `Rest` равен своему номеру.</summary>
        private static void AddRest(AnimatorStateMachine machine, AnimatorState from,
                                    int kind, AnimationClip clip)
        {
            if (clip == null) return;

            var state = machine.AddState("Rest_" + kind);
            state.motion = clip;

            var into = from.AddTransition(state);
            into.hasExitTime = false;
            into.duration = 0.25f;
            into.AddCondition(AnimatorConditionMode.Equals, kind, "Rest");

            var back = state.AddTransition(from);
            back.hasExitTime = false;
            back.duration = 0.25f;
            back.AddCondition(AnimatorConditionMode.NotEqual, kind, "Rest");
        }

        /// <summary>Разовое действие по триггеру.</summary>
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

        /// <summary>Возврат в ход по концу клипа — через выход стейт-машины.</summary>
        private static void Back(AnimatorState state, float exitAt)
        {
            var back = state.AddExitTransition();
            back.hasExitTime = true;
            back.exitTime = exitAt;
            back.duration = 0.12f;
        }

        /// <summary>Скорость клипа общей мерой; ноль — порог запасной, и об этом в журнал.</summary>
        private static float NaturalSpeed(string file, float fallback)
        {
            float speed = ClipSpeed.Measure(Clip(file));

            if (speed > 0f) return speed;

            Debug.LogWarning("[IsoRPG] У клипа " + file + " скорость не померилась — порог запасной.");
            return fallback;
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
