using System.Linq;
using IsoRPG.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит гриба-монстра — второго акцентного врага, в засаде.
    ///
    /// Место выбрал Павлон 02.09.2026: (−43, −113), уровень 6, «что-то типа
    /// босса». Собран тем же трёхэтажным приёмом, что кабаны и волки:
    /// корень с навигацией → узел наклона по рельефу → модель с аниматором.
    ///
    /// Отличие от босса-кабана одно, зато важное: гриб СПИТ, пока игрок не
    /// подойдёт (см. <see cref="AmbushSleeper"/>), и в это время выглядит
    /// обычным грибом. Такие позы есть в самом наборе — автор рисовал их
    /// именно под засаду.
    /// </summary>
    public static class MushroomBossPack
    {
        private const string ModelPath =
            "Assets/InfinityPBR/_InfinityPBR - Mushroom Monster/Models/Mushroom_LP.fbx";
        private const string GroupName = "Гриб-исполин";
        private const string Arena = "Assets/_Game/Scenes/ArenaAuthor.unity";

        private static readonly Vector2 Spot = new Vector2(-43f, -113f);

        private const int Hp = 240;
        private const int Level = 6;
        private const int Armor = 6;

        /// <summary>
        /// Какого роста должен получиться гриб, в метрах.
        ///
        /// Задаём РОСТ, а не множитель: модель из набора оказалась пятиметровой,
        /// и множитель 1.5, поставленный на глаз, дал зверя в 7.6 м — выше
        /// деревьев. Это ровно то, о чём правило «габариты нового вида мерить
        /// ДО посева»: множитель ничего не говорит, пока не знаешь исходник.
        ///
        /// Три с половиной метра: Павлон 02.09.2026 посмотрел двух с половиной
        /// в игре и попросил «на метр больше». Вдвое выше героя — для
        /// одиночного босса в засаде это и нужно.
        ///
        /// Задаём РОСТ, а не множитель: множитель ничего не говорит, пока не
        /// знаешь исходник, — на этом я и обжёгся, поставив 1.5 на глаз и
        /// получив зверя в 7.6 м.
        /// </summary>
        private const float TargetHeight = 3.5f;

        /// <summary>Высота модели по её отрисовщикам, в её собственном масштабе.</summary>
        private static float MeasureHeight(GameObject model)
        {
            var parts = model.GetComponentsInChildren<Renderer>(true);
            if (parts.Length == 0) return 0f;

            var box = parts[0].bounds;
            foreach (var part in parts) box.Encapsulate(part.bounds);

            return box.size.y;
        }

        [MenuItem("Tools/IsoRPG/Мир: поставить гриба-босса", priority = 42)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            // Открываем боевую сцену САМИ.
            //
            // 02.09.2026 гриб «встал» в старую `Arena.unity`, которая была
            // открыта с прошлого раза, а следующим шагом сборка открыла
            // боевую сцену — и правка ушла вместе с закрытой. Отчёт при этом
            // был честный: зверь действительно поставлен, только не там.
            if (EditorSceneManager.GetActiveScene().path != Arena)
                EditorSceneManager.OpenScene(Arena, OpenSceneMode.Single);

            var controller = MushroomAnimations.Build();
            if (controller == null) return;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

            if (source == null)
            {
                Debug.LogError("[IsoRPG] Не найдена модель гриба: " + ModelPath);
                return;
            }

            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — не на что ставить гриба.");
                return;
            }

            var old = GameObject.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old);

            float y = terrain.SampleHeight(new Vector3(Spot.x, 0f, Spot.y)) +
                      terrain.transform.position.y;

            if (NavMesh.SamplePosition(new Vector3(Spot.x, y, Spot.y), out var hit, 6f, NavMesh.AllAreas))
                y = hit.position.y;
            else
                Debug.LogWarning("[IsoRPG] Точка гриба вне навигационной сетки — стоять сможет, ходить нет.");

            var boss = new GameObject(GroupName);
            boss.transform.position = new Vector3(Spot.x, y, Spot.y);

            var tilt = new GameObject("Наклон");
            tilt.transform.SetParent(boss.transform, false);
            tilt.AddComponent<IsoRPG.World.GroundAlign>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);

            // Распаковываем связь с FBX.
            //
            // Модель кладётся в сцену как экземпляр модельного префаба, а он
            // неизменяем: контроллер и аватар, назначенные такому экземпляру,
            // не держатся. 02.09.2026 щуп `mob-anim-probe` показал ровно это —
            // «Гриб-исполин: КОНТРОЛЛЕРА НЕТ, АВАТАРА НЕТ» сразу после того,
            // как задание отчиталось, что всё поставило.
            //
            // У Synty такой беды не было: там в сцену кладут настоящий
            // префаб, а не сам FBX.
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely,
                                               InteractionMode.AutomatedAction);

            model.name = "Модель";
            model.transform.SetParent(tilt.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // Меряем модель в её собственном размере и подгоняем под нужный
            // рост. Порядок важен: сначала замер, потом масштаб — иначе
            // меряешь уже растянутое и получаешь ерунду.
            float raw = MeasureHeight(model);
            float scale = TargetHeight > 0.01f && raw > 0.01f ? TargetHeight / raw : 1f;

            model.transform.localScale = Vector3.one * scale;

            // Красный с белыми пятнами — тот самый мухомор с картинки
            // Павлона. Берём готовый материал набора, а не ColorShifter: тот
            // ссылается на шейдер IPBR_DiffuseHSL под встроенный конвейер, и
            // в URP даёт пурпур.
            var skin = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/InfinityPBR/_InfinityPBR - Mushroom Monster/" +
                "Textures & Materials/MushroomMonsterLP_Red.mat");

            if (skin != null)
            {
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = skin;
                    renderer.sharedMaterials = mats;
                }
            }

            int fixedMats = MobMaterials.FixLegacyShaders(model);

            // Ставим подошвой на грунт: у модели из набора опорная точка
            // может лежать где угодно, и без этого зверь висит или тонет.
            var parts = model.GetComponentsInChildren<Renderer>(true);
            float lift = 0f;
            float height = 2f;

            if (parts.Length > 0)
            {
                var box = parts[0].bounds;
                foreach (var part in parts) box.Encapsulate(part.bounds);

                float groundY = terrain.SampleHeight(new Vector3(Spot.x, 0f, Spot.y)) +
                                terrain.transform.position.y;

                lift = groundY - box.min.y;
                height = box.size.y;
                tilt.transform.localPosition = new Vector3(0f, lift, 0f);
            }

            var animator = boss.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            // Аватар обязателен.
            //
            // Скелет у гриба обычный (Generic), и без аватара аниматор не
            // знает, к каким костям привязывать клипы: анимация молчит, а
            // зверь едет по земле в позе покоя. Именно это Павлон и увидел
            // 02.09.2026 — «анимаций не вижу, он ездит по земле».
            //
            // У префабов Synty аватар лежит внутри и приезжает сам; здесь мы
            // кладём в сцену сам FBX, и аватар надо взять из него руками.
            if (animator.avatar == null)
            {
                var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                                          .OfType<Avatar>()
                                          .FirstOrDefault();

                if (avatar != null) animator.avatar = avatar;
                else Debug.LogWarning("[IsoRPG] У модели гриба нет аватара — анимации молчат.");
            }

            var body = boss.AddComponent<CapsuleCollider>();
            body.height = Mathf.Max(1.6f, height);
            body.radius = 0.8f;
            body.center = new Vector3(0f, body.height * 0.5f, 0f);

            var targetable = boss.AddComponent<Targetable>();
            targetable.Setup(GroupName, Faction.Hostile);

            var health = boss.AddComponent<Health>();
            health.Setup(Hp);

            var defense = boss.AddComponent<DefenseStats>();
            defense.Setup(Level, Armor);

            var agent = boss.AddComponent<NavMeshAgent>();
            // Медленнее кабана: гриб ходит, а не бегает — у него и бега нет
            // в наборе. Это не недостаток, а характер: от него можно уйти,
            // но нельзя стоять рядом.
            agent.speed = 1.9f;
            agent.angularSpeed = 360f;
            agent.acceleration = 6f;
            agent.radius = 0.5f;   // как в сетке навигации, а не по размеру модели
            agent.height = 2f;      // сетка построена под рост 2 м
            agent.stoppingDistance = 0.1f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.avoidancePriority = 40;

            var selector = boss.AddComponent<TargetSelector>();
            selector.SetFaction(Faction.Hostile);

            boss.AddComponent<MeleeCombatant>();
            boss.AddComponent<MonsterBrain>();
            boss.AddComponent<IsoRPG.Player.CharacterAnimatorDriver>();

            // Засада — последней: она гасит навигацию и мозг, а те должны
            // быть уже на месте.
            boss.AddComponent<AmbushSleeper>();

            // Лишний аниматор на корне — вон.
            //
            // Его могли добавить компоненты по требованию (так и вышло с
            // засадой), и он перехватывает любой поиск «первого аниматора в
            // ветке»: настоящий живёт на модели. Проверяем и здесь, чтобы
            // грабли не вернулись с другим компонентом.
            var stray = boss.GetComponent<Animator>();

            if (stray != null && stray != animator)
            {
                Object.DestroyImmediate(stray);
                Debug.Log("[IsoRPG] Снят лишний пустой аниматор с корня гриба.");
            }

            // Щуп на месте: перечисляем ВСЕ аниматоры ветки с их
            // контроллерами. 02.09.2026 щуп по сцене доложил «контроллера
            // нет» сразу после того, как задание отчиталось об успехе, — а
            // ссылка на контроллер в файле сцены при этом была. Значит
            // аниматоров больше одного, и читают они разные.
            var all = boss.GetComponentsInChildren<Animator>(true);

            var report = new System.Text.StringBuilder("[IsoRPG] Аниматоры гриба: " + all.Length + "\n");

            foreach (var a in all)
            {
                report.Append("  на «").Append(a.gameObject.name).Append("»: ")
                      .Append(a.runtimeAnimatorController != null
                                  ? a.runtimeAnimatorController.name : "КОНТРОЛЛЕРА НЕТ")
                      .Append(a.avatar != null ? ", аватар есть" : ", АВАТАРА НЕТ")
                      .Append('\n');
            }

            Debug.Log(report.ToString());

            EditorUtility.SetDirty(boss);
            EditorSceneManager.MarkAllScenesDirty();

            // Сохраняем сами, а не надеемся на общий проход в конце прогона:
            // следующее задание в очереди может открыть другую сцену, и тогда
            // наша правка уедет в никуда.
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[IsoRPG] Гриб-исполин поставлен на {boss.transform.position:0.0}, " +
                      $"рост {height:0.0} м (модель {raw:0.0} м, масштаб {scale:0.00}), " +
                      $"подъём модели {lift:0.00} м, " +
                      $"материалов на URP переведено {fixedMats}, " +
                      $"здоровье {Hp}, уровень {Level}, броня {Armor}, в засаде.");
        }
    }
}
