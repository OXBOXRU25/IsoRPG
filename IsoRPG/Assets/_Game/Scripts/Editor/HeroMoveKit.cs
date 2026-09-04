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

        /// <summary>Третий набор: ExplosiveLLC, анимации с оружием в обеих руках.</summary>
        private const string Boom =
            "Assets/ExplosiveLLC/RPG Character Mecanim Animation Pack/Animations";

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

        /// <summary>Сальто в полёте: 1 — крутим. Включает мотор на вершине прыжка.</summary>
        public const string FlipParameter = "Flip";

        /// <summary>Прошли верхнюю точку прыжка. По нему машина уходит в фазу полёта.</summary>
        public const string FallingParameter = "Falling";

        /// <summary>Множитель темпа хода. 1 — как собрано; меняется на лету для примерки.</summary>
        public const string RateParameter = "MoveRate";

        /// <summary>Какую позу ожидания проиграть: 0..3.</summary>
        public const string FidgetParameter = "FidgetPick";

        /// <summary>Имя слоя, поверх которого играются редкие позы ожидания.</summary>
        public const string FidgetLayer = "Оживление";

        /// <summary>
        /// Редкие позы ожидания в бою. Выбор Павла 04.09.2026: основная
        /// стойка пятая, а «если долго стоим — иногда 9, 10, 11, 12».
        /// Это отдельный раздел набора, нарисованный именно под бой.
        /// </summary>
        private static readonly string[] Fidgets =
        {
            "/Combat Idle/Combat_Idle_2.fbx",
            "/Combat Idle/Combat_Idle_3.fbx",
            "/Combat Idle/Combat_Idle_4.fbx",
            "/Combat Idle/Combat_Idle_5.fbx",
        };

        private const string Actions = "Assets/DoubleL/FBX_Animations/Actions";

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
            // Кольцо направлений — по схеме автора набора, а не своей.
            //
            // У Synty под нашу же модель нарисованы все восемь сторон хода и
            // бега, и в его контроллере `AC_Sidekick_Masculine` они уже
            // расставлены. Схема снята оттуда 04.09.2026 целиком:
            //
            //  * направление НОРМАЛИЗОВАНО — точки стоят на единичной
            //    окружности, а быстроту выбирает отдельная ось Speed. Иначе
            //    одно дерево отвечало бы сразу на два вопроса;
            //  * тип дерева — направленное (FreeformDirectional2D), и девятая
            //    точка стоит в самом центре: она и снимает метание между
            //    соседями на нулевой скорости, из-за которого я в прошлый раз
            //    взял декартово;
            //  * колец ДВА — «лицом вперёд» и «спиной», — и различаются они
            //    всего четырьмя боковыми клипами. Автор развёл их потому, что
            //    ноги перекрещиваются по-разному в зависимости от того, откуда
            //    герой пришёл в это направление;
            //  * граница между кольцами асимметрична: −55°…+125°. Числа его,
            //    и они сходятся со списком файлов — в переднем наборе есть
            //    задне-правый клип и нет задне-левого.
            //
            // Своей расстановки здесь нет ни одной: подобранная на глаз, она
            // дала бы предельный образец каждый второй раз.
            if (!controller.parameters.Any(p => p.name == MoveXParameter))
                controller.AddParameter(MoveXParameter, AnimatorControllerParameterType.Float);

            if (!controller.parameters.Any(p => p.name == MoveYParameter))
                controller.AddParameter(MoveYParameter, AnimatorControllerParameterType.Float);

            if (!controller.parameters.Any(p => p.name == FacingParameter))
                controller.AddParameter(FacingParameter, AnimatorControllerParameterType.Float);

            if (!controller.parameters.Any(p => p.name == TurnStepParameter))
                controller.AddParameter(TurnStepParameter, AnimatorControllerParameterType.Float);

            var idlePeace = Clip(Synty + "/Idles/A_MOD_BL_Idle_Standing_Masc.fbx");
            var idleFight = Clip(Move + "/Idle/Idle/OneHand_Base_Stand_Idle_B_1.fbx");

            // Кольца сторон — из ExplosiveLLC, по фазе.
            //
            // Выбор Павла 04.09.2026: он посмотрел клипы в консоли и сказал
            // «все хорошо выглядят, ставь». Родное кольцо Synty при этом
            // остаётся в коде ниже (`BuildFacingPair`) — оно точнее по
            // пропорциям, но руки в нём пустые, а у героя два кинжала.
            //
            // Замер, из-за которого поехала боковая скорость: эти клипы
            // нарисованы под 1.9–2.3 м/с, то есть МЕДЛЕННЕЕ кольца Synty
            // (2.74). На прежних 3.85 они шли бы с растяжкой x1.83 — почти
            // перемотка. Поэтому боковая доля опущена до 0.55.
            var fightRing = BuildArmedRing(controller, "Стороны боевые", "Armed", idleFight);
            var peaceRing = BuildArmedRing(controller, "Стороны мирные", "Unarmed", idlePeace ?? idleFight);

            var fight = BuildStride(controller, "Ход боевой", "", null, fightRing,
                Move + "/Idle/Idle/OneHand_Base_Stand_Idle_B_1.fbx",
                Synty + "/Locomotion/Walk/A_MOD_BL_Walk_F_Masc.fbx",
                Move + "/Sprint/Type A/Base/InPlace/OneHand_Base_Sprint_A_F_InPlace.fbx",
                Synty + "/Locomotion/Sprint/A_MOD_BL_Sprint_F_Masc.fbx",
                Synty + "/Locomotion/Walk/A_MOD_BL_Walk_F_RM_Masc.fbx",
                Move + "/Sprint/Type A/Base/OneHand_Base_Sprint_A_F.fbx",
                Synty + "/Locomotion/Sprint/A_MOD_BL_Sprint_F_RM_Masc.fbx");

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
            var peace = BuildStride(controller, "Ход мирный", "", null, peaceRing,
                Synty + "/Idles/A_MOD_BL_Idle_Standing_Masc.fbx",
                Synty + "/Locomotion/Walk/A_MOD_BL_Walk_F_Masc.fbx",
                Move + "/Sprint/Type A/Base/InPlace/OneHand_Base_Sprint_A_F_InPlace.fbx",
                Synty + "/Locomotion/Sprint/A_MOD_BL_Sprint_F_Masc.fbx",
                Synty + "/Locomotion/Walk/A_MOD_BL_Walk_F_RM_Masc.fbx",
                Move + "/Sprint/Type A/Base/OneHand_Base_Sprint_A_F.fbx",
                Synty + "/Locomotion/Sprint/A_MOD_BL_Sprint_F_RM_Masc.fbx");

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
            // Прыжок: Synty с разбега. Выбор Павла 04.09.2026 — «руки
            // поднимает, выглядит анатомично».
            var jumpStart = Clip(Synty + "/InAir/A_MOD_BL_Jump_Running_Masc.fbx")
                            ?? Clip(Jump + "/InPlace/OneHand_Base_Jump_Start_InPlace.fbx");

            // Верхняя точка прыжка — сальто.
            //
            // Идея Павла 04.09.2026: «в прыжке делать флип в верхней точке до
            // приземления», и клип он назвал сам — `Armed-Jump-Flip`, 0.47 с.
            //
            // Ставим его вместо зависания в воздухе: фаза полёта у нас как раз
            // и есть «верхняя точка», между отталкиванием и касанием. Клип
            // короче обычного зависания, поэтому зацикливаем — на долгом
            // падении сальто повторится, а на обычном прыжке пройдёт один раз.
            var hangClip = Clip(Jump + "/InPlace/OneHand_Base_Jump_Air_Loop_InPlace.fbx")
                           ?? Clip(Jump + "/OneHand_Base_Jump_Air_Loop.fbx");

            var flipClip = Clip(Boom + "/Armed/RPG-Character@Armed-Jump-Flip.FBX");

            Motion jumpAir = hangClip;

            if (hangClip != null && flipClip != null)
            {
                if (!controller.parameters.Any(p => p.name == FlipParameter))
                    controller.AddParameter(FlipParameter, AnimatorControllerParameterType.Float);

                // Воздушная фаза — двумя видами: обычное зависание и сальто.
                //
                // Павлон 04.09.2026, двумя уточнениями: «флип оставляем только
                // прыжку с высоты, простому прыжку убираем» и «он должен
                // начинаться ровно в верхней точке, а начинается при движении
                // уже вниз».
                //
                // И то и другое решает мотор: он ловит миг, когда скорость
                // подъёма сменила знак, и там же одним лучом смотрит, есть ли
                // под ногами высота. Дерево только выбирает по его ответу —
                // само оно про высоту ничего не знает и знать не должно.
                var airTree = new BlendTree
                {
                    name = "Полёт",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = FlipParameter,
                    useAutomaticThresholds = false,
                };

                AssetDatabase.AddObjectToAsset(airTree, controller);

                airTree.children = new[]
                {
                    new ChildMotion { motion = hangClip, threshold = 0f, timeScale = 1f },
                    new ChildMotion { motion = flipClip, threshold = 1f, timeScale = 1f },
                };

                jumpAir = airTree;

                Debug.Log("[IsoRPG] Полёт: зависание и сальто, переключает «Flip».");
            }

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

                            // Темп хода — множителем состояния по параметру.
                            //
                            // Нужен, чтобы менять быстроту ног НА ЛЕТУ, не
                            // пересобирая дерево: Павлон 05.09.2026 попросил
                            // повесить варианты бега на клавишу и сравнить их
                            // глазами. Число, которое нельзя вывести, ставится
                            // в ряд вариантами — а тут его и правда не вывести:
                            // наши 5.5 м/с не совпадают ни с одним аллюром
                            // автора (бег 2.74, спринт 7.69).
                            if (!controller.parameters.Any(p => p.name == RateParameter))
                                controller.AddParameter(RateParameter, AnimatorControllerParameterType.Float);

                            // Значение по умолчанию — ЕДИНИЦА, и записать его
                            // надо через присвоение всего массива.
                            //
                            // Моя поломка 05.09.2026: `controller.parameters`
                            // отдаёт КОПИЮ, и правка элемента в цикле никуда не
                            // сохраняется. Параметр остался нулём, а он множит
                            // скорость воспроизведения хода — ноги встали, и
                            // Павлон увидел «персонаж ездит на одной ноге».
                            //
                            // Родня давней ловушке с сериализованным полем:
                            // выглядит как присваивание, а на деле пишется в
                            // копию, которую никто не читает.
                            var all = controller.parameters;

                            for (int p = 0; p < all.Length; p++)
                                if (all[p].name == RateParameter) all[p].defaultFloat = 1f;

                            controller.parameters = all;

                            state.speedParameterActive = true;
                            state.speedParameter = RateParameter;
                            break;

                        case "Jump_Start":
                            if (jumpStart != null) { state.motion = jumpStart; jumped++; }

                            // Уход в полёт — по ВЕРШИНЕ, а не по времени клипа.
                            //
                            // Павлон 04.09.2026 дважды: «он должен начинаться
                            // ровно в верхней точке» и «ты так и не поймал, он
                            // уже когда герой приземляется». Причина была не в
                            // моём параметре, а здесь: переход стоял по
                            // `ExitTime 0.85`, то есть после 85% клипа
                            // отталкивания. Клип Synty длинный, и фаза полёта
                            // начиналась, когда герой уже падал.
                            //
                            // Теперь машину переключает мотор: он один знает,
                            // в каком кадре скорость подъёма сменила знак.
                            if (!controller.parameters.Any(p => p.name == FallingParameter))
                                controller.AddParameter(FallingParameter, AnimatorControllerParameterType.Bool);

                            foreach (var link in state.transitions)
                            {
                                if (link == null) continue;

                                link.hasExitTime = false;

                                // Переход плавный, а не мгновенный.
                                //
                                // Павлон 05.09.2026: «в процессе прыжка он
                                // поднимает руки вверх, а потом рывком
                                // перемещает вниз». Это моя правка: я поставил
                                // 0.05 с, и клип отталкивания обрывался прямо
                                // на поднятых руках — вершина наступает раньше,
                                // чем он доигрывает.
                                //
                                // Четверть секунды хватает, чтобы руки успели
                                // опуститься смешиванием, и при этом сальто
                                // по-прежнему начинается с вершины.
                                link.duration = 0.25f;
                                link.conditions = new[]
                                {
                                    new AnimatorCondition
                                    {
                                        mode = AnimatorConditionMode.If,
                                        parameter = FallingParameter,
                                        threshold = 0f,
                                    },
                                };
                            }

                            Debug.Log("[IsoRPG] Прыжок: полёт начинается по вершине, а не по времени клипа.");
                            break;

                        case "Jump_Air":
                            if (jumpAir != null) { state.motion = jumpAir; jumped++; }
                            break;

                        case "Jump_Land":
                            if (jumpLand != null)
                            {
                                state.motion = jumpLand;

                                // Начинаем клип не с нуля.
                                //
                                // Первые кадры приземления — ещё полёт: ноги
                                // прямые, контакта нет. Проигранные после
                                // касания, они читаются как «встал на прямые
                                // и только потом присел». Павлон 04.09.2026:
                                // «прям совсем немного сместить тайминг».
                                // Пропускаем эту долю — присед начинается
                                // ровно в момент удара о землю.
                                state.cycleOffset = 0.12f;

                                jumped++;
                            }
                            break;
                    }
                }
            }

            BuildFidgetLayer(controller);

            // Слой маха правой рукой СНЯТ.
            //
            // Павлон 04.09.2026 сразу после сборки: «ты сломал бег, верни как
            // было». Синхронный слой брал руку из безоружного спринта — а
            // корпус в выбранном клипе развёрнут иначе, и рука пошла не по
            // своей дуге. Замысел был верный, исполнение — нет: маска на всю
            // руку переносит и плечо, а плечо принадлежит корпусу.
            //
            // Если возвращаться, то не этим путём, а аддитивом: разницей
            // между двумя клипами, а не подменой целиком.
            DropArmLayer(controller);
            DropTryout();

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            LockJaw();

            Debug.Log($"[IsoRPG] Ход героя переведён: деревьев {moved}, фаз прыжка {jumped} из 3. " +
                      $"Фазы: боевая есть, мирная {(peace != null ? "есть" : "НЕ НАЙДЕНА")}; " +
                      $"переключает параметр «{CombatParameter}».");
        }

        /// <summary>
        /// Правая рука машет при беге — отдельным СИНХРОННЫМ слоем.
        ///
        /// Павлон 04.09.2026 про выбранный бег: «плохо только что он машет
        /// только левой рукой, а правой вообще не двигает». Так и нарисовано:
        /// клип вооружённый, правая держит оружие у бедра и не участвует в
        /// беге вовсе. Доворотом или подменой всего клипа это не лечится —
        /// нужен мах именно правой, поверх остального.
        ///
        /// Слой синхронный (`syncedLayerIndex = 0`): у него та же машина
        /// состояний и то же время, но свой клип и своя маска. Значит мах
        /// пойдёт РОВНО в такт шагам — а несинхронный слой разошёлся бы с
        /// ногами за пару секунд, и это читалось бы куда хуже неподвижной
        /// руки.
        ///
        /// Маска — только плечо и предплечье, без пальцев: кисть ведёт свой
        /// слой, он держит рукоять, и отнимать её у него нельзя.
        /// </summary>
        /// <summary>
        /// Снять с героя примерку анимаций.
        ///
        /// Инструмент выбора, а не механика: чтобы показывать клипы честно,
        /// он гонит скорость всего аниматора под выбранный вариант — и,
        /// оставшись в сцене после выбора, продолжает её крутить. Павлон
        /// 04.09.2026: «что-то не то со скоростью простого бега». Так и
        /// было: скорость держала примерка, а не дерево.
        ///
        /// Ровно та причина, по которой мы убрали переключатель проекций:
        /// пока варианты живы, каждая правка делается вслепую.
        /// </summary>
        private static void DropTryout()
        {
            var player = GameObject.Find("Player");
            if (player == null) return;

            var tryout = player.GetComponent<IsoRPG.Player.AnimTryout>();
            if (tryout == null) return;

            Object.DestroyImmediate(tryout, true);
            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Примерка анимаций снята с героя: выбор сделан.");
        }

        private static void DropArmLayer(AnimatorController controller)
        {
            for (int i = controller.layers.Length - 1; i > 0; i--)
                if (controller.layers[i].name == ArmLayer) controller.RemoveLayer(i);
        }

        private static void BuildArmLayer(AnimatorController controller)
        {
            var swing = Clip(Peace + "/Sprint/Base/InPlace/Sprint_F_InPlace.fbx");

            if (swing == null)
            {
                Debug.LogWarning("[IsoRPG] Нет клипа для маха правой рукой — слой не собран.");
                return;
            }

            for (int i = controller.layers.Length - 1; i > 0; i--)
                if (controller.layers[i].name == ArmLayer) controller.RemoveLayer(i);

            var mask = new AvatarMask();

            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, false);

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);

            AssetDatabase.CreateAsset(mask, MaskPath);

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = ArmLayer,
                syncedLayerIndex = 0,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f,
            });

            // Клип синхронного слоя задаётся не состоянию, а слою: у него своя
            // подмена движения на каждое состояние базового.
            var layers = controller.layers;
            var last = layers[layers.Length - 1];

            foreach (var child in layers[0].stateMachine.states)
            {
                if (child.state.name != "Locomotion") continue;

                last.SetOverrideMotion(child.state, swing);
                break;
            }

            controller.layers = layers;

            Debug.Log($"[IsoRPG] Слой «{ArmLayer}»: мах правой рукой из «{swing.name}», " +
                      "синхронно с базовым слоем, маска только на руку без пальцев.");
        }

        /// <summary>Имя слоя маха правой рукой.</summary>
        private const string ArmLayer = "Правая рука";

        private const string MaskPath = "Assets/_Game/Art/Animations/Masks/RightArm.mask";

        /// <summary>
        /// Слой редких поз ожидания — поверх всего остального.
        ///
        /// Отдельным СЛОЕМ, а не состоянием в машине: так основная стойка
        /// остаётся на месте и никуда не переключается, а поза ожидания
        /// накладывается поверх неё весом. Машину состояний не трогаем —
        /// значит и ломаться в ней нечему.
        ///
        /// Внутри дерево по параметру выбора: четыре позы, порог у каждой
        /// свой. Выбирать состояниями с переходами было бы втрое больше
        /// работы ради того же результата.
        /// </summary>
        private static void BuildFidgetLayer(AnimatorController controller)
        {
            var clips = Fidgets.Select(p => Clip(Actions + p)).Where(c => c != null).ToArray();

            if (clips.Length == 0)
            {
                Debug.LogWarning("[IsoRPG] Позы ожидания не нашлись — слой не собран.");
                return;
            }

            if (!controller.parameters.Any(p => p.name == FidgetParameter))
                controller.AddParameter(FidgetParameter, AnimatorControllerParameterType.Float);

            for (int i = controller.layers.Length - 1; i > 0; i--)
                if (controller.layers[i].name == FidgetLayer) controller.RemoveLayer(i);

            var machine = new AnimatorStateMachine
            {
                name = FidgetLayer,
                hideFlags = HideFlags.HideInHierarchy,
            };

            AssetDatabase.AddObjectToAsset(machine, controller);

            var tree = new BlendTree
            {
                name = "Позы ожидания",
                blendType = BlendTreeType.Simple1D,
                blendParameter = FidgetParameter,
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.children = clips
                .Select((c, i) => new ChildMotion { motion = c, threshold = i, timeScale = 1f })
                .ToArray();

            var state = machine.AddState("Fidget");
            state.motion = tree;
            machine.defaultState = state;

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = FidgetLayer,
                stateMachine = machine,
                blendingMode = AnimatorLayerBlendingMode.Override,

                // Ноль: поднимает вес игровой код, когда герой долго стоит.
                defaultWeight = 0f,
            });

            // Пальцы — поверх всего, что мы только что добавили.
            KeepFistLast(controller);

            Debug.Log("[IsoRPG] Слой оживления: поз " + clips.Length + " — " +
                      string.Join(", ", clips.Select(c => c.name)) + ".");
        }

        /// <summary>
        /// Стойка, которая умеет переступать при повороте на месте.
        ///
        /// Задача Павла 04.09.2026: A и D поворачивают вид, и герой обязан
        /// переставлять ноги, а не ехать вокруг оси. У Synty под это есть
        /// `Turn_Standing_90L/R` — родные клипы под нашу модель.
        ///
        /// Ставим их не отдельным состоянием, как у автора, а прямо в точку
        /// покоя дерева хода: тогда переход стойка↔переступание достаётся
        /// смешиванием даром, а машину состояний трогать не приходится —
        /// значит и ломаться в ней нечему.
        ///
        /// Клип рассчитан на один поворот на 90°, а игрок держит клавишу
        /// сколько хочет. Поэтому зацикливаем его (это делает `LoopMovement`)
        /// и подгоняем темп: своя скорость клипа — 90° за его длину, наша —
        /// столько, сколько крутит камера.
        /// </summary>
        private static Motion BuildTurnInPlace(AnimatorController controller, string owner, AnimationClip idle)
        {
            // У автора ДВА темпа поворота, и это его распределение, а не
            // пробел: доворот на 90° идёт 93 град/с, разворот на 180° — 142.
            // Берём тот, чья скорость ближе к нашей, — тогда клип играет как
            // нарисован. Выбор автоматический, чтобы правка скорости поворота
            // не требовала помнить про второе место.
            var slowLeft = Clip(Synty + "/Locomotion/Turn/A_MOD_BL_Turn_Standing_90L_Masc.fbx");
            var slowRight = Clip(Synty + "/Locomotion/Turn/A_MOD_BL_Turn_Standing_90R_Masc.fbx");
            var fastLeft = Clip(Synty + "/Locomotion/Turn/A_MOD_BL_Turn_Standing_180L_Masc.fbx");
            var fastRight = Clip(Synty + "/Locomotion/Turn/A_MOD_BL_Turn_Standing_180R_Masc.fbx");

            float slowRate = slowLeft != null && slowLeft.length > 0.05f ? 90f / slowLeft.length : 0f;
            float fastRate = fastLeft != null && fastLeft.length > 0.05f ? 180f / fastLeft.length : 0f;

            AnimationClip left, right;
            float ownRate;

            bool takeFast = fastRate > 1f &&
                            (slowRate < 1f ||
                             Mathf.Abs(fastRate - TurnDegreesPerSecond) <=
                             Mathf.Abs(slowRate - TurnDegreesPerSecond));

            if (takeFast) { left = fastLeft; right = fastRight; ownRate = fastRate; }
            else { left = slowLeft; right = slowRight; ownRate = slowRate; }

            if (idle == null || left == null || right == null)
            {
                Debug.LogWarning($"[IsoRPG] «{owner}»: клипов поворота на месте нет — стойка осталась неподвижной.");
                return idle;
            }

            float scale = ownRate > 1f ? TurnDegreesPerSecond / ownRate : 1f;

            var tree = new BlendTree
            {
                name = owner + ": стойка и поворот",
                blendType = BlendTreeType.Simple1D,
                blendParameter = TurnStepParameter,
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.children = new[]
            {
                new ChildMotion { motion = left, threshold = -1f, timeScale = scale },
                new ChildMotion { motion = idle, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = right, threshold = 1f, timeScale = scale },
            };

            Debug.Log($"[IsoRPG] «{owner}»: переступание при повороте — клип на {ownRate:0} град/с, " +
                      $"нам нужно {TurnDegreesPerSecond:0}, темп x{scale:0.00}.");

            return tree;
        }

        /// <summary>
        /// Пара колец направлений одного аллюра: «лицом вперёд» и «спиной».
        ///
        /// Между ними переключает параметр <see cref="FacingParameter"/>, и
        /// это ровно схема автора набора. Кольца различаются всего четырьмя
        /// боковыми клипами, но разница видна: ноги перекрещиваются иначе,
        /// когда герой пришёл в это направление с заднего хода.
        /// </summary>
        private static BlendTree BuildFacingPair(AnimatorController controller, string name,
                                                 string gait, AnimationClip idle)
        {
            var forward = BuildRing(controller, name + " (лицом)", gait, idle, backwards: false);
            var backward = BuildRing(controller, name + " (спиной)", gait, idle, backwards: true);

            if (forward == null) return null;

            if (backward == null) return forward;

            var pair = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = FacingParameter,
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(pair, controller);

            pair.children = new[]
            {
                new ChildMotion { motion = backward, threshold = 0f, timeScale = 1f },
                new ChildMotion { motion = forward, threshold = 1f, timeScale = 1f },
            };

            return pair;
        }

        /// <summary>
        /// Кольцо восьми направлений плюс покой в центре.
        ///
        /// Расстановка снята с авторского контроллера
        /// `AC_Sidekick_Masculine` 04.09.2026, до единой точки. Свои числа
        /// здесь не подбираются вовсе — у автора эта же модель и эти же
        /// клипы, и он уже выбрал, что куда ставить.
        ///
        /// Одна деталь, которую на глаз не угадать: в кольце «лицом вперёд»
        /// задние точки берутся из НАБОРА ЗАДНЕГО ХОДА, а задне-правая — из
        /// переднего. Отсюда и асимметричная граница −55°…+125°: сектор
        /// «лицом» сдвинут вправо ровно настолько, насколько хватает
        /// нарисованных клипов.
        /// </summary>
        /// <summary>
        /// Расстановка восьми сторон, снятая с авторского контроллера.
        ///
        /// Единственное место, где эти клипы перечислены: дерево строит по
        /// нему точки, а зацикливание по нему же берёт файлы. Пока список жил
        /// в двух местах, любой новый клип надо было чинить дважды — и второй
        /// раз всегда забывался.
        /// </summary>
        private static (Vector2 position, string side)[] RingPlan(bool backwards) => new[]
        {
            (new Vector2(0f, 1f),       "FwdStrafeF"),
            (new Vector2(0.7f, 0.7f),   "FwdStrafeFR"),
            (new Vector2(1f, 0f),       backwards ? "BckStrafeR" : "FwdStrafeR"),
            (new Vector2(0.7f, -0.7f),  backwards ? "BckStrafeBR" : "FwdStrafeBR"),
            (new Vector2(0f, -1f),      "BckStrafeB"),
            (new Vector2(-0.7f, -0.7f), "BckStrafeBL"),
            (new Vector2(-1f, 0f),      backwards ? "BckStrafeL" : "FwdStrafeL"),
            (new Vector2(-0.7f, 0.7f),  backwards ? "BckStrafeFL" : "FwdStrafeFL"),
        };

        /// <summary>Где лежит клип одной стороны кольца. Играем версию без корневого движения.</summary>
        private static string RingClipPath(string gait, string side) =>
            $"{Synty}/Locomotion/{gait}/A_MOD_BL_{gait}_{side}_Masc.fbx";

        /// <summary>Все клипы, которые кольцо реально играет: обе стороны, оба аллюра.</summary>
        internal static System.Collections.Generic.IEnumerable<string> RingClipPaths()
        {
            foreach (var gait in new[] { "Walk", "Run" })
                foreach (var backwards in new[] { false, true })
                    foreach (var point in RingPlan(backwards))
                        yield return RingClipPath(gait, point.side);
        }

        /// <summary>
        /// Имена клипов, которые СТОЯТ В ИГРЕ на сторонах и поворотах.
        ///
        /// Просьба Павла 04.09.2026: во вкладке «Стороны» он получил 64 клипа
        /// вперемешку — приседания, мелкие переступания, всё подряд, — и не
        /// понял, какие смотреть. Здесь ровно то, что играет: кольцо
        /// направлений и переступание при повороте.
        ///
        /// Список тот же, по которому строится дерево, — не вторая его копия.
        /// Копия разошлась бы на первой правке, и он смотрел бы клипы, которых
        /// в игре уже нет.
        /// </summary>
        internal static System.Collections.Generic.IEnumerable<string> UsedSideClipNames()
        {
            var seen = new System.Collections.Generic.HashSet<string>();

            // Кольцо, которое стоит в дереве СЕЙЧАС, — вооружённое и
            // безоружное из ExplosiveLLC. Родное кольцо Synty здесь больше не
            // перечисляется: список обязан показывать то, что играет, иначе
            // Павлон смотрел бы клипы, которых в игре нет.
            foreach (var path in ArmedRingPaths())
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (seen.Add(name)) yield return name;
            }

            // Повороты: какой из двух возьмёт дерево, решает скорость. Отдаём
            // оба — Павлон смотрит и медленный, и быстрый.
            foreach (var turn in new[] { "90L", "90R", "180L", "180R" })
            {
                string name = $"A_MOD_BL_Turn_Standing_{turn}_Masc";
                if (seen.Add(name)) yield return name;
            }
        }

        /// <summary>
        /// Кольцо восьми сторон из набора ExplosiveLLC — с оружием в руках.
        ///
        /// Павлон 04.09.2026 посмотрел клипы в консоли и сказал «все хорошо
        /// выглядят, ставь». Причина, по которой они лучше родного кольца
        /// Synty, простая: у героя два кинжала, а Synty рисовал стороны для
        /// пустых рук — там кисти висят свободно.
        ///
        /// Набор даёт ДВА кольца, вооружённое и безоружное, и это ровно наши
        /// две фазы: боевая берёт `Armed`, мирная `Unarmed`. Внутри каждого
        /// дерева один набор — правило соблюдено.
        ///
        /// Расстановка та же авторская, что снята с Synty: восемь точек на
        /// единичной окружности плюс покой в центре. Схема не зависит от
        /// набора, она про то, как устроено направленное дерево.
        /// </summary>
        private static BlendTree BuildArmedRing(AnimatorController controller, string name,
                                                string kind, AnimationClip idle)
        {
            // Клипы отобраны Павлоном через консоль 04.09.2026, поимённо.
            //
            // Он перебрал набор глазами и назвал четыре: вперёд и вбок —
            // Synty `Run_FwdStrafe*`, назад — `Relax-Walk-Backward` из
            // ExplosiveLLC. Диагоналей он не называл, поэтому их нет: дерево
            // само смешает соседей, а придуманная мной восьмёрка была бы
            // подбором вместо его выбора.
            //
            // Наборы внутри кольца смешаны — вперёд и вбок Synty, назад
            // ExplosiveLLC. Это против правила «один набор в дереве», но это
            // его выбор глазами, а вердикт по виду весит больше правила.
            var plan = new (Vector2 position, string path)[]
            {
                (new Vector2(0f, 1f),  Synty + "/Locomotion/Run/A_MOD_BL_Run_FwdStrafeF_Masc.fbx"),
                (new Vector2(1f, 0f),  Synty + "/Locomotion/Run/A_MOD_BL_Run_FwdStrafeR_Masc.fbx"),
                (new Vector2(-1f, 0f), Synty + "/Locomotion/Run/A_MOD_BL_Run_FwdStrafeL_Masc.fbx"),
                (new Vector2(0f, -1f), Boom + "/Relax/RPG-Character@Relax-Walk-Backward.FBX"),
            };

            var children = new System.Collections.Generic.List<ChildMotion>();

            foreach (var point in plan)
            {
                var clip = Clip(point.path);

                if (clip == null) continue;

                children.Add(new ChildMotion { motion = clip, position = point.position, timeScale = 1f });
            }

            if (children.Count < 4)
            {
                Debug.LogWarning($"[IsoRPG] «{name}»: сторон нашлось {children.Count} из 4 — кольцо пропущено.");
                return null;
            }

            if (idle != null)
                children.Add(new ChildMotion { motion = idle, position = Vector2.zero, timeScale = 1f });

            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = MoveXParameter,
                blendParameterY = MoveYParameter,
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.children = children.ToArray();

            Debug.Log($"[IsoRPG] «{name}»: кольцо {kind}, точек {children.Count}.");

            return tree;
        }

        /// <summary>
        /// Кольцо спринта: вперёд свой клип, стороны — беговые.
        ///
        /// Набор Synty боковой спринт не рисовал, и это не пробел, а ответ
        /// автора: боком с такой скоростью не бегают. Но играть при боковом
        /// ходе клип «бег вперёд» ещё хуже — герой едет вбок лицом вперёд.
        /// Поэтому стороны берём беговые: они хотя бы про боковое движение.
        /// </summary>
        private static BlendTree BuildSprintRing(AnimatorController controller, string name,
                                                 Motion forward, AnimationClip idle, float sprintScale)
        {
            // Стороны гоним НЕ до полного совпадения с землёй.
            //
            // Предложение Павла 05.09.2026: «для спринта в стороны использовать
            // анимацию обычного бега в стороны, только ускоренную». Так и
            // делаем, но с оговоркой по числам: беговой клип нарисован под
            // 2.74 м/с, а спринт несёт 9.35 — совпадение требует x3.4, вдвое
            // больше того x2.00, который он принял для бега глазами.
            //
            // Поэтому целимся в тот же x2.00: боковой спринт по частоте ног
            // выглядит как привычный бег, а не как перемотка. Ноги при этом
            // немного отстают от земли — сознательный размен, тот же, что мы
            // уже приняли на беге.
            float sideRate = sprintScale > 0.1f ? 2f / sprintScale : 1f;

            var plan = new (Vector2 position, string path)[]
            {
                (new Vector2(1f, 0f),  Synty + "/Locomotion/Run/A_MOD_BL_Run_FwdStrafeR_Masc.fbx"),
                (new Vector2(-1f, 0f), Synty + "/Locomotion/Run/A_MOD_BL_Run_FwdStrafeL_Masc.fbx"),
                (new Vector2(0f, -1f), Boom + "/Relax/RPG-Character@Relax-Walk-Backward.FBX"),
            };

            var children = new System.Collections.Generic.List<ChildMotion>
            {
                new ChildMotion { motion = forward, position = new Vector2(0f, 1f), timeScale = 1f },
            };

            foreach (var point in plan)
            {
                var clip = Clip(point.path);

                if (clip == null) continue;

                children.Add(new ChildMotion { motion = clip, position = point.position, timeScale = sideRate });
            }

            if (children.Count < 3)
            {
                Debug.LogWarning($"[IsoRPG] «{name}»: сторон мало — спринт остаётся одним клипом.");
                return null;
            }

            if (idle != null)
                children.Add(new ChildMotion { motion = idle, position = Vector2.zero, timeScale = 1f });

            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = MoveXParameter,
                blendParameterY = MoveYParameter,
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.children = children.ToArray();

            Debug.Log($"[IsoRPG] «{name}»: кольцо спринта, точек {children.Count}.");

            return tree;
        }

        /// <summary>Где лежит одна сторона вооружённого кольца.</summary>
        private static string ArmedRingPath(string kind, string side) =>
            $"{Boom}/{kind}/RPG-Character@{kind}-Strafe-{side}.FBX";

        /// <summary>Все восемь сторон обоих колец — для зацикливания.</summary>
        internal static System.Collections.Generic.IEnumerable<string> ArmedRingPaths()
        {
            foreach (var kind in new[] { "Armed", "Unarmed" })
                foreach (var side in new[] { "Forward", "Forward-Right", "Right", "Backward-Right",
                                             "Backward", "Backward-Left", "Left", "Forward-Left" })
                    yield return ArmedRingPath(kind, side);
        }

        private static BlendTree BuildRing(AnimatorController controller, string name,
                                           string gait, AnimationClip idle, bool backwards)
        {
            var children = new System.Collections.Generic.List<ChildMotion>();

            foreach (var point in RingPlan(backwards))
            {
                var clip = Clip(RingClipPath(gait, point.side));

                if (clip == null) continue;

                children.Add(new ChildMotion
                {
                    motion = clip,
                    position = point.position,
                    timeScale = 1f,
                });
            }

            if (children.Count < 4)
            {
                Debug.LogWarning($"[IsoRPG] «{name}»: клипов направлений нашлось {children.Count} — кольцо пропущено.");
                return null;
            }

            // Девятая точка — покой в самом центре. Она и снимает метание
            // между соседями на нулевой скорости: у направленного дерева угол
            // в нуле не определён, и без центра оно дёргается.
            if (idle != null)
                children.Add(new ChildMotion { motion = idle, position = Vector2.zero, timeScale = 1f });

            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = MoveXParameter,
                blendParameterY = MoveYParameter,
                useAutomaticThresholds = false,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.children = children.ToArray();

            Debug.Log($"[IsoRPG] «{name}»: кольцо направлений, точек {children.Count} " +
                      $"(схема автора {gait}).");

            return tree;
        }

        /// <summary>
        /// Направление хода в своих координатах героя: X вправо, Y вперёд.
        ///
        /// НОРМАЛИЗОВАНО — точка всегда на единичной окружности либо в нуле.
        /// Быстроту выбирает отдельная ось <c>Speed</c>: одно дерево не должно
        /// отвечать сразу на два вопроса, это схема автора набора.
        /// </summary>
        public const string MoveXParameter = "MoveX";

        /// <inheritdoc cref="MoveXParameter"/>
        public const string MoveYParameter = "MoveY";

        /// <summary>
        /// Каким кольцом идти: 1 — лицом вперёд, 0 — спиной.
        ///
        /// Граница у автора асимметрична, −55°…+125°, и это не описка: в
        /// переднем наборе есть задне-правый клип и нет задне-левого.
        /// </summary>
        public const string FacingParameter = "Facing";

        /// <summary>Переступание при повороте на месте: −1 влево, +1 вправо.</summary>
        public const string TurnStepParameter = "TurnStep";

        /// <summary>
        /// С какой скоростью A и D крутят вид, градусов в секунду.
        ///
        /// Одно число в двух местах: по нему подгоняется темп клипа
        /// переступания, а крутит вид ходьба на клавишах. Пусть сборщик
        /// СПРАШИВАЕТ её у игры, а не помнит свою копию.
        /// </summary>
        private static float TurnDegreesPerSecond => IsoRPG.Player.KeyboardMove.TurnInPlaceDefault;

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
                                             Motion walkRing, Motion runRing,
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

            // Шаг и боковой бег идут кольцом направлений, передний бег — нет.
            //
            // Почему не всё кольцом: замер 04.09.2026 показал, что кольцо
            // Synty нарисовано под 2.74 м/с, а герой бегает 5.5 — растяжка
            // ровно вдвое, та самая «перемотка», которую Павлон забраковал на
            // спринте. Поэтому вперёд остаётся принятый им клип DoubleL
            // (нарисован под 6.16, идёт почти как есть), а кольцо ставится на
            // боковую скорость — 0.7 от полной, то есть 3.85 м/с. Там
            // растяжка выходит x1.4 против x2.0, и ноги совпадают с землёй.
            //
            // Боковых клипов у спринта Synty нет вовсе, и это не пробел, а
            // ответ автора: боком с такой скоростью не бегают.
            // Кольцо стоит на ПОЛНОЙ беговой скорости.
            //
            // Павлон 04.09.2026 назвал клипы поимённо и для бега вперёд тоже:
            // `Run_FwdStrafeF`. Значит отдельной точки «бег вперёд другим
            // набором» больше нет — вперёд и вбок идут одним кольцом, и порог
            // у него общий.
            float ringAt = HeroSpeed;

            // Скорость кольца — среднее по четырём сторонам, а не по одной.
            // У ExplosiveLLC они расходятся заметно: вперёд 2.28, вбок 1.90.
            // Взять крайнюю значило бы промахнуться на четверть в ту или
            // другую сторону, и ноги поехали бы по земле на половине сторон.
            // Темп кольца — по клипу бега вперёд: он в нём главный, и по нему
            // игрок судит, «едет» герой или бежит.
            float ringSpeed = Speed(Synty + "/Locomotion/Run/A_MOD_BL_Run_F_RM_Masc.fbx", 2.74f);
            float ringScale = ringSpeed > 0.1f ? ringAt / ringSpeed : 1f;

            float walkRingSpeed = Speed(Synty + "/Locomotion/Walk/A_MOD_BL_Walk_F_RM_Masc.fbx", 1.54f);

            var children = new System.Collections.Generic.List<ChildMotion>
            {
                new ChildMotion { motion = BuildTurnInPlace(controller, name, idle), threshold = 0f, timeScale = 1f },
                new ChildMotion
                {
                    motion = walkRing ?? (Motion)walk,
                    threshold = walkRing != null ? walkRingSpeed : walkAt,
                    timeScale = 1f,
                },
            };

            if (runRing != null)
            {
                children.Add(new ChildMotion { motion = runRing, threshold = ringAt, timeScale = ringScale });

                Debug.Log($"[IsoRPG] «{name}»: кольцо на {ringAt:0.0} м/с, клип нарисован под " +
                          $"{ringSpeed:0.00} — темп x{ringScale:0.00}.");
            }
            else
            {
                // Кольца нет — остаётся прежний бег вперёд одним клипом.
                children.Add(new ChildMotion { motion = run, threshold = HeroSpeed, timeScale = runScale });
            }

            // Спринт — ТОТ ЖЕ клип бега, только быстрее.
            //
            // Решение Павла 04.09.2026 после двух проб: «спринт плохо, надо
            // ставить обычный бег просто ускорять». Оба чужих клипа спринта
            // приходилось гнать вдвое с лишним (DoubleL x2.00, ExplosiveLLC
            // x2.22) и оба читались как перемотка — а вдобавок ломали
            // однородность: в дереве оказывались две разные пластики.
            //
            // С тем же клипом смеси нет вовсе: на разгоне дерево переходит
            // от бега к бегу, меняется только скорость воспроизведения.
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
            if (sprint != null)
            {
                // У спринта тоже своё КОЛЬЦО, а не один клип вперёд.
                //
                // Павлон 05.09.2026: «при спринте нет анимации бега вправо и
                // влево, там та же анимация спринта, при которой персонаж
                // бежит вперёд, но перемещается в стороны».
                //
                // Боковых клипов у спринта Synty нет вовсе — есть только
                // `Sprint_F` и уклоны в горку. Поэтому кольцо собираем
                // смешанное: вперёд его спринт, вбок и назад — беговые клипы,
                // те же, что в обычном кольце. Ноги там частят сильнее
                // нарисованного, но это честнее, чем бежать вбок лицом вперёд.
                var sprintRing = BuildSprintRing(controller, name + " — спринт", sprint, idle, sprintScale);

                children.Add(new ChildMotion
                {
                    motion = sprintRing != null ? (Motion)sprintRing : sprint,
                    threshold = sprintTarget,
                    timeScale = sprintScale,
                });
            }

            tree.children = children.ToArray();

            Debug.Log($"[IsoRPG] «{name}»: шаг {walkAt:0.00}, бег {runAt:0.00}, спринт {sprintAt:0.00} м/с; " +
                      $"клип бега x{runScale:0.00}, спринта x{sprintScale:0.00}.");

            return tree;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Слой сжатой кисти должен быть ПОСЛЕДНИМ.
        ///
        /// Павлон 05.09.2026 разглядел вблизи: «когда проигрывается анимация
        /// ожидания, руки немного разжимаются, и кинжал уже висит в пальцах».
        ///
        /// Причина в порядке слоёв. Слои Unity применяются один за другим, и
        /// поздний перебивает раннего. «Кисть» стояла вторым слоем, а
        /// «Оживление» третьим — и оно БЕЗ МАСКИ, то есть накрывает всё тело,
        /// включая пальцы. Поза ожидания приносила свои раскрытые ладони и
        /// затирала хват.
        ///
        /// Двигаем кисть в конец, а не вешаем маску на оживление: маску
        /// пришлось бы заводить каждому новому слою, а правило «пальцы поверх
        /// всего» верно всегда — рукоять в руке не зависит от того, какую позу
        /// герой сейчас принимает.
        /// </summary>
        private static void KeepFistLast(AnimatorController controller)
        {
            var layers = controller.layers;

            int fist = -1;

            for (int i = 0; i < layers.Length; i++)
                if (layers[i].name == FistLayer) { fist = i; break; }

            if (fist < 0 || fist == layers.Length - 1) return;

            var moved = layers[fist];

            var rest = new System.Collections.Generic.List<AnimatorControllerLayer>(layers);
            rest.RemoveAt(fist);
            rest.Add(moved);

            // Присваиваем ВЕСЬ массив обратно: `controller.layers` отдаёт
            // копию, и правка на месте никуда не сохранится. На этом я уже
            // горел с параметрами — та же ловушка.
            controller.layers = rest.ToArray();

            Debug.Log($"[IsoRPG] Слой «{FistLayer}» переставлен последним " +
                      $"(был {fist}, стал {rest.Count - 1}) — пальцы теперь поверх поз ожидания.");
        }

        /// <summary>Имя слоя сжатой кисти. Его ставит задание `hand-pose`.</summary>
        public const string FistLayer = "Кисть";

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
                // Примерка бега — на героя, клавиша F8.
                if (router.GetComponent<IsoRPG.Player.RunTryout>() == null)
                    router.gameObject.AddComponent<IsoRPG.Player.RunTryout>();

                // Ножны — клавиша Z.
                if (router.GetComponent<IsoRPG.Combat.WeaponSheath>() == null)
                    router.gameObject.AddComponent<IsoRPG.Combat.WeaponSheath>();

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
                (Synty + "/Locomotion/Sprint/A_MOD_BL_Sprint_F_Masc.fbx", true),
                (Jump + "/InPlace/OneHand_Base_Jump_End_2_InPlace.fbx", false),
                (Jump + "/InPlace/OneHand_Base_Jump_End_3_InPlace.fbx", false),
                (Jump + "/InPlace/OneHand_Base_Jump_Air_Loop_InPlace.fbx", true),
                (Jump + "/OneHand_Base_Jump_Air_Loop.fbx", true),
            };

            // Кольцо направлений и переступание — тем же списком, что строит
            // дерево. Незацикленный боковой клип доигрывает и замирает: герой
            // едет вбок в позе стоя, ровно как было с бегом.
            var loops = new System.Collections.Generic.List<(string Path, bool Loop)>(files);

            foreach (var path in RingClipPaths())
                loops.Add((path, true));

            foreach (var path in ArmedRingPaths())
                loops.Add((path, true));

            loops.Add((Synty + "/Locomotion/Turn/A_MOD_BL_Turn_Standing_90L_Masc.fbx", true));
            loops.Add((Synty + "/Locomotion/Turn/A_MOD_BL_Turn_Standing_90R_Masc.fbx", true));

            // Клипы, отобранные Павлоном 04.09.2026 поимённо.
            loops.Add((Synty + "/Locomotion/Run/A_MOD_BL_Run_FwdStrafeF_Masc.fbx", true));
            loops.Add((Synty + "/Locomotion/Run/A_MOD_BL_Run_FwdStrafeR_Masc.fbx", true));
            loops.Add((Synty + "/Locomotion/Run/A_MOD_BL_Run_FwdStrafeL_Masc.fbx", true));
            loops.Add((Boom + "/Relax/RPG-Character@Relax-Walk-Backward.FBX", true));
            // Сальто НЕ зацикливаем.
            //
            // Павлон 04.09.2026: «если высота большая, он делает не 1 флип, а
            // 2, должен быть только 1». Клип я зациклил заодно со всей
            // пластикой — а сальто это разовое движение, и на долгом падении
            // оно честно повторялось.
            loops.Add((Boom + "/Armed/RPG-Character@Armed-Jump-Flip.FBX", false));

            foreach (var (path, loop) in loops)
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

        /// <summary>
        /// Достать корневое движение, если оно запечено в позу.
        ///
        /// У Synty клипы `_RM_` импортированы с включённым «Bake Into Pose»
        /// по горизонтали: движение в них есть, но наружу не выдаётся — ни
        /// кривыми, ни средней скоростью. Замерщик честно возвращал ноль, а
        /// мы молча подставляли запасное число и объявляли его замером.
        /// 04.09.2026 я на этом основании сказал Павлону «клип нарисован под
        /// 4 м/с» — цифра была из кода, а не из клипа.
        ///
        /// Трогаем только версии `_RM_`: играем мы `_InPlace`, поэтому на
        /// саму игру настройка не влияет — лишь на возможность померить.
        /// </summary>
        internal static void UnbakeRoot(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;

            var takes = importer.clipAnimations;
            if (takes == null || takes.Length == 0) takes = importer.defaultClipAnimations;
            if (takes.Length == 0) return;

            bool changed = false;

            for (int i = 0; i < takes.Length; i++)
            {
                if (!takes[i].lockRootPositionXZ) continue;

                takes[i].lockRootPositionXZ = false;
                changed = true;
            }

            if (!changed) return;

            importer.clipAnimations = takes;
            importer.SaveAndReimport();
        }

        private static float Speed(string path, float fallback)
        {
            float measured = ClipSpeed.Measure(Clip(path));

            if (measured > 0.05f) return measured;

            // Не померилось — скорее всего движение запечено в позу.
            // Снимаем запекание и пробуем ещё раз, прежде чем сдаваться.
            UnbakeRoot(path);
            measured = ClipSpeed.Measure(Clip(path));

            if (measured > 0.05f)
            {
                Debug.Log($"[IsoRPG] {path}: {measured:0.00} м/с — померилось после снятия запекания.");
                return measured;
            }

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
