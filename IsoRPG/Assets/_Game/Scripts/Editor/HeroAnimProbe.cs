using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп боевых анимаций героя: что РЕАЛЬНО лежит в состояниях удара.
    ///
    /// Заведён 02.09.2026, когда Павлон увидел в игре: «он бьёт кулаками, а не
    /// кинжалами». Код при этом прописывает шесть кинжальных клипов
    /// ExplosiveLLC — значит либо задание до контроллера не доехало, либо
    /// поверх лежит что-то, что удар перекрывает.
    ///
    /// Второй подозреваемый — слой «Кисть» с маской на пальцы, добавленный в
    /// тот же день: если маска пропускает больше, чем пальцы, слой в режиме
    /// Override накрывает весь верх тела стойкой, и замах пропадает. Поэтому
    /// печатаем не только состояния, но и каждый слой с его маской и весом.
    /// </summary>
    public static class HeroAnimProbe
    {
        private const string ControllerPath =
            "Assets/_Game/Art/Animations/Controllers/AC_Hero_Sidekick.controller";

        [MenuItem("Tools/IsoRPG/Щуп: боевые анимации героя", priority = 48)]
        public static void Run()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                Debug.LogError("[IsoRPG] Нет контроллера героя: " + ControllerPath);
                return;
            }

            var report = new StringBuilder();

            for (int i = 0; i < controller.layers.Length; i++)
            {
                var layer = controller.layers[i];

                report.Append("\n  Слой ").Append(i).Append(" «").Append(layer.name)
                      .Append("», вес ").Append(layer.defaultWeight.ToString("0.00"))
                      .Append(", режим ").Append(layer.blendingMode);

                if (layer.avatarMask == null)
                {
                    report.Append(", маски НЕТ (пропускает всё тело)");
                }
                else
                {
                    var open = System.Enum.GetValues(typeof(AvatarMaskBodyPart))
                        .Cast<AvatarMaskBodyPart>()
                        .Where(p => p != AvatarMaskBodyPart.LastBodyPart)
                        .Where(p => layer.avatarMask.GetHumanoidBodyPartActive(p))
                        .ToArray();

                    report.Append(", маска пропускает: ")
                          .Append(open.Length == 0 ? "ничего" : string.Join(", ", open))
                          .Append("; своих трансформов в маске ")
                          .Append(layer.avatarMask.transformCount);
                }

                if (layer.stateMachine == null) continue;

                foreach (var child in layer.stateMachine.states.OrderBy(s => s.state.name))
                {
                    var state = child.state;

                    // Интересуют удары и всё, что может их перекрыть.
                    bool interesting = state.name.StartsWith("Attack") ||
                                       state.name.Contains("Fist") ||
                                       i > 0;

                    if (!interesting) continue;

                    string motion = state.motion == null ? "ПУСТО" : state.motion.name;

                    string source = "";

                    if (state.motion != null)
                    {
                        string path = AssetDatabase.GetAssetPath(state.motion);

                        // Из какой папки набора приехал клип — по ней и видно,
                        // кинжальный он или безоружный.
                        source = string.IsNullOrEmpty(path)
                            ? ""
                            : "  ← " + System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                    }

                    report.Append("\n      ").Append(state.name.PadRight(16))
                          .Append(motion).Append(source);
                }
            }

            Debug.Log("[IsoRPG] Щуп боевых анимаций героя:" + report);
        }
    }
}
