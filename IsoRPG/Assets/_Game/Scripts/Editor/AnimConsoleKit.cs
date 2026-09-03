using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает все анимации персонажа в консоль разработчика.
    ///
    /// Просьба Павла 04.09.2026 — после того, как выяснилось, что в проекте
    /// не восемнадцать ударов, а больше тысячи клипов: «нам нужно что-то типа
    /// консоли разработчика, окно, в котором я могу выбирать и листать все
    /// анимации, смотря их на манекене».
    ///
    /// Берём НЕ всё подряд. В проекте 4564 клипа, и каждый, на который есть
    /// ссылка, уезжает в сборку — это сотни мегабайт ради того, чего никто не
    /// откроет. Берём то, что может пригодиться нашему герою: одноручное,
    /// безоружное, действия, реакции и всю пластику Synty под нашу модель.
    ///
    /// У DoubleL берём только версии `_InPlace`: остальные везут персонажа
    /// корневым движением, и на манекене он уезжал бы из-под окна.
    /// </summary>
    public static class AnimConsoleKit
    {
        private const string Dbl = "Assets/DoubleL/FBX_Animations";
        private const string Boom = "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations";
        private const string Synty = "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine";

        private const string ControllerPath = "Assets/_Game/Art/Animations/Controllers/AC_AnimPreview.controller";

        /// <summary>Откуда берём. Порядок задаёт порядок в списке.</summary>
        private static readonly (string folder, bool onlyInPlace)[] Sources =
        {
            (Dbl + "/One Hand Base", true),
            (Dbl + "/One Hand Up", true),
            (Dbl + "/Actions", true),
            (Dbl + "/Hit", true),
            (Dbl + "/Dead Pose", true),
            (Dbl + "/Base Move", true),

            (Boom + "/1Hand-Dagger", false),
            (Boom + "/1Hand-Sword", false),
            (Boom + "/Armed", false),
            (Boom + "/Unarmed", false),
            (Boom + "/Relax", false),

            (Synty, false),
        };

        public static void Apply()
        {
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogError("[IsoRPG] Героя нет."); return; }

            var clips = new List<AnimationClip>();

            foreach (var (folder, onlyInPlace) in Sources)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning("[IsoRPG] Нет папки " + folder);
                    continue;
                }

                int was = clips.Count;

                foreach (string path in Directory
                             .GetFiles(folder, "*.fbx", SearchOption.AllDirectories)
                             .Concat(Directory.GetFiles(folder, "*.FBX", SearchOption.AllDirectories))
                             .Select(p => p.Replace('\\', '/'))
                             .Distinct()
                             .OrderBy(p => p))
                {
                    // Версии с корневым движением на манекене уезжают из-под
                    // окна: играем их «на месте».
                    if (onlyInPlace && !path.Contains("InPlace")) continue;
                    if (path.Contains("_RM_")) continue;

                    var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                            .OfType<AnimationClip>()
                                            .FirstOrDefault(c => !c.name.StartsWith("__preview"));

                    if (clip != null) clips.Add(clip);
                }

                Debug.Log($"[IsoRPG]   {folder}: {clips.Count - was}");
            }

            var controller = MakeController();

            var console = player.GetComponent<IsoRPG.UI.AnimConsole>();
            if (console == null) console = player.AddComponent<IsoRPG.UI.AnimConsole>();

            console.Setup(clips.ToArray(), controller);

            EditorUtility.SetDirty(console);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();

            Debug.Log($"[IsoRPG] Консоль анимаций: {clips.Count} клипов. Открывается клавишей F9.");
        }

        /// <summary>
        /// Контроллер-заготовка: одно состояние, один клип, никаких переходов.
        ///
        /// Нужен затем, чтобы показывать клип на манекене, не трогая боевой
        /// контроллер героя. Подмена в боевом означала бы, что смотришь ты
        /// одно, а игра играет другое.
        /// </summary>
        private static AnimatorController MakeController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing;

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var state = controller.layers[0].stateMachine.AddState("Preview");

            // Клип-затычка: без него подменять нечего — оверрайд работает
            // по исходному клипу, а не по имени состояния.
            var stub = AssetDatabase
                .LoadAllAssetsAtPath(Synty + "/Idles/A_MOD_BL_Idle_Standing_Masc.fbx")
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            state.motion = stub;
            controller.layers[0].stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return controller;
        }
    }
}
