using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает контроллеры анимаций и префабы персонажей из набора KayKit.
    ///
    /// Контроллеры строятся с теми же параметрами, что и прежний: Speed,
    /// Attack, StealthKill, Dead, AttackSpeed. Это не формальность — весь
    /// боевой код обращается к аниматору через них, и совпадение имён
    /// означает, что смена набора моделей не требует правок в бою вообще.
    /// </summary>
    public static class KayKitCharacters
    {
        private const string AnimationsFolder = "Assets/_Game/Art/KayKit/Animations";
        private const string CharactersFolder = "Assets/_Game/Art/KayKit/Characters";
        private const string ControllersFolder = "Assets/_Game/Art/KayKit/Controllers";
        private const string PrefabsFolder = "Assets/_Game/Prefabs";

        // Скорости переключения походки. Должны совпадать со скоростями
        // навигационного агента, иначе ноги поедут по земле.
        private const float WalkSpeed = 2f;
        private const float RunSpeed = 5.5f;

        private const float TargetActionDuration = 1.3f;
        private const float MaxSpeedUp = 1.5f;
        private const float MoveInterruptSpeed = 2.5f;
        private const float InterruptGuard = 0.15f;

        /// <summary>
        /// Роли клипов для каждого вида существ. Живой человек и поднятая
        /// нежить двигаются по-разному, и у набора для этого есть отдельные
        /// клипы — брать общие значило бы выбросить работу автора.
        /// </summary>
        private struct AnimSet
        {
            public string idle, walk, run, attack, stealth, death;
        }

        private static readonly AnimSet RogueSet = new AnimSet
        {
            idle = "Idle_A",
            walk = "Walking_A",
            run = "Running_A",
            attack = "Melee_Dualwield_Attack_Slice",
            stealth = "Melee_Dualwield_Attack_Stab",
            death = "Death_A",
        };

        private static readonly AnimSet SkeletonSet = new AnimSet
        {
            idle = "Skeletons_Idle",
            walk = "Skeletons_Walking",
            run = "Running_A",
            attack = "Melee_1H_Attack_Chop",
            stealth = "Melee_1H_Attack_Stab",
            death = "Skeletons_Death",
        };

        /// <summary>
        /// Лучник. Отличается не только атакой: у него и покой другой —
        /// с натянутым луком, а не с опущенными руками. Без этого скелет
        /// держит лук как палку и машет им в бою.
        /// </summary>
        private static readonly AnimSet ArcherSet = new AnimSet
        {
            idle = "Ranged_Bow_Idle",
            walk = "Skeletons_Walking",
            run = "Running_A",
            attack = "Ranged_Bow_Draw",
            stealth = "Ranged_Bow_Release",
            death = "Skeletons_Death",
        };

        /// <summary>
        /// Костяной маг. Бьёт не палкой, а заклинанием: у набора есть
        /// полный цикл магии, и посох в руках без него читается как
        /// дубина, которой почему-то машут издалека.
        /// </summary>
        private static readonly AnimSet MageSet = new AnimSet
        {
            idle = "Skeletons_Idle",
            walk = "Skeletons_Walking",
            run = "Running_A",
            attack = "Ranged_Magic_Shoot",
            stealth = "Ranged_Magic_Spellcasting",
            death = "Skeletons_Death",
        };

        /// <summary>
        /// Живые разбойники. Дышат и стоят иначе, чем нежить: у скелетов
        /// свой набор покоя и ходьбы, и отдать его людям значит получить
        /// живого человека с повадками мертвеца.
        /// </summary>
        private static readonly AnimSet BanditSet = new AnimSet
        {
            idle = "Idle_A",
            walk = "Walking_A",
            run = "Running_A",
            attack = "Melee_1H_Attack_Chop",
            stealth = "Melee_1H_Attack_Stab",
            death = "Death_B",
        };

        /// <summary>Лучник-человек: покой с натянутым луком.</summary>
        private static readonly AnimSet HunterSet = new AnimSet
        {
            idle = "Ranged_Bow_Idle",
            walk = "Walking_A",
            run = "Running_A",
            attack = "Ranged_Bow_Draw",
            stealth = "Ranged_Bow_Release",
            death = "Death_B",
        };

        /// <summary>Кого собираем: модель, набор анимаций, имя префаба.</summary>
        private static readonly (string model, string set, string prefab)[] Roster =
        {
            ("Rogue_Hooded",     "Rogue",    "Player"),
            ("Skeleton_Warrior", "Skeleton", "Skeleton_Warrior"),
            ("Skeleton_Rogue",   "Archer",   "Skeleton_Rogue"),
            ("Skeleton_Minion",  "Skeleton", "Skeleton_Minion"),
            ("Skeleton_Mage",    "Mage",     "Skeleton_Mage"),
            ("Barbarian",        "Bandit",   "Bandit_Brute"),
            ("Knight",           "Bandit",   "Bandit_Guard"),
            ("Ranger",           "Hunter",   "Bandit_Hunter"),
        };

        [MenuItem("Tools/IsoRPG/Собрать персонажей KayKit", priority = 13)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play ассеты не сохраняются на диск.", "Понятно");
                return;
            }

            var clips = CollectClips();
            if (clips.Count == 0)
            {
                Debug.LogError("[IsoRPG] В " + AnimationsFolder + " не нашлось ни одного клипа. " +
                               "Сначала прогони «Подготовить набор KayKit».");
                return;
            }

            EnsureFolder(ControllersFolder);
            EnsureFolder(PrefabsFolder);

            var rogue = BuildController("AC_Rogue", RogueSet, clips);
            var skeleton = BuildController("AC_Skeleton", SkeletonSet, clips);
            var archer = BuildController("AC_SkeletonArcher", ArcherSet, clips);
            var mage = BuildController("AC_SkeletonMage", MageSet, clips);
            var bandit = BuildController("AC_Bandit", BanditSet, clips);
            var hunter = BuildController("AC_Hunter", HunterSet, clips);

            // Контроллеры должны лечь в базу до того, как их попросит префаб:
            // ассет, созданный и загруженный в одном кадре, отдаётся пустой
            // ссылкой, и префаб молча выйдет без анимаций.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int made = 0;

            foreach (var (model, set, prefab) in Roster)
            {
                var controller = set == "Rogue" ? rogue
                               : set == "Archer" ? archer
                               : set == "Mage" ? mage
                               : set == "Bandit" ? bandit
                               : set == "Hunter" ? hunter
                               : skeleton;
                if (BuildPrefab(model, controller, prefab)) made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] KayKit: клипов найдено " + clips.Count +
                      ", префабов собрано " + made + " из " + Roster.Length + ".");
        }

        // ------------------------------------------------------------------

        private static Dictionary<string, AnimationClip> CollectClips()
        {
            var found = new Dictionary<string, AnimationClip>();

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { AnimationsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview")) continue;
                    if (!found.ContainsKey(clip.name)) found[clip.name] = clip;
                }
            }

            return found;
        }

        private static AnimatorController BuildController(string name, AnimSet set,
                                                          Dictionary<string, AnimationClip> clips)
        {
            string path = ControllersFolder + "/" + name + ".controller";

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("StealthKill", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Eating", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "AttackSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            });

            var root = controller.layers[0].stateMachine;

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendParameter = "Speed",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            AddMotion(tree, clips, set.idle, 0f, name);
            AddMotion(tree, clips, set.walk, WalkSpeed, name);
            AddMotion(tree, clips, set.run, RunSpeed, name);

            var move = root.AddState("Locomotion");
            move.motion = tree;
            root.defaultState = move;

            AddOneShot(root, move, clips, set.attack, "Attack", "Attack", name);
            AddOneShot(root, move, clips, set.stealth, "StealthKill", "StealthKill", name);
            AddDeath(root, move, clips, set.death, name);
            AddSitting(root, move, clips);
            AddJump(root, move, clips);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddMotion(BlendTree tree, Dictionary<string, AnimationClip> clips,
                                      string key, float threshold, string owner)
        {
            if (!clips.TryGetValue(key, out var clip))
            {
                Debug.LogError("[IsoRPG] " + owner + ": нет клипа «" + key + "» — состояние пропущено.");
                return;
            }

            tree.AddChild(clip, threshold);
        }

        private static void AddOneShot(AnimatorStateMachine root, AnimatorState returnTo,
                                       Dictionary<string, AnimationClip> clips,
                                       string key, string trigger, string stateName, string owner)
        {
            if (!clips.TryGetValue(key, out var clip))
            {
                Debug.LogError("[IsoRPG] " + owner + ": нет клипа «" + key + "» — действие " +
                               trigger + " не собрано.");
                return;
            }

            var state = root.AddState(stateName);
            state.motion = clip;

            // Ритм боя задаёт оружие, а не длина клипа: множитель ставит
            // боевая система в рантайме.
            state.speedParameterActive = true;
            state.speedParameter = "AttackSpeed";

            if (clip.length > TargetActionDuration)
            {
                float needed = clip.length / TargetActionDuration;
                state.speed = Mathf.Min(needed, MaxSpeedUp);

                Debug.Log("[IsoRPG] " + owner + " «" + key + "»: " + clip.length.ToString("0.00") +
                          " с, ускорение " + state.speed.ToString("0.0") + ".");
            }

            var enter = returnTo.AddTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = 0.05f;

            var exit = state.AddTransition(returnTo);
            exit.hasExitTime = true;
            exit.exitTime = 0.8f;
            exit.duration = 0.12f;

            // Побежал — удар прерывается, но не раньше защитного окна:
            // сглаженная скорость падает не мгновенно, и без него состояние
            // вылетает в том же кадре, в котором вошло.
            var interrupt = state.AddTransition(returnTo);
            interrupt.AddCondition(AnimatorConditionMode.Greater, MoveInterruptSpeed, "Speed");
            interrupt.hasExitTime = true;
            interrupt.exitTime = InterruptGuard;
            interrupt.duration = 0.1f;
        }

        /// <summary>
        /// Прыжок по пробелу. Жест, а не механика: даёт ровно то, что
        /// человек ожидает получить, нажав пробел, и ничего сверх.
        /// </summary>
        private static void AddJump(AnimatorStateMachine root, AnimatorState returnTo,
                                    Dictionary<string, AnimationClip> clips)
        {
            if (!clips.TryGetValue("Jump_Full_Short", out var clip)) return;

            var state = root.AddState("Jump");
            state.motion = clip;

            var toJump = root.AddAnyStateTransition(state);
            toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            toJump.hasExitTime = false;
            toJump.duration = 0.05f;
            toJump.canTransitionToSelf = false;

            // Возврат по времени, а не по условию: прыжок кончается сам.
            var back = state.AddTransition(returnTo);
            back.hasExitTime = true;
            back.exitTime = 0.85f;
            back.duration = 0.1f;
        }

        /// <summary>
        /// Сесть, сидеть, встать. Нужно еде: восемнадцать секунд лечения
        /// у персонажа, стоящего столбом, выглядят как зависшая игра.
        ///
        /// Три клипа вместо одного: без вставания персонаж телепортируется
        /// из позы сидя в позу стоя, и это заметнее, чем кажется, — глаз
        /// ловит подмену позы даже за один кадр.
        /// </summary>
        private static void AddSitting(AnimatorStateMachine root, AnimatorState returnTo,
                                       Dictionary<string, AnimationClip> clips)
        {
            if (!clips.TryGetValue("Sit_Floor_Down", out var down)) return;
            if (!clips.TryGetValue("Sit_Floor_Idle", out var idle)) return;
            if (!clips.TryGetValue("Sit_Floor_StandUp", out var up)) return;

            var sitDown = root.AddState("SitDown");
            sitDown.motion = down;

            var sitIdle = root.AddState("SitIdle");
            sitIdle.motion = idle;

            var standUp = root.AddState("StandUp");
            standUp.motion = up;

            // Садимся ИЗ ДВИЖЕНИЯ, а не из любого состояния.
            //
            // Переход из AnyState выглядел удобнее и был неверен: условие
            // «ест» остаётся истинным всё время еды, поэтому из позы сидя нас
            // тут же выбрасывало обратно в начало усаживания — и персонаж
            // дёргался вставая-садясь, пока не доест. Правило общее: AnyState
            // годится для разового сигнала, а не для флага, который держится.
            var toSit = returnTo.AddTransition(sitDown);
            toSit.AddCondition(AnimatorConditionMode.If, 0f, "Eating");
            toSit.hasExitTime = false;
            toSit.duration = 0.15f;

            var settle = sitDown.AddTransition(sitIdle);
            settle.hasExitTime = true;
            settle.exitTime = 0.9f;
            settle.duration = 0.1f;

            // Встаём и из сидения, и из процесса усаживания: прерваться
            // можно в любой момент, а не только досидев до позы.
            var riseFromIdle = sitIdle.AddTransition(standUp);
            riseFromIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Eating");
            riseFromIdle.hasExitTime = false;
            riseFromIdle.duration = 0.1f;

            var riseFromDown = sitDown.AddTransition(standUp);
            riseFromDown.AddCondition(AnimatorConditionMode.IfNot, 0f, "Eating");
            riseFromDown.hasExitTime = false;
            riseFromDown.duration = 0.1f;

            var back = standUp.AddTransition(returnTo);
            back.hasExitTime = true;
            back.exitTime = 0.8f;
            back.duration = 0.12f;
        }

        private static void AddDeath(AnimatorStateMachine root, AnimatorState returnTo,
                                     Dictionary<string, AnimationClip> clips,
                                     string key, string owner)
        {
            if (!clips.TryGetValue(key, out var clip))
            {
                Debug.LogError("[IsoRPG] " + owner + ": нет клипа смерти «" + key + "».");
                return;
            }

            var state = root.AddState("Death");
            state.motion = clip;

            var toDeath = root.AddAnyStateTransition(state);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            toDeath.hasExitTime = false;
            toDeath.duration = 0.1f;
            toDeath.canTransitionToSelf = false;

            // Выход из смерти. Без него флаг снимается, а аниматор остаётся
            // лежать: возрождённый монстр ходит, дерётся и получает урон,
            // выглядя при этом трупом. Ошибка тем и коварна, что логика
            // работает полностью — не работает только картинка.
            var fromDeath = state.AddTransition(returnTo);
            fromDeath.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            fromDeath.hasExitTime = false;
            fromDeath.duration = 0.15f;
        }

        private static bool BuildPrefab(string modelName, AnimatorController controller, string prefabName)
        {
            string modelPath = CharactersFolder + "/" + modelName + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            if (model == null)
            {
                Debug.LogError("[IsoRPG] Не найдена модель " + modelPath);
                return false;
            }

            if (controller == null)
            {
                Debug.LogError("[IsoRPG] Для " + prefabName + " не собран контроллер.");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = prefabName;

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;

            // Позицию ведёт навигационный агент. С корневым движением
            // анимация тянет персонажа сама, и он уезжает от своего агента.
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabsFolder + "/" + prefabName + ".prefab");
            Object.DestroyImmediate(instance);

            return true;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace(Path.DirectorySeparatorChar, '/');
            string leaf = Path.GetFileName(folder);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
