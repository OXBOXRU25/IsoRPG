using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Собирает боевых зверей из покупных наборов: волка Meshtint и упырей
    /// BitGem.
    ///
    /// Отдельно от KayKitCharacters, хотя работа похожа. Причина не в лени, а
    /// в том, что у людей набор состояний богаче: прыжок, скрытный удар,
    /// сидение, работа инструментом. Звери из этих наборов такого не умеют и
    /// уметь не будут — у них восемь клипов и ни одного лишнего. Втащить их
    /// в общий сборщик значило бы обвесить его проверками «а есть ли у этого
    /// прыжок», и сломать заодно десять уже работающих персонажей ради двух
    /// новых видов. Здесь же контроллер простой: ходьба, удар, смерть.
    ///
    /// Имена параметров те же, что у всех: Speed, Attack, AttackSpeed, Dead —
    /// их дёргает CharacterAnimatorDriver, общий для игрока и монстров.
    /// Разойтись тут нельзя: зверь молча перестанет шевелиться.
    /// </summary>
    public static class BeastBuilder
    {
        private const string ControllersFolder = "Assets/_Game/Art/Beasts";
        private const string PrefabsFolder = "Assets/_Game/Prefabs";

        /// <summary>
        /// Пороги смешивания — это скорости, на которых зверь ЕЗДИТ, а не
        /// скорости игрока.
        ///
        /// Здесь стояли игроцкие 2 и 5.5, скопированные из сборщика людей, и
        /// это была ошибка с видимым следствием. У монстра две скорости:
        /// MonsterBrain гуляет на patrolSpeed = 1.3 и гонится на agent.speed
        /// = 3.4. При порогах 2 и 5.5 ни одна из них не попадает на чистый
        /// клип: на 1.3 играет смесь «стоит плюс шагает», на 3.4 — смесь
        /// «шагает плюс бежит» почти поровну. Ноги в обоих случаях движутся
        /// не с той частотой, с какой едет земля, и зверь скользит; а
        /// «иногда бежит нормально» — это мгновения, когда смесь случайно
        /// совпала.
        ///
        /// Совпадут пороги со скоростями — на прогулке играет чистый шаг, в
        /// погоне чистый бег, и смешивать становится нечего.
        ///
        /// Числа обязаны совпадать с MonsterBrain.patrolSpeed и скоростью
        /// агента, которую ставит сборщик песочницы. Разъедутся — вернётся
        /// скольжение, и искать причину снова придётся с нуля.
        /// </summary>
        private const float WalkSpeed = 1.3f;
        private const float RunSpeed = 3.4f;

        /// <summary>Сколько длится удар, если скорость не подкручена.</summary>
        private const float AttackDuration = 1.3f;

        /// <summary>
        /// Один вид зверя.
        ///
        /// clipFolder отдельно от model, потому что у волка клипы лежат
        /// россыпью в FBX рядом с моделью, а у каждого упыря — в собственной
        /// папке Animations. Общего правила нет, поэтому и записано явно.
        /// </summary>
        private struct Beast
        {
            public string prefabName;   // как назовём в Assets/_Game/Prefabs
            public string model;        // готовый префаб из набора
            public string clipFolder;   // где искать клипы
            public string idle, walk, run, attack, death;

            /// <summary>
            /// Частота шага относительно клипа. Единица — как нарисовано.
            ///
            /// Пороги решают, КАКОЙ клип играет, а это — насколько быстро он
            /// перебирает ногами. Даже на верном пороге художник мог
            /// нарисовать бег под другую скорость, и тогда ноги всё равно
            /// скользят — только уже ровно, а не рывками.
            ///
            /// Больше единицы — чаще шаг, меньше — реже. Подбирается глазом
            /// по одному зверю: если он будто едет на коньках, число
            /// поднимают; если сучит ногами на месте — опускают.
            /// </summary>
            public float stride;
        }

        private const string Wolf = "Assets/Polygonal Wolf";
        private const string Ghouls = "Assets/BitGem/Ghoul-Crew-Hand-Painted-Series";

        private static Beast WolfOf(string colour) => new Beast
        {
            prefabName = "Wolf_" + colour,
            model = Wolf + "/Prefabs/Polygonal Wolf " + colour + ".prefab",
            clipFolder = Wolf + "/FBX",
            idle = "Idle",

            // Именно «WO Root» — без корневого движения.
            //
            // У волка каждая походка идёт в двух версиях. Версия с корнем
            // двигает модель сама, а положение у нас ведёт навигационный
            // агент: два хозяина у одной позиции — это волк, уезжающий от
            // собственного агента и бьющий по воздуху.
            walk = "Walk Forward WO Root",
            run = "Run Forward WO Root",
            attack = "Bite Attack",
            death = "Die",
            stride = 1f,
        };

        private static Beast GhoulOf(string prefabName, string folder, string model) => new Beast
        {
            prefabName = prefabName,
            model = Ghouls + "/" + folder + "/Prefabs/" + model + ".prefab",
            clipFolder = Ghouls + "/" + folder + "/Animations",
            idle = "idle",
            walk = "walk",
            run = "run",
            attack = "attack",
            death = "die",
            stride = 1f,
        };

        private static readonly Beast[] Roster =
        {
            WolfOf("Brown"),
            WolfOf("Black"),
            WolfOf("White"),

            GhoulOf("Ghoul",            "Ghoul",            "ghoul"),
            GhoulOf("Ghoul_Scavenger",  "Ghoul-Scavenger",  "ghoul_scavenger"),
            GhoulOf("Ghoul_Festering",  "Ghoul-Festering",  "ghoul_festering"),
            GhoulOf("Ghoul_Grotesque",  "Ghoul-Grotesque",  "ghoul_grotesque"),
            GhoulOf("Ghoul_Boss",       "Ghoul-Boss",       "ghoul_boss"),
        };

        [MenuItem("Tools/IsoRPG/Собрать зверей", priority = 14)]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Сначала выйди из режима Play",
                    "В режиме Play ассеты не сохраняются на диск.", "Понятно");
                return;
            }

            EnsureFolder(ControllersFolder);

            int made = 0;

            foreach (var beast in Roster)
            {
                var clips = CollectClips(beast.clipFolder);

                if (clips.Count == 0)
                {
                    Debug.LogWarning("[IsoRPG] Для " + beast.prefabName + " не нашлось клипов в " +
                                     beast.clipFolder + " — набор не импортирован?");
                    continue;
                }

                // Зацикливаем походку ДО сборки контроллера: он берёт клипы
                // такими, какие они есть, и незацикленный шаг внутри дерева
                // смешивания уже не починишь.
                int looped = 0;
                foreach (string name in new[] { beast.idle, beast.walk, beast.run })
                    if (!string.IsNullOrEmpty(name) &&
                        clips.TryGetValue(name, out var motion) && EnsureLooping(motion))
                        looped++;

                if (looped > 0)
                    Debug.Log("[IsoRPG] " + beast.prefabName + ": зациклено клипов " + looped + ".");

                var controller = BuildController(beast, clips);
                if (controller == null) continue;

                // Контроллер должен лечь в базу ДО того, как его попросит
                // префаб: ассет, созданный и загруженный в одном кадре,
                // отдаётся пустой ссылкой, и префаб молча выйдет без анимаций.
                AssetDatabase.SaveAssets();

                if (BuildPrefab(beast, controller)) made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[IsoRPG] Зверей собрано " + made + " из " + Roster.Length +
                      ". Дальше — «Собрать песочницу», чтобы они появились на карте.");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Включает зацикливание у клипа, если его там не было.
        ///
        /// Ради чего: у всех пятерых упырей BitGem ни один клип не зациклен —
        /// ни покой, ни шаг, ни бег. Незацикленный бег проигрывается ОДИН раз
        /// (у обычного упыря это две трети секунды) и застывает на последнем
        /// кадре, пока агент продолжает везти тело вперёд. Со стороны упырь
        /// едет по земле в неподвижной позе — ровно то, что было видно в
        /// игре. Стоящий упырь замирал так же, просто через две с половиной
        /// секунды, и этого никто не замечал.
        ///
        /// Возвращает true, если правда починили, — чтобы в журнале было
        /// видно, что тронуто, а не просто «готово».
        ///
        /// Только самостоятельные .anim. У клипов внутри FBX эта настройка
        /// живёт в импортёре модели, а не в самом клипе, и правится совсем
        /// иначе. Волку это и не нужно: его клипы приехали зацикленными, он
        /// потому и бегает нормально.
        /// </summary>
        private static bool EnsureLooping(AnimationClip clip)
        {
            if (clip == null) return false;

            string path = AssetDatabase.GetAssetPath(clip);

            if (!path.EndsWith(".anim", System.StringComparison.OrdinalIgnoreCase))
            {
                var inside = AnimationUtility.GetAnimationClipSettings(clip);

                if (!inside.loopTime)
                    Debug.LogWarning("[IsoRPG] Клип «" + clip.name + "» лежит внутри модели " +
                                     "и не зациклен. Поправь галочку Loop Time в импортёре " +
                                     path + " — отсюда её не достать.");
                return false;
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime) return false;

            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);

            return true;
        }

        private static Dictionary<string, AnimationClip> CollectClips(string folder)
        {
            var found = new Dictionary<string, AnimationClip>();

            if (!AssetDatabase.IsValidFolder(folder)) return found;

            // И модели, и отдельные .anim: у волка клипы лежат внутри FBX, у
            // упырей вынесены в самостоятельные файлы. Ищем и то, и другое.
            foreach (string guid in AssetDatabase.FindAssets("t:Model t:AnimationClip",
                                                             new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path)
                                                  .OfType<AnimationClip>())
                {
                    if (clip.name.StartsWith("__preview")) continue;
                    if (!found.ContainsKey(clip.name)) found[clip.name] = clip;
                }
            }

            return found;
        }

        private static AnimatorController BuildController(
            Beast beast, Dictionary<string, AnimationClip> clips)
        {
            string path = ControllersFolder + "/AC_" + beast.prefabName + ".controller";

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            var speedParam = new AnimatorControllerParameter
            {
                name = "AttackSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            };
            controller.AddParameter(speedParam);

            var root = controller.layers[0].stateMachine;

            // --- Движение: одна ось от покоя к бегу ---
            var tree = new BlendTree
            {
                name = "Move",
                blendParameter = "Speed",
                blendType = BlendTreeType.Simple1D,
            };

            AssetDatabase.AddObjectToAsset(tree, controller);

            float stride = beast.stride > 0f ? beast.stride : 1f;

            AddMotion(tree, clips, beast.idle, 0f, 1f);
            AddMotion(tree, clips, beast.walk, WalkSpeed, stride);
            AddMotion(tree, clips, beast.run, RunSpeed, stride);

            var move = root.AddState("Move");
            move.motion = tree;
            root.defaultState = move;

            // --- Удар ---
            //
            // Возврат по времени клипа, а не по условию: боевая система шлёт
            // только сигнал начала, а закончить удар анимация должна сама.
            if (clips.TryGetValue(beast.attack, out var attackClip))
            {
                var attack = root.AddState("Attack");
                attack.motion = attackClip;
                attack.speedParameter = "AttackSpeed";
                attack.speedParameterActive = true;

                var toAttack = move.AddTransition(attack);
                toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                toAttack.hasExitTime = false;
                toAttack.duration = 0.05f;

                var back = attack.AddTransition(move);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.15f;
            }
            else
            {
                Debug.LogWarning("[IsoRPG] У " + beast.prefabName +
                                 " нет клипа удара «" + beast.attack + "» — бить будет молча.");
            }

            // --- Смерть ---
            //
            // Из ЛЮБОГО состояния и без возврата: умереть можно и посреди
            // замаха, а вставать зверю уже не надо. Возрождение поднимает
            // объект заново, а не отыгрывает смерть назад.
            if (clips.TryGetValue(beast.death, out var deathClip))
            {
                var death = root.AddState("Death");
                death.motion = deathClip;

                var toDeath = root.AddAnyStateTransition(death);
                toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                toDeath.hasExitTime = false;
                toDeath.duration = 0.1f;
                toDeath.canTransitionToSelf = false;

                // Обратно — когда возродился. Без этого воскресший зверь
                // остаётся лежать и бьёт из положения трупа.
                var revive = death.AddTransition(move);
                revive.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
                revive.hasExitTime = false;
                revive.duration = 0.1f;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddMotion(BlendTree tree,
                                      Dictionary<string, AnimationClip> clips,
                                      string name, float threshold, float stride)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (!clips.TryGetValue(name, out var clip))
            {
                Debug.LogWarning("[IsoRPG] Нет клипа «" + name + "» — походка будет неполной.");
                return;
            }

            tree.AddChild(clip, threshold);

            if (Mathf.Approximately(stride, 1f)) return;

            // children отдаёт КОПИЮ массива, а не сам массив: правка на месте
            // молча ничего не изменит. Меняем копию и кладём обратно целиком —
            // ровно та ловушка, на которой такие правки обычно и теряются.
            var children = tree.children;
            children[children.Length - 1].timeScale = stride;
            tree.children = children;
        }

        private static bool BuildPrefab(Beast beast, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(beast.model);

            if (model == null)
            {
                Debug.LogError("[IsoRPG] Не найдена модель " + beast.model);
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = beast.prefabName;

            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;

            // Позицию ведёт навигационный агент. С корневым движением
            // анимация тянет зверя сама, и он уезжает от своего агента.
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            // Свои коллайдеры набору не нужны: по монстру кликают через
            // капсулу на корне, которую вешает сборщик песочницы. Два
            // коллайдера на одном существе дали бы два попадания луча на
            // один клик — и выбор цели стал бы через раз.
            foreach (var collider in instance.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(collider);

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabsFolder + "/" + beast.prefabName + ".prefab");
            Object.DestroyImmediate(instance);

            return true;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
