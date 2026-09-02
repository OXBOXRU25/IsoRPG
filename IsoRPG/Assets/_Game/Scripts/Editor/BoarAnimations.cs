using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает рядовому кабану контроллер под НАШИ параметры.
    ///
    /// **Берём весь набор, а не четыре клипа.** До 02.09.2026 здесь стояло
    /// пять состояний: стойка, шаг, бег, один удар и смерть. У набора Malbers
    /// Forest Pack их 68, и лежат они не по одному в файле, а таками внутри
    /// шестнадцати FBX — поэтому по списку файлов набор и выглядел бедным.
    /// Полная опись (файл — клипы):
    ///
    ///   Actions   BDrink, BEat
    ///   Attack    BAttack Fangs 1, Fangs 2, Fangs Front, Bite
    ///   Death     BDeath Side, BDeath 2 (плюс две «Opp» — для добивания вдвоём)
    ///   Fall      BEdge Jump, BFall High/Low/Water, BFall Recover
    ///   Get Hit   BGetHit R/L, Front R/L, Back R/L (плюс две маски)
    ///   Idle      BIdle, SmallShake, Big Shake, Look
    ///   Jump      BJump Trot/Sprint/Run (плюс Baked), три приземления
    ///   Run       BRun, BRunR, BRunL, BAttackRun (плюс маска)
    ///   Sleep     BStand to Seat, BSeat Idle, BSeat to Sleep, BSleep Idle, BSleep to Stand
    ///   Swim      девять клипов плавания
    ///   Trot      BTrot, BTrot R, BTrot L
    ///   Turn      BTurn R/L, BTurn180 L/R
    ///   Walk      BWalk, BWalk R, BWalk L
    ///   WalkBack  BWalkBack, BWalkBack L/R
    ///
    /// Что взято, а что нет — печатается в конце сборки, чтобы список не
    /// расходился с кодом.
    ///
    /// Клипы движения несут КОРНЕВОЕ смещение (собственный контроллер набора
    /// читает его сам). Мы движение ведём навигацией, поэтому смещение
    /// снимаем — иначе кабан уезжает от своего агента. Правим не суб-ассет
    /// FBX, а клон файлом: правка на суб-ассете уже один раз молча пропала,
    /// Unity вправе пересобрать клип из источника при следующем импорте.
    /// </summary>
    public static class BoarAnimations
    {
        private const string Clips =
            "Assets/Malbers Animations/Animals Packs/01 Forest Pack/Boar/Anims";

        private const string Target = "Assets/_Game/Art/Animations/Controllers/AC_Boar.controller";
        private const string CloneFolder = "Assets/_Game/Art/Animations/Boar";

        /// <summary>Сколько ударов в серии. Столько же состояний Attack_N.</summary>
        public const int AttackVariants = 4;

        /// <summary>Сколько равнозначных падений. Выбирает водитель анимаций жребием.</summary>
        public const int DeathVariants = 2;

        /// <summary>
        /// Запасной порог, метры в секунду, — если скорость клипа не померилась.
        /// Остальные пороги берутся из самих клипов, см. ClipSpeed.Measure.
        /// </summary>
        private const float WalkAt = 1.0f;

        /// <summary>С какой скорости удар считается разгонным.</summary>
        private const float TrotAt = 2.2f;

        /// <summary>Что намерили по клипам — уходит в журнал, чтобы числа были видны.</summary>
        private static readonly System.Text.StringBuilder Measured = new System.Text.StringBuilder();

        /// <summary>Порог разворота на месте, градусы в секунду.</summary>
        private const float TurnAt = 70f;

        [MenuItem("Tools/IsoRPG/Кабан: собрать контроллер", priority = 39)]
        public static AnimatorController Build()
        {
            // Настройки импорта — первым делом: контроллер должен собираться
            // на уже исправленных клипах.
            //
            // `Boar Get Hit` в наборе помечен зацикленным (ошибка автора):
            // вздрагивание не кончалось само, и держалось оно только коротким
            // выходом по времени. Смерть и удары тоже не должны повторяться.
            SetLoop(false, "Boar Get Hit.FBX",
                    "BGetHit R", "BGetHit L", "BGetHit Front R", "BGetHit Front L",
                    "BGetHit Back R", "BGetHit Back L");

            SetLoop(false, "Boar Death.FBX", "BDeath Side", "BDeath 2");

            SetLoop(false, "Boar Attack.FBX",
                    "BAttack Fangs 1", "BAttack Fangs 2", "BAttack Fangs Front", "BAttack Bite");

            // Переходы позы не зациклены, а сами позы — наоборот: без петли
            // сидящий кабан замирает на последнем кадре, и дыхание пропадает.
            SetLoop(false, "Boar Sleep.FBX", "BStand to Seat", "BSeat to Sleep", "BSleep to Stand");
            SetLoop(true, "Boar Sleep.FBX", "BSeat Idle", "BSleep Idle");
            SetLoop(true, "Boar Actions.FBX", "BEat", "BDrink");

            Measured.Clear();

            var idle = Clip("Boar Idle.FBX", "BIdle");
            var alert = Clip("Boar Idle.FBX", "BIdle Look");
            var die1 = Clip("Boar Death.FBX", "BDeath Side");
            var die2 = Clip("Boar Death.FBX", "BDeath 2");

            if (idle == null || die1 == null)
            {
                Debug.LogError("[IsoRPG] Клипы кабана не нашлись — контроллер не собран.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Target) != null)
                AssetDatabase.DeleteAsset(Target);

            EnsureFolder();

            var controller = AnimatorController.CreateAnimatorControllerAtPath(Target);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Strafe", AnimatorControllerParameterType.Float);
            controller.AddParameter("Turn", AnimatorControllerParameterType.Float);
            controller.AddParameter("AttackSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("AttackVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitDir", AnimatorControllerParameterType.Int);
            controller.AddParameter("InCombat", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Rest", AnimatorControllerParameterType.Int);
            controller.AddParameter("DeathVariant", AnimatorControllerParameterType.Int);
            controller.AddParameter("StealthKill", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Eating", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            var machine = controller.layers[0].stateMachine;

            // --- ход: мирный и настороженный -------------------------------
            //
            // Различаются серединой дерева: спокойная стойка или «озирается».
            // Боевой стойки у кабана в наборе нет вовсе, и придумывать её из
            // чужого клипа не надо — «озирается» и есть его настороженность.
            var peace = machine.AddState("Locomotion");
            peace.motion = MoveTree(controller, "Ход", idle);
            machine.defaultState = peace;

            var combat = machine.AddState("LocomotionCombat");
            combat.motion = MoveTree(controller, "Ход настороже", alert ?? idle);

            Switch(peace, combat, "InCombat", true);
            Switch(combat, peace, "InCombat", false);

            // --- разворот на месте -----------------------------------------
            //
            // Ровно та жалоба, которую Павлон видел 01.09.2026: «мелкие кабаны
            // в бою разворачиваются» — разворот шёл голым скольжением, потому
            // что клипы поворота у набора были, а параметра к ним не было.
            AddTurn(machine, peace, combat, "Turn_Right", NoRoot("Boar Turn.FBX", "BTurn R"), 1);
            AddTurn(machine, peace, combat, "Turn_Left", NoRoot("Boar Turn.FBX", "BTurn L"), -1);

            // --- удары ------------------------------------------------------
            //
            // Четыре, по кругу. Сколько их — знает и водитель анимаций: раньше
            // бой слал номер от 1 до 6 всем подряд, и у кабана каждый третий
            // удар уходил в несуществующее состояние, то есть в пустоту.
            string[] attacks =
            {
                "BAttack Fangs 1", "BAttack Fangs 2", "BAttack Fangs Front", "BAttack Bite",
            };

            // Разгонный удар — ПЕРВЫМ переходом из любого состояния: переходы
            // проверяются в порядке добавления, и на бегу должен выигрывать он.
            var charge = Clip("Boar Run.FBX", "BAttackRun");

            if (charge != null)
            {
                StripRoot(charge);

                var state = machine.AddState("Attack_Charge");
                state.motion = charge;

                var any = machine.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                any.AddCondition(AnimatorConditionMode.Greater, TrotAt, "Speed");
                any.duration = 0.05f;
                any.canTransitionToSelf = false;

                Back(state, 0.9f);
            }

            for (int i = 0; i < attacks.Length; i++)
            {
                var clip = Clip("Boar Attack.FBX", attacks[i]);
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

            // --- вздрагивание с четырёх сторон ------------------------------
            //
            // Сторона приходит числом в `HitDir`: 0 спереди, 1 сзади, 2 слева,
            // 3 справа — те же номера, что у босса, водитель у них общий.
            AddHit(machine, "GetHit_Front", Clip("Boar Get Hit.FBX", "BGetHit Front R"), 0);
            AddHit(machine, "GetHit_Back", Clip("Boar Get Hit.FBX", "BGetHit Back R"), 1);
            AddHit(machine, "GetHit_Left", Clip("Boar Get Hit.FBX", "BGetHit L"), 2);
            AddHit(machine, "GetHit_Right", Clip("Boar Get Hit.FBX", "BGetHit R"), 3);

            // --- прыжок ------------------------------------------------------
            OneShot(machine, "Jump", NoRoot("Boar Jump.FBX", "BJump Trot"), "Jump", 0.85f);

            // --- покой вне боя ----------------------------------------------
            //
            // Входное состояние каждого занятия зовётся `Rest_N` — по этому
            // имени праздное поведение и узнаёт, что зверь умеет. Раньше
            // список занятий стоял числом в компоненте и всем врал одинаково.
            AddRest(machine, peace, 1, Clip("Boar Actions.FBX", "BEat"));
            AddRest(machine, peace, 4, Clip("Boar Actions.FBX", "BDrink"));

            AddSit(machine, peace);
            AddSleep(machine, peace);

            // --- смерть: два падения ----------------------------------------
            AddDeath(machine, peace, "Death", die1, 1);
            AddDeath(machine, peace, "Death_2", die2, 2);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[IsoRPG] Контроллер кабана собран: состояний {machine.states.Length}, " +
                      $"ударов {AttackVariants} плюс разгонный, вздрагиваний 4, разворот на месте, " +
                      "движение двумерным деревом (шаг/рысь/бег с наклонами и задний ход), " +
                      "покой (еда, питьё, сидение, сон), два падения. " +
                      "НЕ взяты: плавание (9 клипов, воды под ногами не бывает), падение с высоты " +
                      "(5, нет обрывов), маски (3, для добавочных слоёв), парные смерти Opp (2, " +
                      "для добивания вдвоём), развороты на 180 градусов, запечённые прыжки и " +
                      "два клипа встряхивания — им нужен свой повод, его пока нет.\n" +
                      "  Скорости аллюров, снятые с самих клипов (м/с): " + Measured);

            return controller;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Дерево движения: вперёд-назад по `Speed`, вбок по `Strafe`.
        ///
        /// Двумерное, потому что у набора есть наклоны на всех трёх аллюрах и
        /// задний ход. Плоское одномерное дерево выбросило бы девять клипов из
        /// двенадцати — ровно то, что и было до сегодня.
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

            var children = new List<ChildMotion>
            {
                new ChildMotion { motion = centre, position = Vector2.zero, timeScale = 1f },
            };

            // Пороги берём ИЗ САМИХ КЛИПОВ, а не из головы.
            //
            // Клип идёт со своей скоростью — той, с какой автор нарисовал шаг.
            // Если поставить порог мимо неё, ноги перебирают быстрее или
            // медленнее, чем зверь едет, и это читается как скольжение по
            // земле. Мерить есть по чему: смещение корня за клип, делённое на
            // его длину, — это и есть скорость аллюра в метрах в секунду.
            // Замер делается ДО того, как мы снимем корневые кривые.
            Add(children, "Boar Walk.FBX", "BWalk", 0f, 1f);
            Add(children, "Boar Walk.FBX", "BWalk L", -0.45f, 1f);
            Add(children, "Boar Walk.FBX", "BWalk R", 0.45f, 1f);

            Add(children, "Boar Trot.FBX", "BTrot", 0f, 1f);
            Add(children, "Boar Trot.FBX", "BTrot L", -0.45f, 1f);
            Add(children, "Boar Trot.FBX", "BTrot R", 0.45f, 1f);

            Add(children, "Boar Run.FBX", "BRun", 0f, 1f);
            Add(children, "Boar Run.FBX", "BRunL", -0.45f, 1f);
            Add(children, "Boar Run.FBX", "BRunR", 0.45f, 1f);

            Add(children, "Boar WalkBack.FBX", "BWalkBack", 0f, -1f);
            Add(children, "Boar WalkBack.FBX", "BWalkBack L", -0.45f, -1f);
            Add(children, "Boar WalkBack.FBX", "BWalkBack R", 0.45f, -1f);

            tree.children = children.ToArray();

            return tree;
        }

        /// <summary>
        /// Добавить клип в дерево, поставив его на СВОЮ скорость.
        ///
        /// `side` — доля бокового смещения (наклон влево-вправо), `sign` —
        /// вперёд (+1) или назад (−1). Сама скорость меряется по клипу.
        /// </summary>
        private static void Add(List<ChildMotion> list, string file, string clipName,
                                float side, float sign)
        {
            // Замер — на ИСХОДНОМ клипе: у клона корневых кривых уже нет,
            // мерить там будет нечего, и все аллюры встали бы на ноль.
            float speed = ClipSpeed.Measure(Clip(file, clipName));

            var clip = NoRoot(file, clipName);
            if (clip == null) return;

            if (speed <= 0.05f)
            {
                Debug.LogWarning($"[IsoRPG] У клипа «{clipName}» не померилась скорость — " +
                                 "он встанет на запасной порог.");
                speed = sign > 0f ? WalkAt : WalkAt;
            }

            Measured.Append(clipName).Append(' ').Append(speed.ToString("0.00")).Append("  ");

            list.Add(new ChildMotion
            {
                motion = clip,
                position = new Vector2(side * speed, sign * speed),
                timeScale = 1f,
            });
        }


        /// <summary>
        /// Разворот на месте: держится, пока зверь крутится и не идёт.
        ///
        /// Вход из обоих ходов — мирного и настороженного: разворачивается он
        /// и в бою, и на прогулке.
        /// </summary>
        private static void AddTurn(AnimatorStateMachine machine, AnimatorState peace,
                                    AnimatorState combat, string name, AnimationClip clip, int sign)
        {
            if (clip == null) return;

            var state = machine.AddState(name);
            state.motion = clip;

            foreach (var from in new[] { peace, combat })
            {
                var into = from.AddTransition(state);
                into.hasExitTime = false;
                into.duration = 0.12f;
                into.AddCondition(sign > 0 ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less,
                                  TurnAt * sign, "Turn");
                into.AddCondition(AnimatorConditionMode.Less, 0.35f, "Speed");
            }

            // Возврат в мирный ход; если зверь при этом в бою, переключатель
            // `InCombat` переведёт его дальше сам — состояние одно, правило одно.
            var back = state.AddTransition(peace);
            back.hasExitTime = false;
            back.duration = 0.15f;
            back.AddCondition(sign > 0 ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater,
                              TurnAt * 0.4f * sign, "Turn");

            var walkAway = state.AddTransition(peace);
            walkAway.hasExitTime = false;
            walkAway.duration = 0.15f;
            walkAway.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
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

            // Доля от клипа, дающая те же 0.40 с, что у всех остальных
            // (задание flinch-tune и разбор в памяти проекта).
            float share = clip.length > 0.45f ? 0.40f / clip.length : 0.9f;
            Back(state, Mathf.Clamp(share, 0.1f, 0.9f));
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

        /// <summary>Простое занятие в одном клипе: держится, пока `Rest` равен своему номеру.</summary>
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

        /// <summary>
        /// Сидение: опуститься и сидеть.
        ///
        /// Двумя состояниями, потому что у набора это два клипа — переход и
        /// поза. Одним клипом кабан либо садился бы вечно, либо появлялся уже
        /// сидящим.
        /// </summary>
        private static void AddSit(AnimatorStateMachine machine, AnimatorState from)
        {
            var down = Clip("Boar Sleep.FBX", "BStand to Seat");
            var hold = Clip("Boar Sleep.FBX", "BSeat Idle");

            if (down == null || hold == null) return;

            var enter = machine.AddState("Rest_2");
            enter.motion = down;

            var idle = machine.AddState("Sit_Idle");
            idle.motion = hold;

            var into = from.AddTransition(enter);
            into.hasExitTime = false;
            into.duration = 0.25f;
            into.AddCondition(AnimatorConditionMode.Equals, 2, "Rest");

            var seated = enter.AddTransition(idle);
            seated.hasExitTime = true;
            seated.exitTime = 0.95f;
            seated.duration = 0.1f;

            // Встаём из обеих фаз: зверя могут потревожить и на полпути вниз.
            Leave(enter, from, 2);
            Leave(idle, from, 2);
        }

        /// <summary>Сон: сесть, лечь, спать, встать. Встаёт по своему клипу — он в наборе есть.</summary>
        private static void AddSleep(AnimatorStateMachine machine, AnimatorState from)
        {
            var down = Clip("Boar Sleep.FBX", "BStand to Seat");
            var lie = Clip("Boar Sleep.FBX", "BSeat to Sleep");
            var hold = Clip("Boar Sleep.FBX", "BSleep Idle");
            var up = Clip("Boar Sleep.FBX", "BSleep to Stand");

            if (down == null || lie == null || hold == null) return;

            var enter = machine.AddState("Rest_3");
            enter.motion = down;

            var lying = machine.AddState("Sleep_Down");
            lying.motion = lie;

            var asleep = machine.AddState("Sleep_Idle");
            asleep.motion = hold;

            var into = from.AddTransition(enter);
            into.hasExitTime = false;
            into.duration = 0.25f;
            into.AddCondition(AnimatorConditionMode.Equals, 3, "Rest");

            Chain(enter, lying);
            Chain(lying, asleep);

            if (up != null)
            {
                var rise = machine.AddState("Sleep_Up");
                rise.motion = up;

                var wake = asleep.AddTransition(rise);
                wake.hasExitTime = false;
                wake.duration = 0.2f;
                wake.AddCondition(AnimatorConditionMode.NotEqual, 3, "Rest");

                Chain(rise, from);
            }
            else
            {
                Leave(asleep, from, 3);
            }

            Leave(enter, from, 3);
            Leave(lying, from, 3);
        }

        /// <summary>Падение по номеру. Возврат — когда возродился.</summary>
        private static void AddDeath(AnimatorStateMachine machine, AnimatorState back,
                                     string name, AnimationClip clip, int variant)
        {
            if (clip == null) return;

            var state = machine.AddState(name);
            state.motion = clip;

            var any = machine.AddAnyStateTransition(state);
            any.AddCondition(AnimatorConditionMode.If, 0f, "Dead");

            // Первое падение забирает и всё, чего жребий не назвал: параметр
            // может остаться нулём, если водитель анимаций жребия не бросал.
            any.AddCondition(variant == 1 ? AnimatorConditionMode.NotEqual : AnimatorConditionMode.Equals,
                             2, "DeathVariant");

            any.hasExitTime = false;
            any.duration = 0.1f;
            any.canTransitionToSelf = false;

            // Обратно — когда возродился. Без этого воскресший зверь остаётся
            // лежать и бьёт из положения трупа.
            var revive = state.AddTransition(back);
            revive.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            revive.hasExitTime = false;
            revive.duration = 0.1f;
        }

        /// <summary>Следующая фаза по концу клипа.</summary>
        private static void Chain(AnimatorState from, AnimatorState to)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 0.95f;
            t.duration = 0.1f;
        }

        /// <summary>Бросить занятие, как только `Rest` сменился.</summary>
        private static void Leave(AnimatorState from, AnimatorState to, int kind)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.25f;
            t.AddCondition(AnimatorConditionMode.NotEqual, kind, "Rest");
        }

        /// <summary>Возврат в ход по концу клипа — через выход стейт-машины.</summary>
        private static void Back(AnimatorState state, float exitAt)
        {
            var back = state.AddExitTransition();
            back.hasExitTime = true;
            back.exitTime = exitAt;
            back.duration = 0.12f;
        }

        /// <summary>Переключение между мирным и настороженным ходом по флагу.</summary>
        private static void Switch(AnimatorState from, AnimatorState to, string flag, bool value)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.2f;
            t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, flag);
        }

        // ------------------------------------------------------------------

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(CloneFolder))
                AssetDatabase.CreateFolder("Assets/_Game/Art/Animations", "Boar");
        }

        /// <summary>
        /// Клон клипа БЕЗ кривых смещения корня, отдельным файлом.
        ///
        /// Корень движения у Malbers — не тот трансформ, на котором висит
        /// аниматор, а первый сустав скелета под ним («CG» у кабана). Вместо
        /// угадывания имени берём путь с МЕНЬШЕЙ глубиной среди костей, что
        /// вообще двигают позицию: это и есть корень, как его ни назови.
        /// </summary>
        private static AnimationClip NoRoot(string file, string clipName)
        {
            var source = Clip(file, clipName);
            if (source == null) return null;

            EnsureFolder();

            string safe = clipName.Replace(' ', '_');
            string path = CloneFolder + "/" + safe + "_NoRoot.anim";

            var clone = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clone == null)
            {
                clone = Object.Instantiate(source);
                clone.name = safe + "_NoRoot";
                AssetDatabase.CreateAsset(clone, path);
            }
            else
            {
                clone.ClearCurves();
                EditorUtility.CopySerialized(source, clone);
                clone.name = safe + "_NoRoot";
            }

            StripRoot(clone);

            EditorUtility.SetDirty(clone);

            return clone;
        }

        private static void StripRoot(AnimationClip clip)
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
        }

        /// <summary>
        /// Поправить петлю в настройках импорта набора.
        ///
        /// `Boar Get Hit` помечен зацикленным — ошибка автора набора. Правка
        /// живёт в настройках импорта, а папка набора в `.gitignore`, поэтому
        /// в репозиторий она не попадает: после переустановки набора её надо
        /// прогнать заново. Ради этого она и сделана заданием, а не руками.
        /// </summary>
        private static void SetLoop(bool loop, string file, params string[] names)
        {
            string path = Clips + "/" + file;

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null)
            {
                Debug.LogWarning("[IsoRPG] Нет настроек импорта для " + path);
                return;
            }

            var takes = importer.clipAnimations;

            // Пустой список значит «автор ничего не настраивал руками»: берём
            // разбивку по умолчанию, иначе присвоение снесло бы все таки.
            if (takes == null || takes.Length == 0) takes = importer.defaultClipAnimations;

            int changed = 0;

            for (int i = 0; i < takes.Length; i++)
            {
                if (!names.Contains(takes[i].name)) continue;
                if (takes[i].loopTime == loop) continue;

                takes[i].loopTime = loop;
                changed++;
            }

            if (changed == 0) return;

            importer.clipAnimations = takes;
            importer.SaveAndReimport();

            Debug.Log($"[IsoRPG] {file}: петля {(loop ? "включена" : "снята")} у {changed} клипов.");
        }

        private static AnimationClip Clip(string file, string clipName)
        {
            string path = Clips + "/" + file;

            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => c.name == clipName);

            if (clip == null)
                Debug.LogWarning($"[IsoRPG] Клип кабана не найден: {file} — «{clipName}».");

            return clip;
        }
    }
}
