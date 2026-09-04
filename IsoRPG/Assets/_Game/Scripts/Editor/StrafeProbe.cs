using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Замер кольца направлений Synty: с какой скоростью нарисован каждый
    /// боковой и задний клип.
    ///
    /// Нужен перед тем, как строить двумерное дерево хода. Наш герой бегает
    /// 5.5 м/с, а поле авторского контроллера говорит про бег 2.5 — если это
    /// правда, кольцо придётся гнать вдвое, и Павлон такую перемотку уже
    /// забраковал на спринте 04.09.2026. Поэтому сперва числа, потом дерево:
    /// растяжку больше полутора раз видно глазом, и решать это ему.
    ///
    /// Меряем по версиям `_RM_` — с корневым движением: только они и говорят,
    /// под какую скорость шаг нарисован. Играть будем версии без RM.
    /// </summary>
    public static class StrafeProbe
    {
        private const string Synty =
            "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine";

        /// <summary>Набор ExplosiveLLC: у него боковые клипы есть и с оружием в руках.</summary>
        private const string Boom =
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations";

        private const float HeroSpeed = 5.5f;

        /// <summary>Восемь сторон кольца плюс то, чем автор закрывает дыры.</summary>
        private static readonly string[] Ring =
        {
            "FwdStrafeF", "FwdStrafeFR", "FwdStrafeR", "FwdStrafeBR",
            "FwdStrafeFL", "FwdStrafeL",
            "BckStrafeB", "BckStrafeBL", "BckStrafeBR",
            "BckStrafeFL", "BckStrafeL", "BckStrafeR",
        };

        public static void Run()
        {
            foreach (var gait in new[] { "Walk", "Run" })
            {
                Debug.Log($"[IsoRPG] === {gait}: кольцо направлений ===");

                float lead = 0f;

                foreach (var side in Ring)
                {
                    string path = $"{Synty}/Locomotion/{gait}/A_MOD_BL_{gait}_{side}_RM_Masc.fbx";

                    AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                            .OfType<AnimationClip>()
                                            .FirstOrDefault(c => !c.name.StartsWith("__preview"));

                    if (clip == null)
                    {
                        Debug.LogWarning($"[IsoRPG]   {side}: клипа нет — {path}");
                        continue;
                    }

                    float speed = ClipSpeed.Measure(clip);

                    // Ноль почти всегда значит не «стоит на месте», а
                    // «движение запечено в позу»: у Synty так размечена
                    // часть кольца. Снимаем запекание и меряем снова —
                    // иначе решение принималось бы по пустому замеру.
                    if (speed <= 0.05f)
                    {
                        HeroMoveKit.UnbakeRoot(path);

                        clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                            .OfType<AnimationClip>()
                                            .FirstOrDefault(c => !c.name.StartsWith("__preview"));

                        if (clip != null) speed = ClipSpeed.Measure(clip);
                    }

                    if (side == "FwdStrafeF") lead = speed;

                    string stretch = speed > 0.05f
                        ? $"под наши {HeroSpeed} м/с растяжка x{HeroSpeed / speed:0.00}"
                        : "не померилась";

                    Debug.Log($"[IsoRPG]   {side}: {speed:0.00} м/с, длина {clip.length:0.00} с — {stretch}");
                }

                if (lead > 0.05f)
                    Debug.Log($"[IsoRPG] {gait}: вперёд нарисован под {lead:0.00} м/с.");
            }

            // Вооружённые стороны ExplosiveLLC. Павлон 04.09.2026 посмотрел
            // их в консоли и сказал «все хорошо выглядят, ставь»: у героя два
            // кинжала, а кольцо Synty безоружное — руки в нём свободны.
            // Прежде чем ставить, надо знать, под какую скорость нарисованы,
            // иначе повторим ошибку с растяжкой вдвое.
            foreach (var kind in new[] { "Armed", "Unarmed" })
            {
                Debug.Log($"[IsoRPG] === {kind}-Strafe: восемь сторон ===");

                foreach (var side in new[] { "Forward", "Forward-Right", "Right", "Backward-Right",
                                             "Backward", "Backward-Left", "Left", "Forward-Left" })
                {
                    string path = $"{Boom}/{kind}/RPG-Character@{kind}-Strafe-{side}.FBX";

                    AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                                      .OfType<AnimationClip>()
                                                      .FirstOrDefault(c => !c.name.StartsWith("__preview"));

                    if (clip == null)
                    {
                        Debug.LogWarning($"[IsoRPG]   {side}: клипа нет — {path}");
                        continue;
                    }

                    float speed = ClipSpeed.Measure(clip);

                    if (speed <= 0.05f)
                    {
                        HeroMoveKit.UnbakeRoot(path);

                        clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                            .OfType<AnimationClip>()
                                            .FirstOrDefault(c => !c.name.StartsWith("__preview"));

                        if (clip != null) speed = ClipSpeed.Measure(clip);
                    }

                    string stretch = speed > 0.05f
                        ? $"под наши {HeroSpeed} м/с растяжка x{HeroSpeed / speed:0.00}"
                        : "не померилась";

                    Debug.Log($"[IsoRPG]   {side}: {speed:0.00} м/с, длина {clip.length:0.00} с — {stretch}");
                }
            }

            // Повороты на месте: под какую угловую скорость их нарисовали.
            // То же, что со скоростью хода, только в градусах — и ошибка та
            // же по природе: взять свою скорость и погнать клип вдвое.
            foreach (var turn in new[] { "90L", "90R", "180L", "180R" })
            {
                string path = $"{Synty}/Locomotion/Turn/A_MOD_BL_Turn_Standing_{turn}_Masc.fbx";

                var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                        .OfType<AnimationClip>()
                                        .FirstOrDefault(c => !c.name.StartsWith("__preview"));

                if (clip == null)
                {
                    Debug.LogWarning($"[IsoRPG] Поворот {turn}: клипа нет.");
                    continue;
                }

                float degrees = turn.StartsWith("180") ? 180f : 90f;
                float rate = clip.length > 0.05f ? degrees / clip.length : 0f;

                Debug.Log($"[IsoRPG] Поворот {turn}: {clip.length:0.00} с на {degrees:0}° — {rate:0} град/с.");
            }

            // Спринт для сравнения: он у нас уже стоит и принят Павлоном,
            // значит его растяжка — образец допустимого.
            foreach (var one in new[] { "Sprint/A_MOD_BL_Sprint_F", "Walk/A_MOD_BL_Walk_F" })
            {
                string path = $"{Synty}/Locomotion/{one}_RM_Masc.fbx";

                var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                        .OfType<AnimationClip>()
                                        .FirstOrDefault(c => !c.name.StartsWith("__preview"));

                if (clip != null)
                    Debug.Log($"[IsoRPG] Образец {one}: {ClipSpeed.Measure(clip):0.00} м/с.");
            }
        }
    }
}
