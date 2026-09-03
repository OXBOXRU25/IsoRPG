using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Переводит ход и прыжок героя на новый набор анимаций.
    ///
    /// Первый шаг перехода, и намеренно осторожный: **машину состояний не
    /// трогаем, меняем только клипы**. Дерево хода как было одномерным, так и
    /// осталось; прыжок как был трёхфазным, так и остался — у автора набора
    /// ровно та же схема (`Jump_Start` → `Jump_Air_Loop` → `Jump_End`).
    /// Значит ломаться нечему, а разница в пластике видна сразу.
    ///
    /// Инерция — то, ради чего набор и брали, — сюда НЕ входит. Клипы
    /// переходов между направлениями (`Run_F_L90_A_To_F_R90_B` и ещё 53 таких)
    /// требуют другого дерева и новых параметров, которые герою пока никто не
    /// считает. Это отдельный заход: смешивать подмену клипов с переписыванием
    /// машины значит потом гадать, что из двух сломалось.
    ///
    /// Играем версии `_InPlace` — без корневого движения: героя ведёт капсула,
    /// и клип, который везёт сам, уехал бы от неё. А пороги дерева меряем по
    /// ОБЫЧНЫМ версиям: там корневое движение есть, и оно говорит, с какой
    /// скоростью автор нарисовал этот шаг.
    /// </summary>
    public static class HeroMoveKit
    {
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        private const string ControllerPath =
            "Assets/_Game/Art/Animations/Controllers/AC_Hero_Sidekick.controller";

        /// <summary>
        /// С какой скоростью герой РЕАЛЬНО бегает, метры в секунду. Скорость
        /// его навигационного агента.
        ///
        /// Пороги дерева обязаны стоять на ней, а не на скорости клипа.
        /// Первый заход поставил их по клипам — бег 5.10, спринт 7.28, — и
        /// при беге на 5.5 дерево держало смесь 82% бега и 18% спринта.
        /// Спринт это широкий вынос ног, и подмешанный к бегу он дал
        /// заплетающуюся походку. Павлон 03.09.2026: «бегает как паралитик,
        /// старый дед с радикулитом».
        ///
        /// Клип при этом подтягивается по времени: играет во столько раз
        /// быстрее или медленнее, во сколько его своя скорость отличается от
        /// нашей. Тогда и ноги совпадают с землёй, и смеси нет.
        /// </summary>
        private const float HeroSpeed = 5.5f;

        /// <summary>Прибавка спринта — способность даёт +70%.</summary>
        private const float SprintFactor = 1.7f;

        /// <summary>
        /// Ход берём ВООРУЖЁННЫЙ, а не общий.
        ///
        /// Первый заход взял общий раздел движения — безоружный бег со
        /// свободными махами руками и раскачкой корпуса. У героя в руках два кинжала, и
        /// со стороны это читалось как виляние: Павлон 03.09.2026 «при беге
        /// персонаж как-то сильно задницей вилял». У набора для этого есть
        /// свой раздел: руки при оружии прижаты, корпус собран.
        ///
        /// Тот же случай, что и с пальцами: у автора нужное лежит отдельной
        /// папкой, и брать общее вместо специального — моя ошибка, а не
        /// свойство набора.
        /// </summary>
        private const string Move = "Assets/DoubleL/FBX_Animations/One Hand Base/Movement";
        private const string Jump = "Assets/DoubleL/FBX_Animations/One Hand Base/Jump";

        /// <summary>
        /// Мирный ход — безоружный раздел того же набора.
        ///
        /// Вооружённая стойка правильна в бою и только в нём. Вне боя она
        /// читается как «герой всё время крадётся»: колени подсогнуты, корпус
        /// подан вперёд, кисти держат несуществующую рукоять. Павлон
        /// 03.09.2026: «он всегда стоит немного согнувшись, поза в покое
        /// супер странная».
        ///
        /// Автор набора развёл фазы папками — `Base Move` и `One Hand Base`, —
        /// то есть переключение между ними и есть его замысел, а не наша
        /// выдумка. Мы же взяли одну ветку на все случаи, и это была моя
        /// ошибка дважды подряд: сперва общий ход вместо вооружённого, теперь
        /// вооружённый вместо обоих.
        /// </summary>
        private const string Peace = "Assets/DoubleL/FBX_Animations/Base Move";

        /// <summary>
        /// Набор передвижения Synty — родной для модели Sidekick.
        ///
        /// 346 клипов под нашего героя: ход и бег во все восемь сторон, в
        /// горку и с горки, разгон и торможение с поворотом, повороты на
        /// месте, прыжки по состоянию и аддитивные слои наклона и взгляда.
        /// Пока берём отсюда только прямой ход; остальное — следующими
        /// заходами, по одному, чтобы каждый можно было проверить отдельно.
        /// </summary>
        private const string Synty =
            "Assets/Synty/AnimationBaseLocomotion/Animations/Sidekick/Masculine";

        /// <summary>
        /// Параметр фазы: 0 — мирная пластика, 1 — боевая.
        ///
        /// Дробный намеренно. Будь он булевым, пришлось бы разводить фазы
        /// переходами машины состояний, а так внешнее дерево само смешивает
        /// одну стойку с другой, и вход в бой выглядит как то, чем он и
        /// является: герой подбирается, а не переключается кадром.
        /// </summary>
        public const string CombatParameter = "Stance";

        /// <summary>Сила удара о землю: 0 — мягко, 1 — с высоты.</summary>
        public const string FallParameter = "FallHard";

        [MenuItem("Tools/IsoRPG/Герой: ход и прыжок из нового набора", priority = 43)]
        public static void Apply()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                Debug.LogError("[IsoRPG] Нет контроллера героя: " + ControllerPath);
                return;
            }

            LoopMovement();

            // Ход целиком из набора Synty — родного для нашей модели.
            //
            // Решение Павла 04.09.2026 после того, как он спросил: «а
            // анимаций Synty для персонажа у нас нет?» Есть, и я их не
            // посмотрел — нарушив собственное правило «сперва проверь, что по
            // этой части уже есть в проекте». `AnimationBaseLocomotion` даёт
            // 346 клипов под Sidekick, то есть нарисованных на тот же скелет
            // и те же пропорции.
            //
            // Это и есть настоящая причина двух отвергнутых подряд бегов:
            // «виляние задом» у безоружного DoubleL и «оттопыренный зад» у
            // вооружённого — не плохие клипы, а чужие пропорции. Доворотом
            // такое не лечится, только сменой набора.
            //
            // ПРАВИЛО, которое сюда же: внутри ОДНОГО дерева смешивания
            // клипы должны быть из одного набора. Между состояниями — ход,
            // удар, прыжок — наборы мешать можно и нужно (удары кинжалами у
            // нас останутся из DoubleL, они хороши). А внутри дерева разные
            // пропорции блендятся друг с другом, и герой начинает дёргаться.
            // Спринт — вооружённый бег DoubleL, выбор Павла 04.09.2026 глазами
            // из четырёх вариантов примерки.
            //
            // Это единственное место, где я сознательно нарушаю правило «один
            // набор внутри дерева»: на разгоне между бегом и спринтом пойдёт
            // смесь двух пластик с разными пропорциями. Заказчик предупреждён;
            // увидит дёрганье — рядом лежит родной `A_MOD_BL_Sprint_F_Masc`,
            // замена в одну строку.
            var fight = BuildStride(controller, "Ход боевой", "",
                Move + "/Idle/Idle/OneHand_Base_Stand_Idle_A_1.fbx",
                Synty + "/Locomotion/Walk/A_MOD_BL_Walk_F_Masc.fbx",
                Synty + "/Locomotion/Run/A_MOD_BL_Run_F_Masc.fbx",
                Move + "/Run/Type A/Base/InPlace/OneHand_Base_Run_A_F_InPlace.fbx",
                Synty + "/Locomotion/Walk/A_MOD_BL_Walk_F_RM_Masc.fbx",
                Synty + "/Locomotion/Run/A_MOD_BL_Run_F_RM_Masc.fbx",
                Move + "/Run/Type A/Base/OneHand_Base_Run_A_F.fbx");

            if (fight == null)
            {
                Debug.LogError("[IsoRPG] Клипы хода Synty не нашлись — ход не переведён.");
                return;
            }

            // Боевая фаза отличается ТОЛЬКО стойкой покоя.
            //
            // У Synty в наборе передвижения боевой стойки нет вовсе — есть
            // одна нейтральная. Поэтому боевую берём у DoubleL: Павлон выбрал
            // её глазами из тринадцати вариантов примерки 04.09.2026 (ему
            // понравились первая, вторая и пятая; ставим первую, разнообразие
            // добавим отдельным механизмом).
            //
            // Смешение наборов здесь есть, но узкое: стойка блендится с шагом
            // только в полосе около полутора метров в секунду, а стоя и на
            // бегу играет чистый клип. Это не то же самое, что мешать бег с
            // бегом, где смесь идёт постоянно.
            var peace = BuildStride(controller, "Ход мирный", "",
                Synty + "/Idles/A_MOD_BL_Idle_Standing_Masc.fbx",
                Synty + "/Locomotion/Walk/A_MOD_BL_Walk_F_Masc.fbx",
                Synty + "/Locomotion/Run/A_MOD_BL_Run_F_Masc.fbx",
                null,
                Synty + "/Locomotion/Walk/A_MOD_BL_Walk_F_RM_Masc.fbx",
                Synty + "/Locomotion/Run/A_MOD_BL_Run_F_RM_Masc.fbx",
                null);

            if (!controller.parameters.Any(p => p.name == CombatParameter))
                controller.AddParameter(CombatParameter, AnimatorControllerParameterType.Float);

            // Внешнее дерево выбирает фазу, внутренние — аллюр.
            //
            // Два уровня вместо двух состояний с переходами: смешивание
            // достаётся даром, а машину состояний трогать не приходится
            // вовсе — значит и ломаться в ней нечему.
            var tree = new BlendTree
            {
                name = "Ход",
                blendType = BlendTreeType.Simple1D,
                blendParameter = CombatParameter,
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            // Мирной ветки может не оказаться — тогда ход остаётся боевым,
            // как был. Молча ронять весь ход из-за отсутствия одной папки
            // нельзя: герой встанет в позу T.
            // Одинаковые фазы — одно дитя, а не два: лишнее дерево в
            // смешивании считается каждый кадр у каждого игрока.
            tree.children = peace != null && peace != fight
                ? new[]
                  {
                      new ChildMotion { motion = peace, threshold = 0f, timeScale = 1f },
                      new ChildMotion { motion = fight, threshold = 1f, timeScale = 1f },
                  }
                : new[] { new ChildMotion { motion = fight, threshold = 0f, timeScale = 1f } };

            // --- подмена в состояниях ------------------------------------
            var jumpStart = Clip(Jump + "/InPlace/OneHand_Base_Jump_Start_InPlace.fbx")
                            ?? Clip(Jump + "/OneHand_Base_Jump_Start.fbx");

            var jumpAir = Clip(Jump + "/InPlace/OneHand_Base_Jump_Air_Loop_InPlace.fbx")
                          ?? Clip(Jump + "/OneHand_Base_Jump_Air_Loop.fbx");

            // Приземление в двух видах: мягкое и с высоты.
            //
            // Выбор Павла 04.09.2026: «для приземления 2, если с большой
            // высоты приземление 3». Клипы разные по глубине приседа, и
            // смешивать их по силе удара честнее, чем переключать: прыжок с
            // бордюра и падение со скалы — разные события, а не два состояния.
            var landSoft = Clip(Jump + "/InPlace/OneHand_Base_Jump_End_2_InPlace.fbx");
            var landHard = Clip(Jump + "/InPlace/OneHand_Base_Jump_End_3_InPlace.fbx");

            Motion jumpLand = landSoft;

            if (landSoft != null && landHard != null)
            {
                if (!controller.parameters.Any(p => p.name == FallParameter))
                    controller.AddParameter(FallParameter, AnimatorControllerParameterType.Float);

                var landTree = new BlendTree
                {
                    name = "Приземление",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = FallParameter,
                    useAutomaticThresholds = false,
                };

                AssetDatabase.AddObjectToAsset(landTree, controller);

                landTree.children = new[]
                {
                    new ChildMotion { motion = landSoft, threshold = 0f, timeScale = 1f },
                    new ChildMotion { motion = landHard, threshold = 1f, timeScale = 1f },
                };

                jumpLand = landTree;
            }

            int moved = 0, jumped = 0;

            foreach (var layer in controller.layers)
            {
                if (layer.stateMachine == null) continue;

                foreach (var child in layer.stateMachine.states)
                {
                    var state = child.state;

                    switch (state.name)
                    {
                        case "Locomotion":
                            state.motion = tree;
                            moved++;
                            break;

                        case "Jump_Start":
                            if (jumpStart != null) { state.motion = jumpStart; jumped++; }
                            break;

                        case "Jump_Air":
                            if (jumpAir != null) { state.motion = jumpAir; jumped++; }
                            break;

                        case "Jump_Land":
                            if (jumpLand != null) { state.motion = jumpLand; jumped++; }
                            break;
                    }
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            LockJaw();

            Debug.Log($"[IsoRPG] Ход героя переведён: деревьев {moved}, фаз прыжка {jumped} из 3. " +
                      $"Фазы: боевая есть, мирная {(peace != null ? "есть" : "НЕ НАЙДЕНА")}; " +
                      $"переключает параметр «{CombatParameter}».");
        }

        /// <summary>
        /// Одномерное дерево аллюров одной фазы: покой → шаг → бег → спринт.
        ///
        /// Пороги ставим по скорости ГЕРОЯ, а масштаб времени клипа — по
        /// отношению нашей скорости к его собственной. Скорость клипа
        /// снимаем с версии С корневым движением: она и говорит, под какую
        /// скорость шаг нарисован. Играем при этом версии `_InPlace` —
        /// героя везёт капсула, и клип, который едет сам, уехал бы от неё.
        /// </summary>
        private static BlendTree BuildStride(AnimatorController controller, string name, string root,
                                             string idlePath, string walkPath, string runPath, string sprintPath,
                                             string walkRef, string runRef, string sprintRef)
        {
            var idle = Clip(root + idlePath);
            var walk = Clip(root + walkPath);
            var run = Clip(root + runPath);
            var sprint = Clip(root + sprintPath);

            if (idle == null || walk == null || run == null)
            {
                Debug.LogWarning($"[IsoRPG] «{name}»: клипы не нашлись в {root} — фаза пропущена.");
                return null;
            }

            float walkAt = Speed(root + walkRef, 1.8f);
            float runAt = Speed(root + runRef, 4.0f);
            float sprintAt = Speed(root + sprintRef, 6.5f);

            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            float runScale = runAt > 0.1f ? HeroSpeed / runAt : 1f;
            float sprintTarget = HeroSpeed * SprintFactor;
            float sprintScale = sprintAt > 0.1f ? sprintTarget / sprintAt : 1f;

            var children = new System.Collections.Generic.List<ChildMotion>
            {
                new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = walk, threshold = walkAt, timeScale = 1f },
                new ChildMotion { motion = run, threshold = HeroSpeed, timeScale = runScale },
            };

            // Отдельной ступени спринта больше нет.
            //
            // Решение Павла 04.09.2026: «меняем спринт на обычный бег с
            // ускорением». Причина в замере: любой чужой клип спринта
            // приходилось гнать вдвое — вооружённый бег DoubleL шёл x2.00,
            // родной Synty x1.44, и оба читались как перемотка.
            //
            // Теперь выше беговой скорости дерево держит чистый бег, а
            // ускоряет его множитель скорости состояния (параметр MoveRate).
            // Это честнее: ноги успевают ровно настолько, насколько герой
            // реально быстрее. Стрекот при этом никуда не девается —
            // убрать его можно только уменьшив прибавку спринта.
            _ = sprintTarget;
            _ = sprintScale;
            _ = sprint;

            tree.children = children.ToArray();

            Debug.Log($"[IsoRPG] «{name}»: шаг {walkAt:0.00}, бег {runAt:0.00}, спринт {sprintAt:0.00} м/с; " +
                      $"клип бега x{runScale:0.00}, спринта x{sprintScale:0.00}.");

            return tree;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Замок челюсти герою.
        ///
        /// Клипы нового набора двигают кость , и герой пошёл с открытым
        /// ртом — ровно как до этого НПС. Компонент общий, вешаем той же
        /// рукой: правило одно на всех, кто получил чужие анимации.
        /// </summary>
        private static void LockJaw()
        {
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            int locked = 0;

            foreach (var router in Object.FindObjectsByType<IsoRPG.Player.PlayerInputRouter>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (router.GetComponent<IsoRPG.World.JawLock>() != null) continue;

                router.gameObject.AddComponent<IsoRPG.World.JawLock>();
                locked++;
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[IsoRPG] Замок челюсти повешен героям: " + locked + ".");
        }

        /// <summary>
        /// Зациклить ход и зависание в воздухе.
        ///
        /// Клипы приезжают из набора незацикленными — та же ловушка, что была
        /// со стойками НПС: шаг отыгрывает один раз и замирает.
        /// </summary>
        private static void LoopMovement()
        {
            (string Path, bool Loop)[] files =
            {
                (Move + "/Walk/Type A/Base/InPlace/OneHand_Base_Walk_A_F_InPlace.fbx", true),
                (Move + "/Run/Type A/Base/InPlace/OneHand_Base_Run_A_F_InPlace.fbx", true),
                (Move + "/Sprint/Type A/Base/InPlace/OneHand_Base_Sprint_A_F_InPlace.fbx", true),
                (Move + "/Idle/Idle/OneHand_Base_Stand_Idle_A_1.fbx", true),
                (Peace + "/Stand_Idle/Idle/Stand_Idle_A_1.fbx", true),

                // Synty: то, что реально играет в дереве хода. Незацикленный
                // клип бега доигрывает до конца и замирает — со стороны это
                // выглядит как «герой поехал по земле стоя».
                (Synty + "/Idles/A_MOD_BL_Idle_Standing_Masc.fbx", true),
                (Synty + "/Locomotion/Walk/A_MOD_BL_Walk_F_Masc.fbx", true),
                (Synty + "/Locomotion/Run/A_MOD_BL_Run_F_Masc.fbx", true),
                (Synty + "/Locomotion/Sprint/A_MOD_BL_Sprint_F_Masc.fbx", true),
                (Jump + "/InPlace/OneHand_Base_Jump_End_2_InPlace.fbx", false),
                (Jump + "/InPlace/OneHand_Base_Jump_End_3_InPlace.fbx", false),
                (Jump + "/InPlace/OneHand_Base_Jump_Air_Loop_InPlace.fbx", true),
                (Jump + "/OneHand_Base_Jump_Air_Loop.fbx", true),
            };

            foreach (var (path, loop) in files)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                var takes = importer.clipAnimations;
                if (takes == null || takes.Length == 0) takes = importer.defaultClipAnimations;
                if (takes.Length == 0) continue;

                bool changed = false;

                for (int i = 0; i < takes.Length; i++)
                {
                    if (takes[i].loopTime == loop) continue;

                    takes[i].loopTime = loop;
                    changed = true;
                }

                if (!changed) continue;

                importer.clipAnimations = takes;
                importer.SaveAndReimport();

                Debug.Log("[IsoRPG] Зациклен клип хода: " + System.IO.Path.GetFileName(path));
            }
        }

        private static float Speed(string path, float fallback)
        {
            float measured = ClipSpeed.Measure(Clip(path));

            if (measured > 0.05f) return measured;

            Debug.LogWarning("[IsoRPG] Скорость не померилась у " + path + " — порог запасной.");
            return fallback;
        }

        private static AnimationClip Clip(string path)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                                    .OfType<AnimationClip>()
                                    .FirstOrDefault(c => !c.name.StartsWith("__preview"));

            if (clip == null) Debug.LogWarning("[IsoRPG] Клип не найден: " + path);

            return clip;
        }
    }
}
