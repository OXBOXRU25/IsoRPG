using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Оживляет НПС: вместо одной стойки — несколько, и он их чередует.
    ///
    /// До 02.09.2026 у Талина был ровно ОДИН клип на всё время игры, и
    /// контроллер состоял из одного состояния. Со стороны это читается как
    /// манекен: человек стоит и не дышит.
    ///
    /// Первая проба нового набора анимаций (RPG Animations Pack) — на НПС, а
    /// не на герое. Решение Павлона, и оно верное: у НПС нет боя, ходьбы и
    /// физики, значит пробуем ровно одно — как выглядит чужая пластика на
    /// нашем персонаже. Не понравится — откат в одну строку, боевой код при
    /// этом не тронут вовсе.
    ///
    /// Нового кода не пишем совсем: чередование занятий уже сделано зверям
    /// сегодня же (<see cref="IsoRPG.Combat.IdleBehaviour"/>), и правило у него
    /// общее — входное состояние занятия зовётся `Rest_N`. НПС просто попадает
    /// под то же правило.
    /// </summary>
    public static class NpcIdleKit
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        private const string ControllerPath =
            "Assets/_Game/Art/Animations/Controllers/AC_Npc_Idle.controller";

        private const string Pack = "Assets/DoubleL/FBX_Animations/NPC";

        /// <summary>
        /// Что чередует НПС. Первый — основная стойка, остальные заходят
        /// изредка и ненадолго.
        ///
        /// Павлон 02.09.2026 уточнил разделение, и оно верное: махание — это
        /// СОБЫТИЕ первой встречи, а не занятие. НПС, машущий рукой сам себе
        /// каждые полминуты, читается как заводная игрушка. В цикле остаётся
        /// то, что человек делает наедине с собой: переминается, утирает лоб,
        /// думает. Махание и жестикуляцию ведёт
        /// <see cref="IsoRPG.World.NpcGesture"/>.
        /// </summary>
        private static readonly (string Folder, string Clip)[] Occupations =
        {
            ("Standing", "Standing_Idle_2"),
            ("Standing", "Standing_Idle_3"),
            ("Standing", "Standing_Idle_4"),
            ("Wipe Forehead", "Wipe_Forehead"),
            ("Think", "Think_All"),
        };

        /// <summary>Основная стойка: к ней НПС возвращается между занятиями.</summary>
        private static readonly (string Folder, string Clip) Base = ("Standing", "Standing_Idle_1");

        /// <summary>Приветствие при первой встрече.</summary>
        private static readonly (string Folder, string Clip) Greeting = ("Waving Hello", "Waving_Hello");

        /// <summary>
        /// Сколько жестов разговора завести. У набора их 54, берём все.
        ///
        /// Соблазн взять пять и не раздувать контроллер надо гасить: разговор
        /// с НПС игрок ведёт десятки раз, и пять жестов по кругу он запомнит
        /// на третьем. Состояния в аниматоре стоят дёшево, повтор — дорого.
        /// </summary>
        private const int Gestures = 60;

        /// <summary>Сколько жестов реально собралось. Их и получает компонент.</summary>
        private static int builtGestures;

        [MenuItem("Tools/IsoRPG/НПС: живые стойки ожидания", priority = 44)]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            var controller = BuildController();
            if (controller == null) return;

            // Задание обязано САМО открывать нужную сцену: пакетный запуск
            // оставляет открытой ту, что была.
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            int given = 0;

            foreach (var giver in Object.FindObjectsByType<IsoRPG.Quests.QuestGiver>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var animator = giver.GetComponentsInChildren<Animator>(true)
                                    .FirstOrDefault(a => a.avatar != null)
                               ?? giver.GetComponentInChildren<Animator>(true);

                if (animator == null)
                {
                    Debug.LogWarning($"[IsoRPG] У «{giver.name}» нет аниматора.");
                    continue;
                }

                // Пересобранный контроллер — новый ассет: ссылку в сцене надо
                // переставить, иначе она повиснет на удалённом.
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                EditorUtility.SetDirty(animator);

                var idle = giver.GetComponent<IsoRPG.Combat.IdleBehaviour>();
                if (idle == null) idle = giver.gameObject.AddComponent<IsoRPG.Combat.IdleBehaviour>();

                idle.SetKinds(Enumerable.Range(1, Occupations.Length).ToArray());

                // У НПС занятия короткие и частые: он не спит и не ест, он
                // просто живой. У зверя наоборот — лёг и лежит.
                idle.SetTiming(new Vector2(4f, 11f), new Vector2(3f, 7f));

                EditorUtility.SetDirty(idle);

                // Махание при первой встрече и жестикуляция в разговоре.
                var gesture = giver.GetComponent<IsoRPG.World.NpcGesture>();
                if (gesture == null) gesture = giver.gameObject.AddComponent<IsoRPG.World.NpcGesture>();

                gesture.SetGestures(builtGestures);
                EditorUtility.SetDirty(gesture);

                given++;
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[IsoRPG] Живые стойки розданы НПС: {given}. " +
                      $"Занятий вне разговора {Occupations.Length} (переминание, утирает лоб, " +
                      $"задумался), приветствие при первой встрече, жестов разговора {builtGestures}.");
        }

        // ------------------------------------------------------------------

        private static AnimatorController BuildController()
        {
            var idle = Clip(Base.Folder, Base.Clip);

            if (idle == null)
            {
                Debug.LogError("[IsoRPG] Основная стойка НПС не нашлась — контроллер не собран.");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Rest", AnimatorControllerParameterType.Int);
            controller.AddParameter("Greet", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Talking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("TalkVariant", AnimatorControllerParameterType.Int);

            var machine = controller.layers[0].stateMachine;

            var stand = machine.AddState("Idle");
            stand.motion = idle;
            machine.defaultState = stand;

            // --- приветствие ---------------------------------------------
            //
            // Из любого состояния: игрок может подойти, пока НПС утирает лоб,
            // и махнуть он всё равно должен.
            var hello = Clip(Greeting.Folder, Greeting.Clip);

            if (hello != null)
            {
                var greet = machine.AddState("Greet");
                greet.motion = hello;

                var any = machine.AddAnyStateTransition(greet);
                any.AddCondition(AnimatorConditionMode.If, 0f, "Greet");
                any.duration = 0.2f;
                any.canTransitionToSelf = false;

                var done = greet.AddTransition(stand);
                done.hasExitTime = true;
                done.exitTime = 0.9f;
                done.duration = 0.25f;
            }

            // --- жестикуляция в разговоре ---------------------------------
            //
            // Держится, пока открыто окно; номер жеста меняется на ходу, и
            // переход «жест → жест» идёт по несовпадению номера.
            int talks = 0;

            // Номера у автора идут с дырами: 31 — не жест, а сценка из фаз
            // (Start, Idle_1..7, End), а 47..54 нет вовсе. Поэтому состояния
            // нумеруем ПОДРЯД по факту, а не по исходному номеру: иначе
            // жеребьёвка попадала бы в пустоту на каждой дыре.
            for (int i = 1; i <= Gestures; i++)
            {
                var clip = Clip("Dialogue", "Dialogue_" + i, quiet: true);
                if (clip == null) continue;

                var state = machine.AddState("Talk_" + (talks + 1));
                state.motion = clip;

                var any = machine.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If, 0f, "Talking");
                any.AddCondition(AnimatorConditionMode.Equals, talks + 1, "TalkVariant");
                any.duration = 0.25f;
                any.canTransitionToSelf = false;

                // Жест играется ОДИН раз и НПС возвращается в спокойную
                // стойку, оставаясь в разговоре.
                //
                // Павлон 02.09.2026: «они не должны идти циклически одна за
                // другой, выглядит странно, особенно учитывая что у нас нет
                // анимации мимики». Верно: без лица непрерывная жестикуляция
                // читается как тик, а не как речь. Новый жест — при следующем
                // открытии окна.
                var done = state.AddTransition(stand);
                done.hasExitTime = true;
                done.exitTime = 0.92f;
                done.duration = 0.35f;

                var back = state.AddTransition(stand);
                back.hasExitTime = false;
                back.duration = 0.3f;
                back.AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");

                talks++;
            }

            builtGestures = talks;

            int added = 0;

            for (int i = 0; i < Occupations.Length; i++)
            {
                var clip = Clip(Occupations[i].Folder, Occupations[i].Clip);
                if (clip == null) continue;

                int kind = i + 1;

                var state = machine.AddState("Rest_" + kind);
                state.motion = clip;

                var into = stand.AddTransition(state);
                into.hasExitTime = false;
                into.duration = 0.3f;
                into.AddCondition(AnimatorConditionMode.Equals, kind, "Rest");

                var back = state.AddTransition(stand);
                back.hasExitTime = false;
                back.duration = 0.3f;
                back.AddCondition(AnimatorConditionMode.NotEqual, kind, "Rest");

                added++;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[IsoRPG] Контроллер НПС собран: стойка, {added} занятий, " +
                      $"{talks} жестов разговора, приветствие {(hello != null ? "есть" : "НЕ НАЙДЕНО")}.");

            return controller;
        }

        /// <summary>
        /// Клип из набора. Берём версию БЕЗ приставки `_InPlace`, если её нет:
        /// у стоек ожидания корневого движения и так нет, а у автора приставка
        /// стоит только там, где клип везёт персонажа.
        /// </summary>
        private static AnimationClip Clip(string folder, string name, bool quiet = false)
        {
            string path = Pack + "/" + folder + "/" + name + ".fbx";

            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null && !quiet) Debug.LogWarning("[IsoRPG] Клип НПС не найден: " + path);

            return clip;
        }
    }
}
