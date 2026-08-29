using System.Linq;
using IsoRPG.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Щуп хвата: ставит в ряд героев с разными доворотами оружия.
    ///
    /// Ориентацию клинка в руке аналитически не вывести — у кости-держателя
    /// своя ось, у модели оружия своя, и совпадают они только случайно.
    /// Примера от автора в наборах нет: Sidekick Starter не содержит ни
    /// одного персонажа с оружием, демо-сцены Base Locomotion тоже пустые.
    ///
    /// Поэтому не гадаем по кругу, а меряем: один прогон, один кадр, на нём
    /// сразу видно, какой поворот верный. Дешевле трёх заходов вслепую.
    /// </summary>
    public static class GripProbe
    {
        private const string Hero = "Human-Custom2";
        private const string Dagger = "SM_Wep_Dagger_01";

        /// <summary>Проверяемые довороты. Смещение при этом держим нулевым.</summary>
        private static readonly Vector3[] Rotations =
        {
            new Vector3(  0f,  0f,   0f),
            new Vector3(  0f,  0f,  90f),
            new Vector3(  0f, 90f,   0f),
            new Vector3(  0f,  0f, -90f),
            new Vector3(180f,  0f,   0f),
            new Vector3(-90f, 90f,   0f),
        };

        /// <summary>
        /// Проверяемые смещения рукояти, метры.
        ///
        /// Точка отсчёта у модели Synty в середине клинка, поэтому при нуле
        /// половина оружия уходит назад сквозь ладонь. Сдвигаем вдоль оси,
        /// куда смотрит клинок после доворота.
        /// </summary>
        private static readonly Vector3[] Variants = Rotations;

        /// <summary>Клип стойки: без него герой стоит буквой «Т».</summary>
        private const string IdleClip =
            "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine/" +
            "Idles/A_MOD_BL_Idle_Standing_Masc.fbx";

        [MenuItem("Tools/IsoRPG/Щуп: хват оружия", priority = 47)]
        public static void Build()
        {
            if (EditorApplication.isPlaying) return;

            var heroPrefab = Find(Hero);
            var daggerPrefab = Find(Dagger);

            if (heroPrefab == null || daggerPrefab == null)
            {
                Debug.LogError("[IsoRPG] Нет героя или кинжала для щупа хвата.");
                return;
            }

            var idle = AssetDatabase.LoadAllAssetsAtPath(IdleClip)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (idle == null)
                Debug.LogWarning("[IsoRPG] Клип стойки не найден — замер пойдёт на Т-позе " +
                                 "и снова соврёт.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Солнце").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.intensity = 1.1f;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Земля";
            ground.transform.localScale = Vector3.one * 4f;

            for (int i = 0; i < Variants.Length; i++)
            {
                var hero = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab);
                hero.name = (i + 1) + ". " + Variants[i];
                hero.transform.position = new Vector3((i - (Variants.Length - 1) * 0.5f) * 1.5f, 0f, 0f);
                hero.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                // Ставим героя в стойку ДО того, как цеплять оружие.
                //
                // Прошлый замер соврал именно из-за этого: без позы модель
                // стоит буквой «Т», кисть развёрнута иначе, чем в игре, и
                // «правильный» на кадре доворот в бою оказался боком. В
                // редакторе аниматор не играет, но позу можно наложить
                // руками — клип умеет применить себя к объекту.
                if (idle != null)
                {
                    // Клип держит кривую положения корня, и наложение позы
                    // утаскивает объект в начало координат — в первом
                    // заходе все четверо слиплись в одну кучу. Позицию
                    // возвращаем сами.
                    var keep = hero.transform.position;
                    idle.SampleAnimation(hero, 0f);
                    hero.transform.position = keep;
                }

                // Цепляем кинжал вручную, теми же правилами, что и в бою:
                // ищем кость-держатель и сажаем с проверяемым доворотом.
                var bone = hero.GetComponentsInChildren<Transform>(true)
                               .FirstOrDefault(t => t.name == "prop_r");

                if (bone == null)
                {
                    Debug.LogWarning("[IsoRPG] У героя нет кости prop_r.");
                    continue;
                }

                var blade = (GameObject)PrefabUtility.InstantiatePrefab(daggerPrefab, bone);
                blade.transform.localPosition = Vector3.zero;
                blade.transform.localRotation = Quaternion.Euler(Variants[i]);
                blade.transform.localScale = Vector3.one;
            }

            EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/GripProbe.unity");

            Debug.Log("[IsoRPG] Щуп хвата: " + Variants.Length +
                      " вариантов слева направо — " +
                      string.Join(", ", Variants.Select(v => v.ToString())));
        }

        private static GameObject Find(string prefabName)
        {
            var guid = AssetDatabase.FindAssets(prefabName + " t:Prefab").FirstOrDefault();

            return guid == null
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
