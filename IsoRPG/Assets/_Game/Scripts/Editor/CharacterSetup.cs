using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Готовит модель и анимации Mixamo к работе: настраивает импорт,
    /// собирает контроллер анимаций и создаёт префаб персонажа.
    ///
    /// Всё скриптом, а не руками по галочкам, по одной причине: настроек
    /// десятки, и любая забытая проявится не сразу, а странным поведением
    /// в игре. Скрипт ставит их одинаково каждый раз.
    /// </summary>
    public static class CharacterSetup
    {
        private const string CharactersFolder = "Assets/_Game/Art/Characters";
        private const string AnimationsFolder = "Assets/_Game/Art/Animations";
        private const string ControllerPath = "Assets/_Game/Art/Characters/AC_Player.controller";
        private const string PrefabPath = "Assets/_Game/Prefabs/Player.prefab";

        // Какие анимации на какие роли. Ищем по куску имени без учёта регистра,
        // потому что Mixamo зовёт файлы вида "X Bot@Knife Idle.fbx".
        private const string ClipIdle = "Knife Idle";
        private const string ClipWalk = "Walking";
        private const string ClipRun = "Running";
        private const string ClipAttack = "Sword And Shield Slash";
        private const string ClipStealthKill = "Brutal Assassination";
        private const string ClipDeath = "Dying";

        // Скорости, на которых переключаются состояния движения.
        // Должны совпадать со скоростью NavMeshAgent, иначе ноги поедут.
        private const float WalkSpeed = 2f;
        private const float RunSpeed = 5.5f;

        // Сколько должно длиться разовое действие — удар, добивание.
        // Держим короче интервала между ударами (1.4 с), чтобы анимация
        // успевала закончиться до следующей и не наслаивалась.
        private const float TargetActionDuration = 1.3f;

        // Скорость, выше которой начатое действие считается прерванным.
        // Порог выше скорости шага (2) — прерываем только на явном беге,
        // иначе доводка позиции у цели считается «побежал».
        private const float MoveInterruptSpeed = 2.5f;

        // Доля клипа от начала, в течение которой прерывать нельзя.
        // Защита от вылета в тот же кадр, пока сглаженная скорость падает.
        private const float InterruptGuard = 0.15f;

        // Предел ускорения анимации. Выше примерно полутора раз движение
        // человека начинает читаться как перемотка — проверено на глаз
        // Павлоном: «слишком быстро, выглядит нереалистично».
        private const float MaxSpeedUp = 1.5f;

        [MenuItem("Tools/IsoRPG/Собрать персонажа", priority = 10)]
        public static void Build()
        {
            // В режиме Play ассеты не пересобираются: изменения уйдут в
            // никуда при остановке игры.
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] Останови игру — в режиме Play персонаж не пересобирается.");
                return;
            }

            string modelPath = FindCharacterModel();
            if (modelPath == null)
            {
                Debug.LogError("[IsoRPG] В " + CharactersFolder + " нет ни одного .fbx с моделью.");
                return;
            }

            Debug.Log("[IsoRPG] Модель: " + Path.GetFileName(modelPath));

            // 1. Модель — человеческий скелет, аватар создаётся из неё самой.
            var modelAvatar = ConfigureModel(modelPath);
            if (modelAvatar == null)
            {
                Debug.LogError("[IsoRPG] Не удалось получить аватар из модели. " +
                               "Обычно значит, что Mixamo отдал не гуманоида.");
                return;
            }

            // 2. Анимации — тот же скелет, аватар копируется от модели.
            var clips = ConfigureAnimations(modelAvatar);
            Debug.Log("[IsoRPG] Настроено анимаций: " + clips.Count);

            // 3. Контроллер: дерево движения плюс разовые действия.
            var controller = BuildController(clips);

            // 4. Префаб, готовый к постановке в сцену.
            BuildPrefab(modelPath, controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[IsoRPG] Персонаж собран: " + PrefabPath);
        }

        // ------------------------------------------------------------------

        private static string FindCharacterModel()
        {
            return AssetDatabase.FindAssets("t:Model", new[] { CharactersFolder })
                                .Select(AssetDatabase.GUIDToAssetPath)
                                .FirstOrDefault(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase));
        }

        private static Avatar ConfigureModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;   // анимации приходят отдельными файлами
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;

            // Mixamo отдаёт материалы, которые в URP выглядят розовыми:
            // их шейдер из встроенного рендера. Пусть Unity создаст свои.
            //
            // Свойство materialLocation не трогаем: External объявлено
            // устаревшим, а поведение по умолчанию (материалы внутри модели)
            // нас устраивает — Unity всё равно вытащил текстуры отдельно.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

            importer.SaveAndReimport();

            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        }

        private static Dictionary<string, AnimationClip> ConfigureAnimations(Avatar sourceAvatar)
        {
            var result = new Dictionary<string, AnimationClip>();

            var paths = AssetDatabase.FindAssets("t:Model", new[] { AnimationsFolder })
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase));

            foreach (string path in paths)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);

                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = sourceAvatar;
                importer.importAnimation = true;

                // В файле анимации меша нет, материалы там ни к чему.
                // Свойство importMaterials устарело — в Unity 6 это materialImportMode.
                importer.materialImportMode = ModelImporterMaterialImportMode.None;

                string fileName = Path.GetFileNameWithoutExtension(path);
                bool shouldLoop = Contains(fileName, ClipIdle)
                                  || Contains(fileName, ClipWalk)
                                  || Contains(fileName, ClipRun);

                var clipSettings = importer.defaultClipAnimations;
                if (clipSettings != null && clipSettings.Length > 0)
                {
                    for (int i = 0; i < clipSettings.Length; i++)
                    {
                        clipSettings[i].loopTime = shouldLoop;

                        // Корневое движение гасим: персонажа двигает навигация,
                        // а не анимация. Иначе они тянут в разные стороны и
                        // персонаж скользит или уезжает от своего агента.
                        clipSettings[i].lockRootRotation = true;
                        clipSettings[i].keepOriginalPositionY = true;
                        clipSettings[i].lockRootHeightY = true;
                    }
                    importer.clipAnimations = clipSettings;
                }

                importer.SaveAndReimport();

                var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                        .OfType<AnimationClip>()
                                        .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

                if (clip != null) result[fileName] = clip;
            }

            return result;
        }

        // ------------------------------------------------------------------

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            EnsureFolder(Path.GetDirectoryName(ControllerPath));

            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("StealthKill", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            // Множитель скорости удара. Через него боевая система подгоняет
            // длительность анимации под скорость оружия: кинжал бьёт чаще,
            // и клип поджимается, чтобы не было пауз между ударами.
            var attackSpeed = new AnimatorControllerParameter
            {
                name = "AttackSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f
            };
            controller.AddParameter(attackSpeed);

            var root = controller.layers[0].stateMachine;

            // Движение одним деревом смешивания: стойка перетекает в шаг,
            // шаг в бег, по одному числу — текущей скорости. Это плавнее и
            // проще, чем три состояния с переходами между ними.
            var moveTree = new BlendTree
            {
                name = "Locomotion",
                blendParameter = "Speed",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(moveTree, controller);

            AddMotion(moveTree, clips, ClipIdle, 0f);
            AddMotion(moveTree, clips, ClipWalk, WalkSpeed);
            AddMotion(moveTree, clips, ClipRun, RunSpeed);

            var moveState = root.AddState("Locomotion");
            moveState.motion = moveTree;
            root.defaultState = moveState;

            AddOneShot(controller, root, moveState, clips, ClipAttack, "Attack", "Attack");
            AddOneShot(controller, root, moveState, clips, ClipStealthKill, "StealthKill", "StealthKill");
            AddDeath(controller, root, clips);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddMotion(BlendTree tree, Dictionary<string, AnimationClip> clips,
                                      string key, float threshold)
        {
            var clip = Find(clips, key);
            if (clip == null)
            {
                Debug.LogWarning("[IsoRPG] Не найдена анимация со словом «" + key + "» — состояние пропущено.");
                return;
            }
            tree.AddChild(clip, threshold);
        }

        private static void AddOneShot(AnimatorController controller, AnimatorStateMachine root,
                                       AnimatorState returnTo, Dictionary<string, AnimationClip> clips,
                                       string key, string trigger, string stateName)
        {
            var clip = Find(clips, key);
            if (clip == null)
            {
                Debug.LogWarning("[IsoRPG] Не найдена анимация «" + key + "» — действие " + trigger + " не собрано.");
                return;
            }

            var state = root.AddState(stateName);
            state.motion = clip;

            // Скорость состояния домножается на параметр: базовое ускорение
            // клипа задаётся ниже, а ритм боя накидывается поверх в рантайме.
            state.speedParameterActive = true;
            state.speedParameter = "AttackSpeed";

            // Mixamo отдаёт связки на несколько секунд — «комбо» из трёх
            // ударов вместо одного. Ускорять их сильно нельзя: движение
            // становится суетливым и сразу читается как подделка. Поэтому
            // жмём умеренно, а если клип всё равно длинный — честно говорим
            // об этом в консоль. Правильное лечение здесь — короткий клип.
            if (clip.length > TargetActionDuration)
            {
                float needed = clip.length / TargetActionDuration;
                state.speed = Mathf.Min(needed, MaxSpeedUp);

                float actual = clip.length / state.speed;

                if (needed > MaxSpeedUp)
                {
                    Debug.LogWarning(
                        $"[IsoRPG] «{key}» длится {clip.length:0.00} с — это связка, а не одиночный удар. " +
                        $"Ускорено в {state.speed:0.0} раза до {actual:0.00} с (сильнее нельзя — будет суета). " +
                        $"Для нормального боя нужна короткая анимация одиночного удара.");
                }
                else
                {
                    Debug.Log($"[IsoRPG] «{key}»: {clip.length:0.00} с → {actual:0.00} с " +
                              $"(ускорение {state.speed:0.0}).");
                }
            }

            var enter = returnTo.AddTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = 0.05f;   // почти мгновенно: удар должен отзываться сразу

            var exit = state.AddTransition(returnTo);
            exit.hasExitTime = true;
            exit.exitTime = 0.8f;     // возвращаемся, не досматривая хвост
            exit.duration = 0.12f;

            // Побежал — удар прерывается. Без этого персонаж, которого увели
            // от цели сразу после замаха, доигрывает взмах по воздуху на бегу.
            //
            // ВАЖНО: прерывание разрешено только после InterruptGuard от начала
            // клипа. Скорость в аниматоре сглажена и после остановки падает не
            // сразу — без этой защиты состояние удара вылетает обратно в тот же
            // кадр, в котором вошло. Со стороны это выглядит как пауза: замаха
            // нет, а урон уже прошёл, потому что он считается своим таймером.
            var interrupt = state.AddTransition(returnTo);
            interrupt.AddCondition(AnimatorConditionMode.Greater, MoveInterruptSpeed, "Speed");
            interrupt.hasExitTime = true;
            interrupt.exitTime = InterruptGuard;
            interrupt.duration = 0.1f;
        }

        private static void AddDeath(AnimatorController controller, AnimatorStateMachine root,
                                     Dictionary<string, AnimationClip> clips)
        {
            var clip = Find(clips, ClipDeath);
            if (clip == null) return;

            var state = root.AddState("Death");
            state.motion = clip;

            var toDeath = root.AddAnyStateTransition(state);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            toDeath.hasExitTime = false;
            toDeath.duration = 0.1f;
            toDeath.canTransitionToSelf = false;
        }

        private static AnimationClip Find(Dictionary<string, AnimationClip> clips, string key)
        {
            foreach (var pair in clips)
                if (Contains(pair.Key, key)) return pair.Value;
            return null;
        }

        private static bool Contains(string haystack, string needle) =>
            haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;

        // ------------------------------------------------------------------

        private static void BuildPrefab(string modelPath, AnimatorController controller)
        {
            EnsureFolder(Path.GetDirectoryName(PrefabPath));

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "PlayerVisual";

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;   // двигает навигация, не анимация

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
