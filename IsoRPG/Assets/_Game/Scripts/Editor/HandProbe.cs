using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп кисти: что за кости в руке героя и где они лежат.
    ///
    /// Заведён 02.09.2026, когда Павлон посмотрел на кинжалы в игре и назвал
    /// сразу три вещи: левый лежит на кончиках пальцев, правый проходит
    /// сквозь начало кисти, а легли они по-разному, хотя один делался
    /// отражением другого.
    ///
    /// Последнее и есть подсказка: **отражение врёт, если скелет не
    /// зеркальный.** Проверяется это замером, а не рассуждением — печатаем
    /// оси обеих кистей в системе героя и смотрим, зеркальны они или
    /// одинаковы.
    ///
    /// Заодно печатаем кости пальцев в системе кисти: по ним центр кулака и
    /// ось рукояти СЧИТАЮТСЯ, а не подбираются глазом. Кинжал в кулаке лежит
    /// вдоль линии оснований пальцев, чуть внутрь ладони от неё.
    /// </summary>
    public static class HandProbe
    {
        private const string Hero = "Human-Custom2";

        /// <summary>Клип, в котором герой стоит в игре. Мерить надо в НЁМ, а не в Т-позе.</summary>
        private const string IdleClip =
            "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine/" +
            "Idles/A_MOD_BL_Idle_Standing_Masc.fbx";

        [MenuItem("Tools/IsoRPG/Щуп: кости кисти", priority = 47)]
        public static void Run()
        {
            var prefab = FindPrefab(Hero);

            if (prefab == null)
            {
                Debug.LogError("[IsoRPG] Нет героя " + Hero);
                return;
            }

            var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            var idle = AssetDatabase.LoadAllAssetsAtPath(IdleClip)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (idle != null) idle.SampleAnimation(hero, 0f);

            var all = hero.GetComponentsInChildren<Transform>(true);

            var report = new StringBuilder();

            // --- все кости кисти по именам ----------------------------------
            report.Append("\n  Кости, в имени которых есть hand/finger/thumb/index/middle/ring/pinky:");

            foreach (var t in all)
            {
                string n = t.name.ToLowerInvariant();

                if (!n.Contains("hand") && !n.Contains("finger") && !n.Contains("thumb") &&
                    !n.Contains("index") && !n.Contains("middle") && !n.Contains("ring") &&
                    !n.Contains("pinky")) continue;

                report.Append("\n    ").Append(t.name);
            }

            // --- зеркальны ли кисти -----------------------------------------
            var right = all.FirstOrDefault(t => t.name == "hand_r");
            var left = all.FirstOrDefault(t => t.name == "hand_l");

            if (right != null && left != null)
            {
                report.Append("\n\n  Оси кистей в системе героя (X / Y / Z каждой):");
                Axes(report, "hand_r", hero.transform, right);
                Axes(report, "hand_l", hero.transform, left);

                // Зеркальность проверяем прямо: отражаем оси правой по X и
                // сравниваем с левой. Совпало — скелет зеркальный, отражение
                // законно. Не совпало — надо считать каждую руку отдельно.
                var mirror = new Vector3(-1f, 1f, 1f);

                float diff = 0f;

                for (int i = 0; i < 3; i++)
                {
                    var r = hero.transform.InverseTransformDirection(Axis(right, i));
                    var l = hero.transform.InverseTransformDirection(Axis(left, i));

                    r = Vector3.Scale(r, mirror);
                    diff += Vector3.Angle(r, l);
                }

                report.Append("\n  Расхождение отражённой правой и левой: ")
                      .Append(diff.ToString("0.0")).Append(" градусов суммарно по трём осям — ")
                      .Append(diff < 30f ? "скелет ЗЕРКАЛЬНЫЙ, отражение законно"
                                         : "скелет НЕ ЗЕРКАЛЬНЫЙ, отражать нельзя");
            }

            // --- пальцы в системе кисти -------------------------------------
            foreach (var hand in new[] { right, left })
            {
                if (hand == null) continue;

                report.Append("\n\n  Пальцы в системе ").Append(hand.name)
                      .Append(" (метры, локально):");

                foreach (var t in all)
                {
                    if (t == hand) continue;
                    if (!IsUnder(t, hand)) continue;

                    var p = hand.InverseTransformPoint(t.position);

                    report.Append("\n    ").Append(t.name.PadRight(18))
                          .Append(p.x.ToString("0.0000")).Append(" / ")
                          .Append(p.y.ToString("0.0000")).Append(" / ")
                          .Append(p.z.ToString("0.0000"));
                }
            }

            Debug.Log("[IsoRPG] Щуп кисти:" + report);

            Object.DestroyImmediate(hero);
        }

        private static void Axes(StringBuilder report, string name, Transform root, Transform bone)
        {
            report.Append("\n    ").Append(name).Append(": ");

            for (int i = 0; i < 3; i++)
            {
                var v = root.InverseTransformDirection(Axis(bone, i));

                report.Append("(").Append(v.x.ToString("0.00")).Append(", ")
                      .Append(v.y.ToString("0.00")).Append(", ")
                      .Append(v.z.ToString("0.00")).Append(") ");
            }
        }

        private static Vector3 Axis(Transform t, int index) =>
            index == 0 ? t.right : index == 1 ? t.up : t.forward;

        private static bool IsUnder(Transform t, Transform parent)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p == parent) return true;

            return false;
        }

        private static GameObject FindPrefab(string prefabName)
        {
            var guid = AssetDatabase.FindAssets(prefabName + " t:Prefab").FirstOrDefault();

            return guid == null
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
