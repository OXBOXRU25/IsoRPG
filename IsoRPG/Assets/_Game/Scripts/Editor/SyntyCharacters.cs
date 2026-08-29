using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Переселение игровых персонажей на модели Synty.
    ///
    /// Работает так дёшево только благодаря переводу рига KayKit в Humanoid.
    /// Контроллеры `AC_Rogue`, `AC_Skeleton`, `AC_Bandit` и прочие собраны из
    /// клипов KayKit — а humanoid-клип надевается на любой humanoid-скелет.
    /// То есть весь бой, удары, смерть и добивание переезжают вместе с
    /// персонажем и заново их собирать не надо.
    ///
    /// Пересобираем ТЕ ЖЕ префабы под теми же именами. Игровые компоненты
    /// (здоровье, бой, добыча, квесты) сюда не входят: их вешает сборщик
    /// сцены при постройке, а не префаб. Поэтому подмена модели не задевает
    /// ни таблицу монстров, ни баланс.
    /// </summary>
    public static class SyntyCharacters
    {
        private const string Prefabs = "Assets/_Game/Prefabs";
        private const string Controllers = "Assets/_Game/Art/KayKit/Controllers";

        /// <summary>
        /// Кто кем становится: имя префаба, модель Synty, контроллер.
        ///
        /// Модель ищем по имени, а не по пути: наборы лежат на разной
        /// глубине, и один опечатанный путь дал бы молча пропущенного
        /// персонажа.
        /// </summary>
        private static readonly (string prefab, string model, string controller)[] Roster =
        {
            ("Player",            "SM_Chr_Commoner_Male_01",       "AC_Rogue"),

            ("Skeleton_Warrior",  "Chr_Skeleton_01",               "AC_Skeleton"),
            ("Skeleton_Rogue",    "Chr_Skeleton_02",               "AC_SkeletonArcher"),
            ("Skeleton_Minion",   "Chr_Skeleton_03",               "AC_Skeleton"),
            ("Skeleton_Mage",     "Chr_Undead_Knight_01",          "AC_SkeletonMage"),

            ("Bandit_Brute",      "Chr_Nomad_Male_01",             "AC_Bandit"),
            ("Bandit_Guard",      "SM_Chr_Soldier_Male_01",        "AC_Bandit"),
            ("Bandit_Hunter",     "Chr_Nomad_Male_02",             "AC_Hunter"),
            ("Bandit_Warlock",    "SM_Chr_Male_Sorcerer_01",       "AC_SkeletonMage"),
            ("Bandit_Skirmisher", "Chr_Nomad_Male_03",             "AC_Rogue"),
        };

        [MenuItem("Tools/IsoRPG/Персонажи: переселить на Synty", priority = 56)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play ассеты не сохраняются на диск.", "Понятно");
                return;
            }

            int made = 0, missed = 0;

            foreach (var (prefabName, modelName, controllerName) in Roster)
            {
                string modelPath = FindPrefab(modelName);

                if (modelPath == null)
                {
                    Debug.LogError("[IsoRPG] Не нашёл модель " + modelName +
                                   " для " + prefabName);
                    missed++;
                    continue;
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    Controllers + "/" + controllerName + ".controller");

                if (controller == null)
                {
                    Debug.LogError("[IsoRPG] Нет контроллера " + controllerName);
                    missed++;
                    continue;
                }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);

                instance.name = prefabName;

                var animator = instance.GetComponentInChildren<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();

                // Проверяем скелет ЗДЕСЬ, а не в игре.
                //
                // Humanoid-клип на не-humanoid скелете не играет вовсе, и
                // персонаж застывает буквой «Т». В журнале при этом тишина —
                // ровно тот случай, когда молчание дороже ошибки.
                if (animator.avatar == null || !animator.avatar.isHuman)
                {
                    Debug.LogError("[IsoRPG] " + modelName + " не Humanoid — " +
                                   prefabName + " остался бы столбом. Пропускаю.");
                    Object.DestroyImmediate(instance);
                    missed++;
                    continue;
                }

                animator.runtimeAnimatorController = controller;

                // Позицию ведёт навигационный агент. С корневым движением
                // анимация тянет персонажа сама, и он уезжает от агента.
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                PrefabUtility.SaveAsPrefabAsset(instance, Prefabs + "/" + prefabName + ".prefab");
                Object.DestroyImmediate(instance);

                made++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log("[IsoRPG] Персонажи переселены на Synty: " + made +
                      (missed > 0 ? ", не вышло у " + missed : "") +
                      ". Контроллеры прежние — клипы KayKit теперь humanoid " +
                      "и надеваются на чужой скелет.");
        }

        private static string FindPrefab(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return path;
            }

            return null;
        }
    }
}
