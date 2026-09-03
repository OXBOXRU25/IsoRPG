using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Оживляет бой героя: серия ударов вместо одного, вздрагивание от
    /// попаданий, отдельные жесты способностей, уклонение.
    ///
    /// Зачем. В контроллере героя было шесть состояний — ход, ОДИН удар,
    /// прыжок, смерть, добивание, еда. Из 1165 боевых клипов набора
    /// ExplosiveLLC работали три. Бой от этого читался метрономом: каждый
    /// удар один и тот же кадр в кадр, попадания по герою видно только по
    /// цифрам, а четыре кнопки способностей играли обычный замах.
    ///
    /// Задание дополняет СУЩЕСТВУЮЩИЙ контроллер, а не пересобирает его:
    /// ход, смерть и прыжок в нём уже настроены, и трогать их незачем.
    /// Прогонять можно повторно — состояния с теми же именами не плодятся.
    /// </summary>
    public static class HeroCombatAnimations
    {
        private const string ControllerPath =
            "Assets/_Game/Art/Animations/Controllers/AC_Hero_Sidekick.controller";

        private const string Pack =
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations/";

        /// <summary>
        /// Удары кинжалом. Герой дерётся парой, и клипы левой и правой руки
        /// чередуются по кругу — так драка перестаёт быть метрономом.
        /// </summary>
        private static readonly string[] Attacks =
        {
            "1Hand-Dagger/RPG-Character@Dagger-Attack-R1.FBX",
            "1Hand-Dagger/RPG-Character@Dagger-Attack-L1.FBX",
            "1Hand-Dagger/RPG-Character@Dagger-Attack-R2.FBX",
            "1Hand-Dagger/RPG-Character@Dagger-Attack-L2.FBX",
            "1Hand-Dagger/RPG-Character@Dagger-Attack-R3.FBX",
            "1Hand-Dagger/RPG-Character@Dagger-Attack-L3.FBX",
        };

        /// <summary>Вздрагивание от удара. Направление пока одно — спереди.</summary>
        private const string GetHitClip = "Armed/RPG-Character@Armed-GetHit-F1.FBX";

        /// <summary>Уклонение вбок.</summary>
        private const string DodgeClip = "Armed/RPG-Character@Armed-Dodge-Backward.FBX";

        /// <summary>
        /// Жесты способностей. Разные по смыслу: направленный удар, площадь,
        /// усиление себя. Игрок должен видеть, что нажал разные кнопки.
        /// </summary>
        private static readonly (string clip, string trigger, string state)[] Casts =
        {
            ("Armed/RPG-Character@Armed-Cast-R-Attack1.FBX", "CastAttack", "Cast_Attack"),
            ("Armed/RPG-Character@Armed-Cast-R-AOE1.FBX",    "CastAOE",    "Cast_AOE"),
            ("Armed/RPG-Character@Armed-Cast-R-Buff1.FBX",   "CastBuff",   "Cast_Buff"),
        };

        [MenuItem("Tools/IsoRPG/Герой: оживить бой", priority = 34)]
        public static void Build()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                Debug.LogError("[IsoRPG] Нет контроллера героя: " + ControllerPath);
                return;
            }

            var root = controller.layers[0].stateMachine;

            EnsureParameter(controller, "AttackVariant", AnimatorControllerParameterType.Int);
            EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Dodge", AnimatorControllerParameterType.Trigger);

            foreach (var cast in Casts)
                EnsureParameter(controller, cast.trigger, AnimatorControllerParameterType.Trigger);

            int added = 0;

            // Серия ударов. Переход из Any State по общему триггеру «Attack»
            // плюс номер варианта: так боевой код продолжает дёргать один
            // триггер, как раньше, и знать про варианты ему незачем — он
            // только выставляет число.
            for (int i = 0; i < Attacks.Length; i++)
            {
                string stateName = "Attack_" + (i + 1);

                var already = Find(root, stateName);

                if (already != null)
                {
                    // Состояние есть, но выхода у него может не быть: первая
                    // версия задания их не ставила, и герой залипал в позе
                    // удара. Чиним на месте, а не пересоздаём.
                    EnsureExit(already);
                    continue;
                }

                var clip = LoadClip(Attacks[i]);
                if (clip == null) continue;

                var state = root.AddState(stateName);
                state.motion = clip;
                state.speedParameterActive = true;
                state.speedParameter = "AttackSpeed";

                var any = root.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                any.AddCondition(AnimatorConditionMode.Equals, i + 1, "AttackVariant");
                any.duration = 0.05f;
                any.canTransitionToSelf = false;

                // Выход обязателен. Без него герой залипает в позе удара:
                // цель умерла, бить больше некого, а состояние держится —
                // Павлон 01.09.2026: «кабан умер, а персонаж так и остался
                // согнутым». Вход из Any State сам по себе не возвращает
                // никуда, состояние обязано выйти по концу клипа.
                var back = state.AddExitTransition();
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.1f;

                added++;
            }

            // Вздрагивание. Не прерывает удар: попадание по герою в момент
            // его замаха не должно отменять этот замах — иначе в плотном бою
            // герой перестаёт бить вовсе.
            added += OneShot(controller, root, GetHitClip, "Hit", "GetHit", 0.06f);

            // Уклонение.
            added += OneShot(controller, root, DodgeClip, "Dodge", "Dodge", 0.05f);

            // Жесты способностей.
            foreach (var cast in Casts)
                added += OneShot(controller, root, cast.clip, cast.trigger, cast.state, 0.05f);

            // Метание кинжала — новая способность, заведена 04.09.2026.
            //
            // Клип не из ExplosiveLLC, а из DoubleL: у первого метания нет
            // вовсе, у второго оно есть отдельным действием. Поэтому путь
            // абсолютный, а не от корня набора.
            added += OneShot(controller, root, ThrowClip, "Throw", "Throw", 0.05f);

            // Крадущийся шаг.
            added += Sneak(controller, root);

            // Прыжок фазами.
            added += JumpPhases(controller, root);

            // Возврат из смерти: у героя воскрешение по кнопке, и без этого
            // перехода он встал бы и пошёл лёжа.
            if (EnsureRevive(root))
            {
                Debug.Log("[IsoRPG] Герой: добавлен выход из смерти.");
                added++;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Бой героя: добавлено состояний " + added +
                      " (ударов в серии " + Attacks.Length + ").");

            Check(controller);
        }

        /// <summary>
        /// Прыжок фазами: отрыв, зависание, приземление.
        ///
        /// Было одно состояние на весь прыжок, и получалось вот что: клип
        /// короче самого прыжка, он доигрывает, аниматор возвращается в ход —
        /// и герой перебирает ногами, вися в воздухе. А приземление вообще
        /// нечем показать, поэтому он встаёт на прямые ноги без амортизации.
        /// Павлон 01.09.2026: «первая фаза нормально, во второй он ногами
        /// двигает так же, как бежит, и приземляется без инерции».
        ///
        /// Фазами это лечится ровно потому, что зависание ЗАЦИКЛЕНО: сколько
        /// бы герой ни висел, он висит, а не бежит. Выход из него по флагу от
        /// прыжка, а не по времени клипа.
        /// </summary>
        private static int JumpPhases(AnimatorController controller, AnimatorStateMachine root)
        {
            // Пересобираем фазы ВСЕГДА, а не пропускаем готовые.
            //
            // Ранний выход означал, что любая правка клипов и переходов не
            // доходит до уже собранного контроллера: он ассет, и живёт
            // сам по себе. На боссе это стоило вечера — там состояние
            // смерти осталось без выхода, хотя в коде выход был.
            foreach (var name in new[] { "Jump_Start", "Jump_Air", "Jump_Land" })
            {
                var stale = Find(root, name);
                if (stale != null) root.RemoveState(stale);
            }

            const string Base = "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine/InAir/";

            var takeOff = LoadAny(Base + "A_MOD_BL_Jump_Idle_Masc.fbx");
            var air = LoadAny(Base + "A_MOD_BL_InAir_FallShort_Masc.fbx");
            // Приземление берём СРЕДНЕЙ жёсткости, а не мягкое: в мягком
            // герой встаёт на прямые ноги, и падение не читается вовсе.
            // Павлон 01.09.2026: «при приземлении должна быть инерция, он
            // должен немного приседать». В наборе три степени — Soft,
            // Medium, Hard; Medium даёт приседание, не превращая прыжок
            // через кочку в падение с крыши.
            var land = LoadAny(Base + "A_MOD_BL_Land_IdleMedium_Masc.fbx")
                       ?? LoadAny(Base + "A_MOD_BL_Land_IdleSoft_Masc.fbx");

            if (takeOff == null || air == null || land == null)
            {
                Debug.LogError("[IsoRPG] Нет клипов прыжка — фазы не собраны.");
                return 0;
            }

            EnsureParameter(controller, "InAir", AnimatorControllerParameterType.Bool);

            var start = root.AddState("Jump_Start");
            start.motion = takeOff;

            var flight = root.AddState("Jump_Air");
            flight.motion = air;

            var landing = root.AddState("Jump_Land");
            landing.motion = land;

            // Вход: по тому же триггеру, что и раньше — боевой код и
            // JumpGesture ничего не заметят.
            var into = root.AddAnyStateTransition(start);
            into.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            into.duration = 0.05f;
            into.canTransitionToSelf = false;

            // Отрыв → зависание по концу клипа.
            var up = start.AddTransition(flight);
            up.hasExitTime = true;
            up.exitTime = 0.85f;
            up.duration = 0.1f;

            // Зависание → приземление, когда прыжок кончился. По флагу, а не
            // по времени: высота и длительность прыжка живут в JumpGesture, и
            // анимация обязана следовать за ними, а не наоборот.
            // Приземление в стойку — только если герой ОСТАНОВИЛСЯ.
            //
            // Иначе выходило странное: клип приземления рассчитан на стойку,
            // а мотор в это время продолжает вести героя — и он ехал по
            // земле в позе приземления, а потом рывком переходил на бег.
            // На ходу приземление пропускаем: бег сам по себе выглядит
            // продолжением прыжка.
            var down = flight.AddTransition(landing);
            down.AddCondition(AnimatorConditionMode.IfNot, 0f, "InAir");
            down.hasExitTime = false;
            down.duration = 0.08f;

            // Приземление → ход.
            var back = landing.AddExitTransition();
            back.hasExitTime = true;
            back.exitTime = 0.8f;
            back.duration = 0.12f;

            // На бегу приземление играется БЫСТРЕЕ, а не обрезается.
            //
            // Обрезание теряло часть анимации: Павлон 01.09.2026 — «ты там
            // что-то обрезал, а надо было просто ускорять фазу приземления
            // и разгибания, потому что сейчас персонаж едет по земле».
            // Ускорение сохраняет обе фазы — присед и разгибание, — просто
            // они проходят быстрее, и ехать уже некогда.
            EnsureParameter(controller, "LandSpeed", AnimatorControllerParameterType.Float);
            landing.speedParameterActive = true;
            landing.speedParameter = "LandSpeed";

            // Старое одиночное состояние больше не нужно: вход в него шёл по
            // тому же триггеру, и два перехода на один триггер дрались бы
            // между собой.
            var old = Find(root, "Jump");

            if (old != null)
            {
                root.RemoveState(old);
                Debug.Log("[IsoRPG] Старое состояние прыжка убрано — заменено фазами.");
            }

            return 3;
        }

        /// <summary>
        /// Крадущийся шаг для скрытности.
        ///
        /// Отдельным состоянием с деревом смешивания по скорости: стоя —
        /// присед, в движении — крадущийся шаг. Одним клипом не обойтись,
        /// иначе герой либо скользит в позе сидя, либо семенит на месте.
        ///
        /// Переход туда-обратно по флагу, а не по триггеру: скрытность — это
        /// состояние, в котором находятся, а не действие, которое играют.
        /// </summary>
        private static int Sneak(AnimatorController controller, AnimatorStateMachine root)
        {
            if (Find(root, "Sneak") != null) return 0;

            const string Base = "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine/";

            var idle = LoadAny(Base + "Idles/A_MOD_BL_Idle_Crouching_Masc.fbx");
            var walk = LoadAny(Base + "Locomotion/Crouch/A_MOD_BL_Crouch_FwdStrafeF_Masc.fbx");

            if (idle == null || walk == null)
            {
                Debug.LogError("[IsoRPG] Нет клипов приседания — скрытность останется без анимации.");
                return 0;
            }

            EnsureParameter(controller, "Sneaking", AnimatorControllerParameterType.Bool);

            var tree = new BlendTree
            {
                name = "SneakBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 2.2f);

            var state = root.AddState("Sneak");
            state.motion = tree;

            var locomotion = Find(root, "Locomotion");

            if (locomotion != null)
            {
                var into = locomotion.AddTransition(state);
                into.AddCondition(AnimatorConditionMode.If, 0f, "Sneaking");
                into.hasExitTime = false;
                into.duration = 0.18f;

                var outOf = state.AddTransition(locomotion);
                outOf.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sneaking");
                outOf.hasExitTime = false;
                outOf.duration = 0.18f;
            }
            else Debug.LogWarning("[IsoRPG] Нет состояния Locomotion — переход в присед не связан.");

            return 1;
        }

        private static AnimationClip LoadAny(string path)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null) Debug.LogError("[IsoRPG] Нет клипа " + path);

            return clip;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Вздрагивание зверям. Вопрос Павлона 01.09.2026: «а у кабанов и
        /// волков есть эффекты вздрагивания от ударов?» — оказалось, клипы у
        /// обоих наборов есть, просто в контроллеры их не завели. Без этого
        /// бой односторонний: герой дёргается от попаданий, а зверь стоит
        /// столбом и получает урон как мешок.
        /// </summary>
        [MenuItem("Tools/IsoRPG/Звери: вздрагивание от ударов", priority = 35)]
        public static void Beasts()
        {
            var list = new (string controller, string clip, string who)[]
            {
                ("Assets/_Game/Art/Animations/Controllers/AC_Wolf.controller",
                 "Assets/Polygonal Wolf/FBX/Polygonal Wolf@Take Damage.FBX", "волк"),

                ("Assets/_Game/Art/Animations/Controllers/AC_Boar.controller",
                 "Assets/Malbers Animations/Animals Packs/01 Forest Pack/Boar/Anims/Boar Get Hit.FBX", "кабан"),

                ("Assets/_Game/Art/Animations/Controllers/AC_BoarBoss.controller",
                 "Assets/Malbers Animations/Animals Packs/01 Forest Pack/Boar/Anims/Boar Get Hit.FBX", "вожак"),
            };

            int done = 0;

            foreach (var (path, clipPath, who) in list)
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

                if (controller == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет контроллера " + path + " — " + who + " без вздрагивания.");
                    continue;
                }

                var root = controller.layers[0].stateMachine;

                // Возврат из смерти — раньше вздрагивания: без него
                // возрождённый зверь бегает за героем лёжа.
                if (EnsureRevive(root))
                {
                    Debug.Log("[IsoRPG] " + who + ": добавлен выход из смерти.");
                    EditorUtility.SetDirty(controller);
                }

                if (Find(root, "GetHit") != null) { done++; continue; }

                var clip = AssetDatabase.LoadAllAssetsAtPath(clipPath)
                                        .OfType<AnimationClip>()
                                        .FirstOrDefault(c => !c.name.StartsWith("__preview"));

                if (clip == null)
                {
                    Debug.LogError("[IsoRPG] Нет клипа " + clipPath);
                    continue;
                }

                EnsureParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);

                var state = root.AddState("GetHit");
                state.motion = clip;

                var any = root.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                any.duration = 0.06f;
                any.canTransitionToSelf = false;

                var back = state.AddExitTransition();
                back.hasExitTime = true;
                back.exitTime = 0.8f;
                back.duration = 0.12f;

                EditorUtility.SetDirty(controller);
                done++;

                Debug.Log("[IsoRPG] Вздрагивание добавлено: " + who +
                          " («" + clip.name + "», " + clip.length.ToString("0.00") + " с).");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[IsoRPG] Звери со вздрагиванием: " + done + " из " + list.Length + ".");
        }

        /// <summary>
        /// Одиночное действие: состояние с клипом и вход по триггеру из
        /// любого места. Возвращает 1, если добавили, и 0, если уже было.
        /// </summary>
        private static int OneShot(AnimatorController controller, AnimatorStateMachine root,
                                   string clipPath, string trigger, string stateName,
                                   float blend)
        {
            var already = Find(root, stateName);

            if (already != null)
            {
                EnsureExit(already);
                return 0;
            }

            var clip = LoadClip(clipPath);
            if (clip == null) return 0;

            var state = root.AddState(stateName);
            state.motion = clip;

            var any = root.AddAnyStateTransition(state);
            any.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            any.duration = blend;
            any.canTransitionToSelf = false;

            // Возврат в ход по концу клипа. Без выхода состояние залипает, и
            // герой остаётся стоять в позе вздрагивания насовсем.
            var back = state.AddExitTransition();
            back.hasExitTime = true;
            back.exitTime = 0.85f;

            return 1;
        }

        /// <summary>
        /// Убедиться, что из состояния есть выход по концу клипа.
        ///
        /// Вход из Any State сам по себе никуда не возвращает: состояние
        /// доигрывает клип и держит последнюю позу навсегда. Для одиночных
        /// действий это всегда ошибка.
        /// </summary>
        /// <summary>
        /// Вернуть из смерти в ход, когда флаг снят.
        ///
        /// Состояние Death у всех строилось «в одну сторону»: вход по флагу
        /// Dead из любого места и никакого выхода. Пока никто не воскресал,
        /// это было незаметно. С возрождателем вылезло сразу: моб оживает,
        /// бежит за героем — и всё это лёжа, потому что аниматор так и стоит
        /// в клипе смерти. Павлон 01.09.2026: «кабан босс которого я убил
        /// мёртвый ездит по земле за мной».
        /// </summary>
        private static bool EnsureRevive(AnimatorStateMachine root)
        {
            var death = Find(root, "Death");
            var locomotion = Find(root, "Locomotion");

            if (death == null || locomotion == null) return false;

            foreach (var t in death.transitions)
                if (t.destinationState == locomotion) return false;

            var back = death.AddTransition(locomotion);
            back.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            back.hasExitTime = false;
            back.duration = 0.2f;

            return true;
        }

        private static void EnsureExit(AnimatorState state)
        {
            foreach (var t in state.transitions)
                if (t.isExit) return;

            var back = state.AddExitTransition();
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.1f;

            Debug.Log("[IsoRPG] «" + state.name + "»: добавлен выход — состояние залипало.");
        }

        private static void EnsureParameter(AnimatorController controller, string name,
                                            AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(p => p.name == name)) return;
            controller.AddParameter(name, type);
        }

        private static AnimatorState Find(AnimatorStateMachine root, string name)
        {
            foreach (var child in root.states)
                if (child.state != null && child.state.name == name) return child.state;

            return null;
        }

        /// <summary>Клип лежит подобъектом внутри FBX — берём его оттуда.</summary>
        /// <summary>Метание: единственный клип не из этого набора, потому путь целиком.</summary>
        private const string ThrowClip =
            "Assets/DoubleL/FBX_Animations/Actions/Item/Item_Throw_InPlace.fbx";

        private static AnimationClip LoadClip(string relative)
        {
            // Путь, начинающийся с Assets, берём как есть: клипы приходят из
            // разных наборов, и склейка с корнем годится только для своего.
            string path = relative.StartsWith("Assets/") ? relative : Pack + relative;

            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null)
                Debug.LogError("[IsoRPG] Нет клипа " + path);

            return clip;
        }

        /// <summary>Щуп: спрашиваем сам контроллер, а не журнал.</summary>
        private static void Check(AnimatorController controller)
        {
            var root = controller.layers[0].stateMachine;
            var names = new List<string>();

            foreach (var child in root.states)
                if (child.state != null) names.Add(child.state.name);

            int attacks = names.Count(n => n.StartsWith("Attack_"));

            Debug.Log("[IsoRPG] В контроллере состояний " + names.Count + ": " +
                      string.Join(", ", names));

            if (attacks < 2)
                Debug.LogError("[IsoRPG] Серии ударов НЕТ — вариантов " + attacks + ".");

            foreach (var must in new[] { "GetHit", "Dodge", "Cast_Attack", "Cast_AOE", "Cast_Buff" })
                if (!names.Contains(must))
                    Debug.LogError("[IsoRPG] Нет состояния " + must + ".");
        }
    }
}
