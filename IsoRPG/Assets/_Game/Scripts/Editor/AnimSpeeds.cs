using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Печатает НАСТОЯЩИЕ скорости всех беговых клипов, какие есть в проекте.
    ///
    /// Заведено 04.09.2026, когда выяснилось, что замер молча возвращал ноль
    /// и код подставлял запасное число, а я подавал его заказчику как факт.
    /// Выбирать клип под нашу скорость, не зная, под какую скорость он
    /// нарисован, — это подбор вслепую: растяжка вдвое даёт мелкий частый
    /// шаг, и никакой доворот этого не лечит.
    ///
    /// Печатаем ещё и растяжку при нашей скорости — то самое число, которое
    /// и решает, будет клип выглядеть бегом или перемоткой.
    /// </summary>
    public static class AnimSpeeds
    {
        private const float HeroSpeed = 5.5f;
        private const float SprintSpeed = 5.5f * 1.7f;

        private const string Synty =
            "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine";

        private const string Dbl = "Assets/DoubleL/FBX_Animations";

        private const string Boom =
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations";

        public static void Apply()
        {
            Debug.Log("[IsoRPG] Скорости клипов. Растяжка = наша скорость / скорость клипа. " +
                      $"Бег у нас {HeroSpeed:0.0} м/с, спринт {SprintSpeed:0.0}.");

            Row("Synty шаг", Synty + "/Locomotion/Walk/A_MOD_BL_Walk_F_RM_Masc.fbx", HeroSpeed);
            Row("Synty бег", Synty + "/Locomotion/Run/A_MOD_BL_Run_F_RM_Masc.fbx", HeroSpeed);
            Row("Synty спринт", Synty + "/Locomotion/Sprint/A_MOD_BL_Sprint_F_RM_Masc.fbx", SprintSpeed);

            Row("DoubleL бег вооружённый",
                Dbl + "/One Hand Base/Movement/Run/Type A/Base/OneHand_Base_Run_A_F.fbx", HeroSpeed);
            Row("DoubleL спринт вооружённый",
                Dbl + "/One Hand Base/Movement/Sprint/Type A/Base/OneHand_Base_Sprint_A_F.fbx", SprintSpeed);
            Row("DoubleL бег безоружный",
                Dbl + "/Base Move/Run/Base/Run_F.fbx", HeroSpeed);
            Row("DoubleL спринт безоружный",
                Dbl + "/Base Move/Sprint/Base/Sprint_F.fbx", SprintSpeed);
            Row("DoubleL бег, оружие поднято",
                Dbl + "/One Hand Up/Movement/Run/Type A/Base/OneHand_Up_Run_F.fbx", HeroSpeed);

            Row("Explosive бег с оружием",
                Boom + "/Armed/RPG-Character@Armed-Run-Forward.FBX", HeroSpeed);
            Row("Explosive бег без оружия",
                Boom + "/Unarmed/RPG-Character@Unarmed-Run-Forward.FBX", HeroSpeed);
            Row("Explosive шаг",
                Boom + "/Armed/RPG-Character@Armed-Walk.FBX", HeroSpeed);
        }

        private static void Row(string title, string path, float ours)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null)
            {
                Debug.LogWarning($"[IsoRPG]   {title}: клипа нет — {path}");
                return;
            }

            float speed = ClipSpeed.Measure(clip);

            if (speed <= 0.05f)
            {
                Unbake(path);
                speed = ClipSpeed.Measure(clip);
            }

            if (speed <= 0.05f)
            {
                Debug.LogWarning($"[IsoRPG]   {title}: померить не вышло ({clip.name}).");
                return;
            }

            Debug.Log($"[IsoRPG]   {title}: {speed:0.00} м/с, растяжка x{ours / speed:0.00}  ({clip.name})");
        }

        /// <summary>Снять запекание корня в позу — иначе движение наружу не выдаётся.</summary>
        private static void Unbake(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            var takes = importer.clipAnimations;
            if (takes == null || takes.Length == 0) takes = importer.defaultClipAnimations;
            if (takes.Length == 0) return;

            bool changed = false;

            for (int i = 0; i < takes.Length; i++)
            {
                if (!takes[i].lockRootPositionXZ) continue;

                takes[i].lockRootPositionXZ = false;
                changed = true;
            }

            if (!changed) return;

            importer.clipAnimations = takes;
            importer.SaveAndReimport();
        }
    }
}
