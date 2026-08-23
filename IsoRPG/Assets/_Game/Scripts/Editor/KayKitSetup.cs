using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Готовит набор KayKit к работе: зацикливает нужные клипы и приводит
    /// модели к нашему масштабу.
    ///
    /// Оба шага обязательны и оба молчаливы, если их пропустить. Ни один клип
    /// в библиотеке не зациклен — персонаж делает шаг и застывает, будто
    /// сломалась логика движения. А модели у KayKit ростом 2.3–2.8 м против
    /// наших 1.9: навигация, коллайдеры и скорости настроены под человека, и
    /// великан среди них выглядит ошибкой сборки.
    /// </summary>
    public static class KayKitSetup
    {
        private const string AnimationsFolder = "Assets/_Game/Art/KayKit/Animations";
        private const string CharactersFolder = "Assets/_Game/Art/KayKit/Characters";

        /// <summary>
        /// Окружение. Масштабируется тем же коэффициентом, что и персонажи:
        /// набор нарисован в одной мерке, и стоит ужать только людей, как
        /// двери станут велики, а деревья превратятся в секвойи.
        /// </summary>
        private static readonly string[] EnvironmentFolders =
        {
            "Assets/_Game/Art/KayKit/Dungeon",
            "Assets/_Game/Art/KayKit/Nature",
        };

        /// <summary>
        /// Целевой рост персонажа в метрах. Взят от нашей прежней модели,
        /// чтобы не пересчитывать навигацию, высоты коллайдеров и скорости.
        /// </summary>
        private const float TargetHeight = 1.9f;

        /// <summary>Коэффициент, которым ужали персонажей. Им же идёт окружение.</summary>
        private static float lastScaleFactor;

        /// <summary>
        /// Клипы, которые обязаны зацикливаться: всё, что играет, пока
        /// состояние держится. Разовые действия — удар, смерть, подбор —
        /// зацикливать нельзя, иначе персонаж будет умирать по кругу.
        /// </summary>
        private static readonly string[] LoopingClips =
        {
            "Idle_A", "Idle_B",
            "Walking_A", "Walking_B", "Walking_C", "Walking_Backwards",
            "Running_A", "Running_B",
            "Running_Strafe_Left", "Running_Strafe_Right",
            "Sneaking", "Crouching", "Crawling",
            "Melee_Unarmed_Idle", "Melee_2H_Idle", "Melee_Blocking",
            "Skeletons_Idle", "Skeletons_Walking",
            "Ranged_Bow_Idle", "Ranged_Bow_Aiming_Idle",
            "Sit_Chair_Idle", "Sit_Floor_Idle", "Lie_Idle",
            "Fishing_Idle",
            "Chopping", "Digging", "Hammering", "Pickaxing", "Sawing",
            "Lockpicking", "Working_A", "Working_B", "Working_C",
            "Holding_A", "Holding_B", "Holding_C",
        };

        [MenuItem("Tools/IsoRPG/Подготовить набор KayKit", priority = 12)]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play настройки импорта не сохраняются.", "Понятно");
                return;
            }

            int loops = PrepareAnimations();
            int scaled = PrepareCharacters();
            int env = PrepareEnvironment();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] KayKit подготовлен: зациклено клипов " + loops +
                      ", персонажей " + scaled + ", объектов окружения " + env + ".");
        }

        // ------------------------------------------------------------------

        private static int PrepareAnimations()
        {
            var wanted = new HashSet<string>(LoopingClips);
            int changed = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { AnimationsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                // Берём описания клипов, которые Unity вытащил из файла, и
                // правим их. Если брать defaultClipAnimations, правки уйдут
                // в пустоту: это копия, importer читает clipAnimations.
                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0)
                    clips = importer.defaultClipAnimations;

                bool dirty = false;

                for (int i = 0; i < clips.Length; i++)
                {
                    bool shouldLoop = wanted.Contains(clips[i].name);

                    if (clips[i].loopTime != shouldLoop)
                    {
                        clips[i].loopTime = shouldLoop;
                        dirty = true;
                        if (shouldLoop) changed++;
                    }
                }

                if (dirty)
                {
                    importer.clipAnimations = clips;
                    importer.SaveAndReimport();
                }
            }

            return changed;
        }

        /// <summary>
        /// Масштаб считается по фактической высоте модели, а не подбирается
        /// числом: у разных персонажей набора рост отличается (скелет-воин
        /// выше прислужника), и один коэффициент на всех сделал бы их
        /// одинаковыми — то есть стёр бы разницу, которую автор заложил.
        /// </summary>
        private static int PrepareCharacters()
        {
            int changed = 0;

            // Самый высокий в наборе задаёт мерку. Тогда пропорции между
            // персонажами сохраняются, а весь набор целиком садится в наш
            // масштаб.
            float tallest = 0f;

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharactersFolder }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                float h = MeasureHeight(go);
                if (h > tallest) tallest = h;
            }

            if (tallest < 0.01f)
            {
                Debug.LogError("[IsoRPG] Не удалось измерить рост моделей KayKit.");
                return 0;
            }

            // Самого высокого приводим чуть выше цели: он должен возвышаться.
            float factor = (TargetHeight * 1.15f) / tallest;
            lastScaleFactor = factor;

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharactersFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                if (Mathf.Abs(importer.globalScale - factor) > 0.001f)
                {
                    importer.globalScale = factor;
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            Debug.Log("[IsoRPG] Самый высокий в наборе: " + tallest.ToString("0.00") +
                      " м, коэффициент " + factor.ToString("0.000") + ".");

            return changed;
        }

        /// <summary>
        /// Приводит окружение к тому же масштабу, что и персонажей.
        ///
        /// Коэффициент берём тот же самый, что вычислен по самому высокому
        /// персонажу набора. Если масштабы разойдутся, это будет видно сразу
        /// (дверные проёмы перестанут соответствовать росту), но искать
        /// причину придётся долго — выглядеть будет как «модели кривые».
        /// </summary>
        private static int PrepareEnvironment()
        {
            float factor = lastScaleFactor;

            if (factor <= 0.01f)
            {
                Debug.LogWarning("[IsoRPG] Масштаб персонажей неизвестен — окружение не тронуто.");
                return 0;
            }

            int changed = 0;

            foreach (var folder in EnvironmentFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null) continue;

                    if (Mathf.Abs(importer.globalScale - factor) > 0.001f)
                    {
                        importer.globalScale = factor;
                        importer.SaveAndReimport();
                        changed++;
                    }
                }
            }

            return changed;
        }

        private static float MeasureHeight(GameObject go)
        {
            if (go == null) return 0f;

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 0f;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            return bounds.size.y;
        }
    }
}
