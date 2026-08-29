using System.Collections.Generic;
using System.IO;
using System.Linq;
using IsoRPG.Dev;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Живая витрина: все купленные персонажи ходят по полю, каждый со своей
    /// походкой.
    ///
    /// Раньше витрина была одна на всех и с одним набором клипов —
    /// гоблинским. Отсюда жалоба «ходят как гориллы»: на короля, крестьянку
    /// и скелета ложился один и тот же сгорбленный шаг.
    ///
    /// Теперь походок три, и все из уже купленного:
    ///
    /// * Люди — клипы KayKit. Они человеческие: прямая спина, нормальный шаг.
    ///   Стали доступны после перевода рига KayKit в Humanoid: до этого клип
    ///   был намертво привязан к своим костям.
    /// * Нежить — та же пачка KayKit, но её собственный раздел «скелеты»:
    ///   шаркающий шаг, своя стойка. Авторы нарисовали их отдельно, и это
    ///   ровно то, что нужно.
    /// * Гоблины, демоны, големы — гоблинский набор Synty. Сгорбленная
    ///   походка тут наконец на своём месте.
    ///
    /// Ставим на поле, проверенное щупом навигации: 33 на 33 метра без единой
    /// дырки вокруг точки X -1, Z 30. Выбор не случайный — витрина, половина
    /// которой стоит в непроходимом кусте, показывает не персонажей, а баг.
    /// </summary>
    public static class LivingShowcase
    {
        private const string HolderName = "LivingShowcase";
        private const string Data = "Assets/_Game/Data";

        private const string GoblinAnim =
            "Assets/Synty/AnimationGoblinLocomotion/Animations/Polygon/Neutral";

        /// <summary>Родная человеческая локомоция Synty — под их пропорции.</summary>
        private const string BaseAnim =
            "Assets/Synty/AnimationBaseLocomotion/Animations/Polygon/Masculine";

        /// <summary>
        /// Большой набор Mecanim: 1198 клипов по видам оружия.
        ///
        /// Берём отсюда то, чего нет у Synty. Смешивать наборы можно свободно
        /// — оба Humanoid, а humanoid-клип не помнит, с какого скелета снят.
        /// </summary>
        private const string Mecanim =
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations";

        /// <summary>Центр витрины — проверенное чистое поле.</summary>
        private static readonly Vector3 Centre = new Vector3(-1f, 0f, 30f);

        /// <summary>Шаг сетки, метров.</summary>
        private const float Step = 4.2f;

        private static readonly string[] Folders =
        {
            "Assets/Synty/PolygonFantasyCharacters/Prefabs",
            "Assets/Synty/PolygonDungeonRealms/Prefabs/Characters",
            "Assets/Synty/PolygonFantasyKingdom/Prefabs/Characters",
            "Assets/PolygonElvenRealm/Prefabs/Characters",
            "Assets/PolygonDungeon/Prefabs/Characters",
        };

        /// <summary>Не персонажи: детали одежды, причёски, реквизит.</summary>
        private static readonly string[] NotPeople =
        {
            "Attach", "Hair", "SM_Prop", "Prop_", "Quiver",
        };

        private static readonly string[] UndeadWords =
        {
            "Skeleton", "Undead", "Ghost", "Tormented", "Lich", "Zombie",
        };

        private static readonly string[] BeastWords =
        {
            "Goblin", "Demon", "Golem", "Orc", "Troll", "Ogre",
        };

        // ------------------------------------------------------------------

        [MenuItem("Tools/IsoRPG/Живая витрина: все персонажи", priority = 66)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play изменения сцены не сохраняются.", "Понятно");
                return;
            }

            Clear();
            SyntyCast.Clear();

            var human = HumanController();
            var undead = UndeadController();
            var beast = BeastController();

            if (human == null || undead == null || beast == null)
            {
                Debug.LogError("[IsoRPG] Не собрались контроллеры — витрина была бы " +
                               "из столбов. Смотри, каких клипов не нашлось.");
                return;
            }

            var people = Gather();

            if (people.Count == 0)
            {
                Debug.LogError("[IsoRPG] Персонажей не нашлось — проверь пути к наборам.");
                return;
            }

            var holder = new GameObject(HolderName);

            int columns = Mathf.CeilToInt(Mathf.Sqrt(people.Count));
            int rows = Mathf.CeilToInt(people.Count / (float)columns);

            int placed = 0, skipped = 0;
            int humans = 0, undeads = 0, beasts = 0;

            for (int i = 0; i < people.Count; i++)
            {
                string path = people[i];
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (asset == null) continue;

                int column = i % columns;
                int row = i / columns;

                Vector3 at = Centre + new Vector3(
                    (column - (columns - 1) * 0.5f) * Step,
                    0f,
                    (row - (rows - 1) * 0.5f) * Step);

                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder.transform);
                go.transform.position = at;
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                var animator = go.GetComponentInChildren<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();

                // Без человеческого скелета humanoid-клип не наденется, и
                // персонаж встанет буквой Т. Молча пропускать нельзя — именно
                // так и рождается «половина витрины сломана, а почему никто
                // не знает».
                if (animator.avatar == null || !animator.avatar.isHuman)
                {
                    Debug.LogWarning("[IsoRPG] " + Path.GetFileNameWithoutExtension(path) +
                                     " не Humanoid — поставлен без походки.");
                    skipped++;
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path);

                AnimatorController pick;
                string style;

                if (UndeadWords.Any(w => name.Contains(w)))
                { pick = undead; style = "AC_Show_Undead"; undeads++; }
                else if (BeastWords.Any(w => name.Contains(w)))
                { pick = beast; style = "AC_Show_Beast"; beasts++; }
                else
                { pick = human; style = "AC_Show_Human"; humans++; }

                animator.runtimeAnimatorController = pick;
                animator.applyRootMotion = false;

                var agent = go.GetComponent<NavMeshAgent>();
                if (agent == null) agent = go.AddComponent<NavMeshAgent>();

                // Скорость агента = скорость, на которую нарисован клип.
                //
                // Это и есть лекарство от скольжения, и разброс тут нельзя
                // делать «просто так»: разгони агента на двадцать процентов —
                // ноги отстанут ровно на двадцать. Поэтому разнобой даём
                // ОБОИМ сразу: и телу, и проигрывателю анимации. Тогда один
                // идёт быстрее другого только в кадре, а не в ногах.
                float paced = WalkSpeed.TryGetValue(style, out float found) ? found : 1.6f;
                float variety = Random.Range(0.85f, 1.15f);

                agent.speed = paced * variety;
                animator.speed = variety;
                agent.angularSpeed = 480f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 0.2f;
                agent.radius = 0.4f;
                agent.height = 1.9f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

                // Бродят близко к своему месту: с большим радиусом полсотни
                // фигур сбиваются в кучу посреди поля, и рассмотреть нельзя
                // никого.
                var wander = go.AddComponent<Wanderer>();
                wander.Range = 3.5f;

                placed++;
            }

            DropDummy();

            NavBake.Rebake();

            Selection.activeGameObject = holder;

            Debug.Log("[IsoRPG] Живая витрина: " + placed + " персонажей в " + Centre +
                      "  (людей " + humans + ", нежити " + undeads + ", тварей " + beasts +
                      (skipped > 0 ? ", без походки " + skipped : "") + ")." +
                      "  Сетка " + columns + " в ряд, шаг " + Step + " м.");
        }

        [MenuItem("Tools/IsoRPG/Живая витрина: убрать", priority = 67)]
        public static void Clear()
        {
            var old = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                            .FirstOrDefault(g => g.name == HolderName);

            if (old != null) Object.DestroyImmediate(old);
        }

        // ------------------------------------------------------------------

        private static List<string> Gather()
        {
            var found = new List<string>();

            foreach (var folder in Folders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                foreach (var path in Directory.GetFiles(folder, "*.prefab",
                                                        SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileNameWithoutExtension(path);

                    if (NotPeople.Any(s => name.Contains(s))) continue;

                    found.Add(path.Replace(Path.DirectorySeparatorChar, '/'));
                }
            }

            return found.OrderBy(p => Path.GetFileNameWithoutExtension(p)).ToList();
        }

        // ---- контроллеры --------------------------------------------------

        /// <summary>
        /// Скорость, на которую НАРИСОВАН клип шага, метров в секунду.
        ///
        /// Это и есть лекарство от скольжения. Персонаж скользит не потому,
        /// что анимация плохая, а потому что тело везут с одной скоростью, а
        /// ноги переступают с другой. Совпасть они обязаны точно, и подобрать
        /// это на глаз нельзя: разница в двадцать процентов уже видна, а на
        /// глаз двадцать процентов не отличить.
        ///
        /// Unity считает эту скорость сама и хранит в клипе: `averageSpeed` —
        /// смещение корня за секунду. У клипа «на месте» она нулевая, и тогда
        /// спрашиваем `apparentSpeed` — оценку по шагу ног.
        /// </summary>
        /// <summary>Откуда взялся каждый клип — нужно, чтобы найти его двойник.</summary>
        private static readonly Dictionary<AnimationClip, string> ClipPath =
            new Dictionary<AnimationClip, string>();

        private static float Ground(AnimationClip clip)
        {
            if (clip == null) return 0f;

            float root = new Vector3(clip.averageSpeed.x, 0f, clip.averageSpeed.z).magnitude;

            if (root > 0.15f) return root;
            if (clip.apparentSpeed > 0.15f) return clip.apparentSpeed;

            // У Synty рядом с каждым клипом «на месте» лежит его двойник с
            // корневым движением. Это подарок: у двойника скорость записана
            // точно, а замер по ступням — всего лишь оценка, и на беге она
            // врёт. Первый заход дал человеку бег 1.50 м/с, то есть медленнее
            // собственного шага; по двойнику такого не выйдет.
            if (ClipPath.TryGetValue(clip, out string path))
            {
                string twin = path
                    .Replace("_Masc.fbx", "_RootMotion_Masc.fbx")
                    .Replace("_Femn.fbx", "_RootMotion_Femn.fbx")
                    .Replace("_Neut.fbx", "_RootMotion_Neut.fbx");

                if (twin != path)
                {
                    var moving = AssetDatabase.LoadAllAssetsAtPath(twin)
                                              .OfType<AnimationClip>()
                                              .FirstOrDefault(c => !c.name.StartsWith("__preview"));

                    if (moving != null)
                    {
                        float exact = new Vector3(moving.averageSpeed.x, 0f,
                                                  moving.averageSpeed.z).magnitude;

                        if (exact > 0.15f) return exact;
                    }
                }
            }

            // Клип снят «на месте»: корень стоит, и обе быстрые оценки дают
            // ноль. Тогда меряем ШАГ — по ступням.
            return Stride(clip);
        }

        /// <summary>
        /// Скорость клипа, снятая по расхождению ступней.
        ///
        /// Все наши клипы ходьбы сняты «на месте»: персонаж перебирает
        /// ногами, а корень не движется — так и надо, иначе анимация уезжает
        /// от навигационного агента. Но у этого есть цена: Unity не может
        /// сказать, на какую скорость клип нарисован, и оба её поля отдают
        /// ноль. Дальше обычно ставят число на глаз — и получают скольжение,
        /// потому что на глаз двадцать процентов не отличить, а видно их
        /// сразу.
        ///
        /// Меряем сами и по-честному. Проигрываем клип на модели покадрово и
        /// смотрим, насколько далеко расходятся ступни в самой широкой фазе.
        /// Это длина шага. За один цикл человек делает два шага, значит
        /// проходит две таких длины, а цикл — это и есть длительность клипа.
        ///
        /// Модель нужна с человеческим скелетом: у неё Unity знает, где
        /// ступни. Берём первую попавшуюся из витрины — пропорции у Synty
        /// общие, разница в сантиметрах.
        /// </summary>
        private static float Stride(AnimationClip clip)
        {
            var model = StrideDummy();

            if (model == null) return 0f;

            var animator = model.GetComponentInChildren<Animator>();

            var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            if (left == null || right == null) return 0f;

            float widest = 0f;
            const int Frames = 40;

            for (int i = 0; i < Frames; i++)
            {
                clip.SampleAnimation(model, clip.length * i / (Frames - 1f));

                Vector3 a = left.position, b = right.position;
                float apart = new Vector2(a.x - b.x, a.z - b.z).magnitude;

                if (apart > widest) widest = apart;
            }

            // Два шага за цикл.
            float speed = widest * 2f / Mathf.Max(clip.length, 0.01f);

            return speed;
        }

        private static GameObject strideDummy;

        private static GameObject StrideDummy()
        {
            if (strideDummy != null) return strideDummy;

            foreach (var path in Gather())
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;

                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                go.transform.position = new Vector3(0f, 5000f, 0f);
                go.hideFlags = HideFlags.HideAndDontSave;

                var animator = go.GetComponentInChildren<Animator>();

                if (animator != null && animator.avatar != null && animator.avatar.isHuman)
                {
                    strideDummy = go;
                    return go;
                }

                Object.DestroyImmediate(go);
            }

            Debug.LogWarning("[IsoRPG] Не нашлось humanoid-модели для замера шага.");
            return null;
        }

        private static void DropDummy()
        {
            if (strideDummy != null) Object.DestroyImmediate(strideDummy);
            strideDummy = null;
        }

        /// <summary>Скорость шага у каждой походки — заполняется при сборке.</summary>
        private static readonly Dictionary<string, float> WalkSpeed =
            new Dictionary<string, float>();

        /// <summary>
        /// Люди — родной локомоцией Synty.
        ///
        /// Раньше здесь стояли клипы KayKit. Они человеческие и выручили,
        /// пока другого не было, но рядом с моделями Synty видно, что сняты
        /// они на другие пропорции: шаг короче, руки живут отдельно. Родной
        /// набор снят на этот же скелет.
        /// </summary>
        private static AnimatorController HumanController()
        {
            return Locomotion("AC_Show_Human",
                Clip(BaseAnim + "/Idle/A_Idle_Standing_Masc.fbx", null),
                Clip(BaseAnim + "/Locomotion/Walk/A_Walk_F_Masc.fbx", null),
                Clip(BaseAnim + "/Locomotion/Run/A_Run_F_Masc.fbx", null),
                1.4f, 3.5f);
        }

        /// <summary>
        /// Нежить — раненой походкой из набора Mecanim.
        ///
        /// В родном наборе Synty шаркающего шага нет вовсе, и придумывать его
        /// не из чего. Зато в Mecanim лежит «раненый»: подволакивает ногу,
        /// корпус завален. Для скелета и упыря это ровно то, что нужно, и
        /// брать чужое тут не компромисс, а правильный ход — оба набора
        /// Humanoid, и клип не помнит, с какого скелета снят.
        /// </summary>
        private static AnimatorController UndeadController()
        {
            var idle = Clip(Mecanim + "/Unarmed/RPG-Character@Unarmed-Idle-Injured1.FBX", null);
            var walk = Clip(Mecanim + "/Unarmed/RPG-Character@Unarmed-Walk-Injured.FBX", null);

            // Бега у нежити нет намеренно: ковыляющий скелет, срывающийся на
            // спринт, читается как баг. Верхний порог даём тем же клипом.
            return Locomotion("AC_Show_Undead", idle, walk, walk, 1.0f, 3f);
        }

        private static AnimatorController BeastController()
        {
            return Locomotion("AC_Show_Beast",
                Clip(GoblinAnim + "/Idles/A_POLY_GBL_Idle_Standing_Neut.fbx", null),
                Clip(GoblinAnim + "/Locomotion/Walk/A_POLY_GBL_Walk_F_Neut.fbx", null),
                Clip(GoblinAnim + "/Locomotion/Run/A_POLY_GBL_Run_F_Neut.fbx", null),
                2f, 5.5f);
        }

        /// <summary>
        /// Простое дерево смешивания по скорости: стойка, шаг, бег.
        ///
        /// Пороги — это скорости, на которых существо ЕЗДИТ, а не круглые
        /// числа. Ошибка здесь даёт ноги, скользящие по земле: клип бега
        /// играет на скорости, которой у агента никогда не бывает.
        /// </summary>
        private static AnimatorController Locomotion(string name,
                                                     AnimationClip idle,
                                                     AnimationClip walk,
                                                     AnimationClip run,
                                                     float walkAt,
                                                     float runAt)
        {
            if (idle == null || walk == null || run == null)
            {
                Debug.LogError("[IsoRPG] " + name + ": не хватило клипов — " +
                               (idle == null ? "стойка " : "") +
                               (walk == null ? "шаг " : "") +
                               (run == null ? "бег" : ""));
                return null;
            }

            Directory.CreateDirectory(Data);

            string path = Data + "/" + name + ".controller";
            AssetDatabase.DeleteAsset(path);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendParameter = "Speed",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            // Пороги — по ЗАМЕРУ клипа, а не по числам, которые я придумал.
            //
            // Придуманные и стояли: 1.8 и 4.5. Клипы KayKit нарисованы на
            // другую скорость, и от этого расхождения персонаж скользил —
            // ноги в одном темпе, тело в другом. Замер снимает вопрос совсем.
            float measuredWalk = Ground(walk);
            float measuredRun = Ground(run);

            if (measuredWalk > 0f) walkAt = measuredWalk;
            if (measuredRun > 0f && measuredRun > walkAt) runAt = measuredRun;

            WalkSpeed[name] = walkAt;

            Debug.Log("[IsoRPG] " + name + ": шаг " + walkAt.ToString("0.00") +
                      " м/с, бег " + runAt.ToString("0.00") + " м/с" +
                      (measuredWalk > 0f ? "  (замер по шагу ступней)" : "  (замерить не вышло, взяты наши числа)"));

            tree.AddChild(idle, 0f);
            tree.AddChild(walk, walkAt);
            tree.AddChild(run, runAt);

            var state = controller.layers[0].stateMachine.AddState("Locomotion");
            state.motion = tree;
            controller.layers[0].stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return controller;
        }

        /// <summary>
        /// Клип из FBX по имени, с зацикливанием.
        ///
        /// Имя пустое — берём первый: у наборов Synty в файле ровно один клип
        /// и назван он по файлу, а у KayKit в одном файле их два десятка.
        ///
        /// Зацикливание обязательно: незацикленный шаг играет один раз и
        /// застывает, пока агент везёт тело дальше, — существо «едет по
        /// земле». Мы это уже проходили дважды.
        /// </summary>
        private static AnimationClip Clip(string path, string wanted)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null)
            {
                Debug.LogWarning("[IsoRPG] Нет файла анимаций " + path);
                return null;
            }

            var clips = importer.defaultClipAnimations;
            bool changed = false;

            for (int i = 0; i < clips.Length; i++)
            {
                bool mine = string.IsNullOrEmpty(wanted) || clips[i].name == wanted;

                if (mine && !clips[i].loopTime) { clips[i].loopTime = true; changed = true; }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            var found = AssetDatabase.LoadAllAssetsAtPath(path)
                                     .OfType<AnimationClip>()
                                     .Where(c => !c.name.StartsWith("__preview"))
                                     .FirstOrDefault(c => string.IsNullOrEmpty(wanted) || c.name == wanted);

            if (found != null) ClipPath[found] = path;

            return found;
        }
    }
}
