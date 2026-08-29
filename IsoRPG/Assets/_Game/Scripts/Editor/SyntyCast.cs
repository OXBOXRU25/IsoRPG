using System.Collections.Generic;
using System.IO;
using System.Linq;
using IsoRPG.Dev;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Живая витрина персонажей Synty: они ходят, а не стоят.
    ///
    /// Стоячая фигура ничего не говорит о том, годится ли существо: половина
    /// впечатления — походка. Поэтому здесь персонажи получают
    /// навигационного агента, контроллер анимаций и бродячее поведение —
    /// расходятся, останавливаются, идут дальше.
    ///
    /// Контроллер собирается на месте из купленного набора Goblin
    /// Locomotion. Это возможно только потому, что у персонажей Synty риг
    /// **Humanoid**: клипы, снятые с чужого скелета, ложатся на них
    /// ретаргетом. У наших KayKit риг Generic, и там такой фокус не прошёл
    /// бы — пришлось бы искать анимации именно под их скелет.
    ///
    /// Ставятся рядом с залом, чтобы дойти пешком, и убираются одним
    /// пунктом меню.
    /// </summary>
    public static class SyntyCast
    {
        private const string HolderName = "SyntyCast";
        private const string ControllerPath = "Assets/_Game/Data/SyntyLocomotion.controller";

        private const string Anim = "Assets/Synty/AnimationGoblinLocomotion/Animations/Polygon/Neutral";

        private const string IdleClip = Anim + "/Idles/A_POLY_GBL_Idle_Standing_Neut.fbx";
        private const string WalkClip = Anim + "/Locomotion/Walk/A_POLY_GBL_Walk_F_Neut.fbx";
        private const string RunClip  = Anim + "/Locomotion/Run/A_POLY_GBL_Run_F_Neut.fbx";

        /// <summary>
        /// Кого показываем. Взяты те, что ближе к нашей игре: воины, маги,
        /// нежить, ремесленники — и по одному представителю каждого рода,
        /// чтобы разница между ними была видна сразу.
        /// </summary>
        private static readonly string[] Cast =
        {
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_BR_Dwarf_Soldier_Male_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_BR_Dwarf_King_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_BR_Demon_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_Skeleton_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_Undead_Knight_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_Hero_Male_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_Hero_Female_01.prefab",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Characters/Chr_Nomad_Male_01.prefab",
            "Assets/PolygonDungeon/Prefabs/Characters/Character_Skeleton_Knight_FixedScale.prefab",
            "Assets/PolygonDungeon/Prefabs/Characters/Character_Goblin_Shaman_FixedScale.prefab",
            "Assets/PolygonDungeon/Prefabs/Characters/Character_Rock_Golem.prefab",
            "Assets/PolygonDungeon/Prefabs/Characters/Character_Ghost_02_FixedScale.prefab",
        };

        [MenuItem("Tools/IsoRPG/Живая витрина персонажей: собрать", priority = 66)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            Clear();

            var controller = BuildController();

            if (controller == null)
            {
                Debug.LogError("[IsoRPG] Не собрался контроллер — без него персонажи будут стоять.");
                return;
            }

            var holder = new GameObject(HolderName);

            // Ставим к востоку от зала, поодаль: там ровно и никто не мешает.
            Vector3 centre = RuinsLayout.HallCentre + new Vector3(34f, 0f, 0f);

            int placed = 0, missing = 0;

            for (int i = 0; i < Cast.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Cast[i]);

                if (asset == null)
                {
                    Debug.LogWarning("[IsoRPG] Нет персонажа " + Cast[i]);
                    missing++;
                    continue;
                }

                float angle = i * Mathf.PI * 2f / Cast.Length;
                Vector3 at = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 9f;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder.transform);
                go.transform.position = at;
                go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                // Анимация. Аниматор может уже быть в префабе — тогда просто
                // подменяем контроллер, иначе добавляем свой.
                var animator = go.GetComponentInChildren<Animator>();

                if (animator == null) animator = go.AddComponent<Animator>();

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                // Навигация и походка.
                var agent = go.GetComponent<NavMeshAgent>();
                if (agent == null) agent = go.AddComponent<NavMeshAgent>();

                agent.speed = Random.Range(1.6f, 2.6f);
                agent.angularSpeed = 480f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 0.2f;
                agent.radius = 0.45f;
                agent.height = 2f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

                go.AddComponent<Wanderer>();

                placed++;
            }

            Rebake();

            Selection.activeGameObject = holder;

            Debug.Log("[IsoRPG] Живая витрина: персонажей " + placed +
                      (missing > 0 ? ", не найдено " + missing : "") +
                      ". Стоят к востоку от зала, в " + centre + " — ходят сами.");
        }

        [MenuItem("Tools/IsoRPG/Живая витрина персонажей: убрать", priority = 67)]
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == HolderName);

            if (old != null) Object.DestroyImmediate(old);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Собирает контроллер: стойка, шаг, бег в одном дереве смешивания
        /// по скорости.
        ///
        /// Пороги — это скорости, на которых существо ЕЗДИТ, а не красивые
        /// круглые числа. Ошибиться здесь значит получить ноги, скользящие
        /// по земле: мы это уже проходили с упырями, где клип бега играл на
        /// скорости, которой у агента никогда не было.
        /// </summary>
        private static AnimatorController BuildController()
        {
            var idle = Clip(IdleClip);
            var walk = Clip(WalkClip);
            var run = Clip(RunClip);

            if (idle == null || walk == null || run == null)
            {
                Debug.LogError("[IsoRPG] Не нашёл клипы: " +
                               (idle == null ? "стойка " : "") +
                               (walk == null ? "шаг " : "") +
                               (run == null ? "бег" : ""));
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
            AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendParameter = "Speed",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 2f);
            tree.AddChild(run, 5.5f);

            var state = controller.layers[0].stateMachine.AddState("Locomotion");
            state.motion = tree;

            controller.layers[0].stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return controller;
        }

        /// <summary>
        /// Достаёт клип из FBX и зацикливает его.
        ///
        /// Зацикливание обязательно: незацикленный шаг играет один раз и
        /// застывает, пока агент везёт тело дальше — существо «едет по
        /// земле». На этом мы уже обожглись с BitGem, там все клипы приезжали
        /// незацикленными.
        /// </summary>
        private static AnimationClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null) return null;

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer != null)
            {
                var clips = importer.defaultClipAnimations;

                if (clips.Length > 0 && !clips[0].loopTime)
                {
                    clips[0].loopTime = true;
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();

                    clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                        .OfType<AnimationClip>()
                                        .FirstOrDefault(c => !c.name.StartsWith("__preview"));
                }
            }

            return clip;
        }

        private static void Rebake() => NavBake.Rebake();
    }
}
