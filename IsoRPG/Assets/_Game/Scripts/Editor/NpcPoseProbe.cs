using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп поз НПС: ставит в ряд по одному персонажу на каждое занятие.
    ///
    /// Павлон 02.09.2026: «поза где он две руки к небу поднимает выглядит
    /// плохо, я бы её убрал». Какая из пяти это — из слов не выводится, а
    /// гадать значит выкинуть не ту. Один прогон, один кадр, и видно сразу.
    ///
    /// Позу берём в СЕРЕДИНЕ клипа, а не на первом кадре: стойки начинаются
    /// из нейтрального положения, и на нулевом кадре все пятеро выглядят
    /// одинаково.
    /// </summary>
    public static class NpcPoseProbe
    {
        private const string Model =
            "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_01/Starter_01.prefab";

        private const string Pack = "Assets/DoubleL/FBX_Animations";

        /// <summary>Те же занятия, что раздаёт NpcIdleKit, в том же порядке.</summary>
        private static readonly (string Folder, string Clip)[] Poses =
        {
            ("Base Move/Stand_Idle/Idle", "Stand_Idle_A_1"),
            ("Base Move/Stand_Idle/Idle", "Stand_Idle_A_2"),
            ("Base Move/Stand_Idle/Idle", "Stand_Idle_A_3"),
            ("Base Move/Stand_Idle/Idle", "Stand_Idle_A_4"),
            ("Base Move/Stand_Idle/Idle", "Stand_Idle_B_1"),
            ("Base Move/Stand_Idle/Idle", "Stand_Idle_B_2"),
            ("NPC/Wipe Forehead", "Wipe_Forehead"),
            ("NPC/Think", "Think_All"),
        };

        /// <summary>Куда смотреть камере — считается по ряду.</summary>
        public static Vector3 Centre { get; private set; } = new Vector3(0f, 1.2f, 0f);

        [MenuItem("Tools/IsoRPG/Щуп: позы НПС", priority = 48)]
        public static void Build()
        {
            if (EditorApplication.isPlaying) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Model);

            if (prefab == null)
            {
                Debug.LogError("[IsoRPG] Нет модели НПС: " + Model);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Солнце").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(42f, 20f, 0f);
            sun.intensity = 1.5f;

            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.64f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Земля";
            ground.transform.localScale = Vector3.one * 4f;

            float step = 1.4f;

            for (int i = 0; i < Poses.Length; i++)
            {
                var clip = Clip(Poses[i].Folder, Poses[i].Clip);

                var person = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                person.name = (i + 1) + ". " + Poses[i].Clip;
                person.transform.position = new Vector3((i - (Poses.Length - 1) * 0.5f) * step, 0f, 0f);
                person.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                if (clip == null) continue;

                // Клип держит кривую положения корня, и наложение позы
                // утаскивает объект в начало координат — позицию возвращаем.
                var keep = person.transform.position;
                clip.SampleAnimation(person, clip.length * 0.5f);
                person.transform.position = keep;
            }

            Centre = new Vector3(0f, 1.15f, 0f);

            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/NpcPoseProbe.unity");

            Debug.Log("[IsoRPG] Щуп поз НПС: " + Poses.Length + " слева направо — " +
                      string.Join(" | ", Poses.Select((p, i) => (i + 1) + "." + p.Clip)) +
                      Sway());
        }

        /// <summary>
        /// Размах качания корпуса у каждой стойки, сантиметры и градусы.
        ///
        /// Павлон 02.09.2026: «поза где он две руки немного одновременно
        /// поднимает и делает движения тазом». Какая из шести — по кадру не
        /// видно вовсе: качание это ДВИЖЕНИЕ, а кадр статичен. Зато оно
        /// меряется: у гуманоидного клипа корпус живёт в кривых корня
        /// `RootT` (смещение) и `RootQ` (поворот), и стойка с раскачкой даёт
        /// размах заметно больше соседей.
        ///
        /// Это тот случай, когда щуп-картинка бессилен, а щуп-число решает
        /// за один прогон.
        /// </summary>
        private static string Sway()
        {
            var report = new System.Text.StringBuilder("\n  Размах корпуса (боком, см / поворот, град):");

            foreach (var pose in Poses)
            {
                var clip = Clip(pose.Folder, pose.Clip);
                if (clip == null) continue;

                float side = 0f, turn = 0f;

                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    bool isSide = b.propertyName == "RootT.x" || b.propertyName == "RootT.z";
                    bool isTurn = b.propertyName.StartsWith("RootQ");

                    if (!isSide && !isTurn) continue;

                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    if (curve == null || curve.length < 2) continue;

                    float min = float.MaxValue, max = float.MinValue;

                    // По ключам, а не по времени: их немного, и крайние
                    // значения кривой лежат именно в них.
                    foreach (var key in curve.keys)
                    {
                        min = Mathf.Min(min, key.value);
                        max = Mathf.Max(max, key.value);
                    }

                    float span = max - min;

                    if (isSide) side = Mathf.Max(side, span);
                    else turn = Mathf.Max(turn, span);
                }

                report.Append("\n    ").Append(pose.Clip.PadRight(18))
                      .Append((side * 100f).ToString("0.0")).Append("  /  ")
                      .Append((turn * 100f).ToString("0.0"));
            }

            return report.ToString();
        }

        private static AnimationClip Clip(string folder, string name)
        {
            string path = Pack + "/" + folder + "/" + name + ".fbx";

            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null) Debug.LogWarning("[IsoRPG] Клип не найден: " + path);

            return clip;
        }
    }
}
