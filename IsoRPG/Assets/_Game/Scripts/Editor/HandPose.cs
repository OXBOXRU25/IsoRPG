using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Сжимает пальцы героя вокруг рукояти — отдельным слоем с маской.
    ///
    /// Павлон 02.09.2026, глядя на кинжалы в игре: «пальцы должны сжать
    /// рукоять, они сейчас вообще не согнуты». Так и есть, и доворотом оружия
    /// это не чинится в принципе: пальцы держит анимация, а герой играет
    /// БЕЗОРУЖНЫЕ стойки Synty — в них ладонь раскрыта, и рукоять она не
    /// обхватит, куда её ни клади.
    ///
    /// Путей было два. Первый — поменять всю пластику героя на вооружённый
    /// набор ExplosiveLLC (`Armed-Idle`, `Armed-Walk`, `Armed-Run-Forward`):
    /// там кисть сжата, но заодно меняется вообще всё, как герой стоит и
    /// ходит. Второй — взять из того же набора ТОЛЬКО кисть, отдельным слоем
    /// с маской. Взят второй: правка целится ровно в то, что не так.
    ///
    /// Слой стоит с нулевым весом и включается, когда в руке появляется
    /// оружие (<see cref="IsoRPG.Items.WeaponVisual"/>). Иначе герой ходил бы
    /// с вечно сжатыми кулаками и без оружия.
    ///
    /// Цена в кадре нулевая: маска гуманоида отбирает кости на стороне
    /// движка, а вес слоя ставится один раз при смене экипировки — не в
    /// Update. Это ММО, считать пальцы каждый кадр у каждого игрока нельзя.
    /// </summary>
    public static class HandPose
    {
        private const string ControllerPath =
            "Assets/_Game/Art/Animations/Controllers/AC_Hero_Sidekick.controller";

        private const string MaskPath = "Assets/_Game/Art/Animations/Masks/Fingers.mask";

        /// <summary>Клип, из которого берём сжатую кисть. Всё остальное в нём маска отрежет.</summary>
        private const string FistClip =
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations/" +
            "Armed/RPG-Character@Armed-Idle.FBX";

        /// <summary>Имя слоя. По нему его находит и включает показ оружия.</summary>
        public const string LayerName = "Кисть";

        [MenuItem("Tools/IsoRPG/Герой: сжать пальцы вокруг оружия", priority = 45)]
        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                Debug.LogError("[IsoRPG] Нет контроллера героя: " + ControllerPath);
                return;
            }

            var fist = AssetDatabase.LoadAllAssetsAtPath(FistClip)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (fist == null)
            {
                Debug.LogError("[IsoRPG] Нет клипа со сжатой кистью: " + FistClip);
                return;
            }

            var mask = BuildMask();

            // Прогонять можно повторно: старый слой сносим, иначе они
            // копятся и вес делится между одинаковыми.
            for (int i = controller.layers.Length - 1; i > 0; i--)
                if (controller.layers[i].name == LayerName) controller.RemoveLayer(i);

            var machine = new AnimatorStateMachine
            {
                name = LayerName,
                hideFlags = HideFlags.HideInHierarchy,
            };

            AssetDatabase.AddObjectToAsset(machine, controller);

            var state = machine.AddState("Fist");
            state.motion = fist;
            machine.defaultState = state;

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = LayerName,
                stateMachine = machine,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,

                // Ноль: включает слой показ оружия, когда клинок появился в
                // руке. Иначе герой и с пустыми руками ходил бы с кулаками.
                defaultWeight = 0f,
            });

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"[IsoRPG] Слой «{LayerName}» собран: маска только на пальцы обеих рук, " +
                      $"поза из «{fist.name}», вес 0 — поднимает его показ оружия.");
        }

        /// <summary>
        /// Маска: пропускает ТОЛЬКО пальцы обеих рук.
        ///
        /// У гуманоидного аватара маска задаётся частями тела, а не путями
        /// костей, — поэтому она переживает любую смену скелета, лишь бы
        /// пальцы в аватаре были размечены.
        /// </summary>
        private static AvatarMask BuildMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);

            if (mask == null)
            {
                EnsureFolder();
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, MaskPath);
            }

            for (var part = AvatarMaskBodyPart.Root;
                 part < AvatarMaskBodyPart.LastBodyPart;
                 part++)
            {
                mask.SetHumanoidBodyPartActive(part, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);

            EditorUtility.SetDirty(mask);

            return mask;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Art/Animations/Masks"))
                AssetDatabase.CreateFolder("Assets/_Game/Art/Animations", "Masks");
        }
    }
}
