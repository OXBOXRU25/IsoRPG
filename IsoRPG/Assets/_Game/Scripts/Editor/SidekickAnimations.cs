using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит герою родные анимации Synty, не трогая логику боя.
    ///
    /// <b>Почему не переключить контроллер целиком.</b> Рядом лежит готовый
    /// `AC_Sidekick_Masculine` от автора набора, и соблазн велик. Но он
    /// рассчитан на СВОЮ систему движения: знает `CurrentGait`,
    /// `CameraRotationOffset`, `BodyLookX`, а про наши `Speed`, `Attack`,
    /// `StealthKill` не знает вовсе. Наш `CharacterAnimatorDriver` писал бы
    /// в пустоту, и герой встал бы столбом.
    ///
    /// Поэтому берём наш контроллер с нашими параметрами и меняем внутри
    /// него только сами клипы. Ретаргет уходит — анимации становятся
    /// родными для скелета Sidekick, — а переходы, условия и бой остаются
    /// как были.
    ///
    /// Боевых клипов в Base Locomotion нет вовсе: там ходьба, бег, стойки и
    /// прыжки. Удар и добивание остаются на ExplosiveLLC до тех пор, пока не
    /// поставим Sword Combat.
    /// </summary>
    public static class SidekickAnimations
    {
        private const string Source = "Assets/_Game/Art/Animations/Controllers/AC_Rogue.controller";
        private const string Target = "Assets/_Game/Art/Animations/Controllers/AC_Hero_Sidekick.controller";

        private const string Clips =
            "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine";

        /// <summary>
        /// Пороги дерева смешивания, метры в секунду. Скорость приходит от
        /// навигационного агента как есть, поэтому это настоящие м/с, а не
        /// доли. Подобраны на глаз и проверяются кадром: если на ходьбе
        /// герой семенит — порог бега опустить.
        /// </summary>
        private const float WalkAt = 1.8f, RunAt = 4.0f;

        [MenuItem("Tools/IsoRPG/Анимации: родные Synty герою", priority = 36)]
        public static void Apply()
        {
            var source = AssetDatabase.LoadAssetAtPath<AnimatorController>(Source);

            if (source == null)
            {
                Debug.LogError("[IsoRPG] Нет исходного контроллера " + Source);
                return;
            }

            var idle = Clip("Idles/A_MOD_BL_Idle_Standing_Masc.fbx");
            var walk = Clip("Locomotion/Walk/A_MOD_BL_Walk_F_Masc.fbx");
            var run = Clip("Locomotion/Run/A_MOD_BL_Run_F_Masc.fbx");

            if (idle == null || walk == null || run == null)
            {
                Debug.LogError("[IsoRPG] Не нашлись клипы Synty — стойка/ходьба/бег. " +
                               "Проверь, что набор Base Locomotion на месте.");
                return;
            }

            // Работаем на копии: оригинал AC_Rogue остаётся рабочим, и
            // откат — это одна строка в SyntyHeroSwap, а не пересборка.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(Target) != null)
                AssetDatabase.DeleteAsset(Target);

            if (!AssetDatabase.CopyAsset(Source, Target))
            {
                Debug.LogError("[IsoRPG] Не удалось скопировать контроллер.");
                return;
            }

            AssetDatabase.ImportAsset(Target);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Target);
            int replaced = 0;

            foreach (var layer in controller.layers)
            {
                foreach (var child in layer.stateMachine.states)
                {
                    var state = child.state;

                    if (state.name == "Locomotion")
                    {
                        var tree = new BlendTree
                        {
                            name = "Ход",
                            blendType = BlendTreeType.Simple1D,
                            blendParameter = "Speed",
                            useAutomaticThresholds = false,
                        };

                        AssetDatabase.AddObjectToAsset(tree, controller);

                        tree.children = new[]
                        {
                            new ChildMotion { motion = idle, threshold = 0f,      timeScale = 1f },
                            new ChildMotion { motion = walk, threshold = WalkAt,  timeScale = 1f },
                            new ChildMotion { motion = run,  threshold = RunAt,   timeScale = 1f },
                        };

                        state.motion = tree;
                        replaced++;
                    }
                    // Прыжок НЕ трогаем.
                    //
                    // У Synty он разбит на три фазы: отрыв, зависание,
                    // приземление — под их контроллер с отдельными
                    // состояниями. У нас состояние одно, и клип отрыва
                    // отыгрывался целиком: с середины полёта включалось
                    // дерево ходьбы (герой «шёл по воздуху»), а приземление
                    // не играло вовсе — посадка на прямые ноги.
                    //
                    // Прыжок ExplosiveLLC цельный, с группировкой при
                    // посадке, и на одном состоянии работает правильно.
                    // Ретаргет тут дешевле, чем разбор контроллера на фазы.
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Родные анимации Synty: состояний перебрано " + replaced +
                      ". Ход — стойка/шаг(" + WalkAt + ")/бег(" + RunAt + "). " +
                      "Удар и добивание остались на ExplosiveLLC: боя в Base Locomotion нет.");
        }

        /// <summary>Достать клип из FBX. Внутри файла он лежит подобъектом.</summary>
        private static AnimationClip Clip(string relative)
        {
            string path = Clips + "/" + relative;

            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null) Debug.LogWarning("[IsoRPG] Клип не найден: " + path);

            return clip;
        }
    }
}
