using System.Collections.Generic;
using System.Linq;
using IsoRPG.Combat;
using IsoRPG.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace IsoRPG.EditorTools
{
    /// <summary>
    /// Ставит на карту стаю волков — живых, с боем.
    ///
    /// Волк собирается тем же набором частей, что и прочие монстры арены:
    /// цель для наведения, здоровье, защита, навигационный агент, ближний
    /// бой, мозг и водитель анимаций. Ничего своего боевой код для зверя не
    /// требует — он говорит с аниматором через параметры, а их волку даёт
    /// собранный нами контроллер.
    ///
    /// Стая ставится у малого пруда: до неё от старта метров двадцать —
    /// достаточно, чтобы дойти и проверить бой, и достаточно далеко, чтобы
    /// волки не набегали на героя в первую же секунду.
    /// </summary>
    public static class WolfPack
    {
        private const string GroupName = "Стая волков";

        /// <summary>Одно место стаи: позиция, окрас (имя префаба) и масштаб.</summary>
        private readonly struct Spot
        {
            public readonly Vector2 Pos;
            public readonly string Prefab;
            public readonly float Scale;

            /// <summary>
            /// Пропустить проверку «внутри чаши» для этой точки. Проверка
            /// исключает ВЕСЬ круг по максимальному радиусу залива, а берег
            /// неровный — реальная вода может не доставать. Ставить true
            /// только когда место проверено глазами в игре, не на карте.
            /// </summary>
            public readonly bool TrustedDry;

            public Spot(float x, float z, string prefab, float scale = 1f, bool trustedDry = false)
            {
                Pos = new Vector2(x, z);
                Prefab = prefab;
                Scale = scale;
                TrustedDry = trustedDry;
            }
        }

        /// <summary>
        /// Где стоят волки. Мир, метры. Высоту берём с земли.
        ///
        /// Первый заход я поставил их в (14,−18), (21,−25) и (27,−15) —
        /// и вся стая оказалась ПОД ВОДОЙ: малый пруд сидит в (20,−16), у
        /// него чаша 22 метра, а все три точки лежали от центра в шести-
        /// девяти метрах, то есть внутри глади. Ошибка не видна глазом в
        /// коде — числа выглядят «рядом с прудом», — поэтому ниже стоит
        /// проверка, которая ловит её на прогоне.
        ///
        /// Вся стая переведена в зону 2 по разметке Павлона на карте сверху —
        /// западнее малого кольцевого пруда (20, −16), по каменистому
        /// пятнистому участку и в сторону пруда (−46, 34). Двое — белые и
        /// крупнее обычных на 30%: редкая крупная особь, а не рядовой зверь.
        /// </summary>
        private static readonly Spot[] Spots =
        {
            new Spot(-20f, -20f, "Polygonal Wolf Brown"),
            new Spot(-15f, -25f, "Polygonal Wolf Black"),
            // (-38,-47): формально внутри радиуса чаши пруда (-62,-66), но
            // Павлон стоял там в игре и воды не увидел — берег неровный,
            // залив «чаши» не круглый, а радиус в коде это худший случай
            // по самому широкому языку залива, не факт воды во все стороны.
            // Верю глазам, а не грубой проверке: ставим прямо туда.
            new Spot(-38f, -47f, "Polygonal Wolf White", 1.3f, trustedDry: true),
            new Spot(-115f,-20f, "Polygonal Wolf Brown"),
            new Spot(-70f, -10f, "Polygonal Wolf Black"),
            new Spot(-34f, -50f, "Polygonal Wolf White", 1.3f, trustedDry: true),
            new Spot(-60f,  90f, "Polygonal Wolf Brown"),
            new Spot(-100f,-15f, "Polygonal Wolf Black"),
            new Spot(-10f, -15f, "Polygonal Wolf Brown"),
            new Spot(-15f,  75f, "Polygonal Wolf Black"),
        };

        /// <summary>
        /// С какой скоростью волк гонится, метры в секунду.
        ///
        /// Числом здесь, а не прямо в агенте, потому что его читает и сборщик
        /// анимаций: клип бега у набора идёт всего 2.47 м/с, и без поправки
        /// волк ехал бы быстрее, чем перебирает лапами.
        /// </summary>
        public const float ChaseSpeed = 3.6f;

        private const int Hp = 45;
        private const int Level = 2;
        private const int Armor = 2;

        /// <summary>
        /// На сколько опустить волка относительно земли, метров. Точка
        /// отсчёта у модели набора не в лапах, поэтому поставленный «на
        /// высоту грунта» зверь висит в воздухе. Число проверяется глазом.
        /// </summary>
        private const float WolfDrop = 0f;

        [MenuItem("Tools/IsoRPG/Мир: поставить стаю волков", priority = 38)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            var controller = WolfAnimations.Build();

            if (controller == null) return;

            // Префабы окрасов кэшируем по имени: пять белых стоят на том же
            // поиске, что и первый, повторный AssetDatabase.FindAssets на
            // каждого — лишняя работа.
            var prefabCache = new Dictionary<string, GameObject>();

            GameObject LoadPrefab(string prefabName)
            {
                if (prefabCache.TryGetValue(prefabName, out var cached)) return cached;

                var g = AssetDatabase.FindAssets(prefabName + " t:Prefab").FirstOrDefault();
                var loaded = g == null ? null
                    : AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));

                prefabCache[prefabName] = loaded;
                return loaded;
            }

            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — не на что ставить волков.");
                return;
            }

            // Старую стаю сносим целиком: иначе каждый прогон добавляет
            // новую поверх прежней, и через три захода у пруда толпа.
            var old = GameObject.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old);

            var group = new GameObject(GroupName);
            int placed = 0;

            for (int i = 0; i < Spots.Length; i++)
            {
                var spotInfo = Spots[i];
                var spot = spotInfo.Pos;

                var source = LoadPrefab(spotInfo.Prefab);

                if (source == null)
                {
                    Debug.LogError("[IsoRPG] Не найден префаб волка «" + spotInfo.Prefab + "».");
                    continue;
                }

                // Точка внутри водоёма — это утонувший волк, а не «зверь у
                // воды». Пропускаем только ЭТУ точку, а не всю стаю: снос
                // всей группы на одной плохой координате однажды стоил
                // нам всех волков разом, включая уже верно поставленных.
                if (!spotInfo.TrustedDry && SyntyWater.InBowl(spot, 2f))
                {
                    Debug.LogError("[IsoRPG] Точка " + spot + " внутри чаши водоёма — " +
                                   "волк пропущен, поправь координаты.");
                    continue;
                }

                float y = terrain.SampleHeight(new Vector3(spot.x, 0f, spot.y)) +
                          terrain.transform.position.y + WolfDrop;

                // Ставим на навигационную сетку, а не на «высоту грунта».
                //
                // Высота грунта берётся в одной точке, а зверь занимает
                // больше метра: на склоне он этой точкой опирается, а
                // остальными лапами висит. Навигационная сетка уже лежит по
                // проходимой поверхности, и точка на ней — та, где агент
                // реально стоит. Заодно проверяем, что место вообще
                // проходимо: волк вне сетки не сдвинется с места, и это
                // выглядит как поломка ИИ.
                if (NavMesh.SamplePosition(new Vector3(spot.x, y, spot.y),
                                           out var hit, 6f, NavMesh.AllAreas))
                {
                    y = hit.position.y + WolfDrop;
                }
                else
                {
                    Debug.LogWarning("[IsoRPG] Точка " + spot + " вне навигационной сетки — " +
                                     "волк там стоять сможет, а ходить нет.");
                }

                // Волк собирается из двух узлов.
                //
                // Корень несёт агента и боевые части и всегда стоит
                // вертикально — так его держит навигация. Модель лежит
                // внутри и наклоняется по склону сама: иначе на подъёме
                // висят передние лапы, на спуске задние. Драться с агентом
                // за поворот корня бесполезно, он возвращает вертикаль
                // каждый кадр.
                var wolf = new GameObject("Волк " + (i + 1));
                wolf.transform.SetParent(group.transform, false);
                wolf.transform.position = new Vector3(spot.x, y, spot.y);
                wolf.transform.rotation = Quaternion.Euler(0f, i * 120f, 0f);

                // Между корнем и моделью — отдельный узел наклона.
                //
                // Наклонять сам объект с аниматором бесполезно: риг волка
                // generic, его клипы держат кривые для корня модели, и
                // аниматор каждый кадр возвращает свой поворот. Мой наклон
                // стирался ровно там же, где ставился, — снаружи это
                // выглядело как «компонент не работает».
                //
                // Отсюда три этажа: корень ходит (агент держит вертикаль),
                // средний наклоняется по склону, модель анимируется.
                var tilt = new GameObject("Наклон");
                tilt.transform.SetParent(wolf.transform, false);
                tilt.AddComponent<IsoRPG.World.GroundAlign>();

                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.name = "Модель";
                model.transform.SetParent(tilt.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * spotInfo.Scale;

                // Сажаем модель на землю по её же мешу.
                //
                // Константу просадки я сначала взял с потолка, и волк
                // висел. Правильнее спросить саму модель: где у неё низ.
                // Складываем габариты всех кусков и опускаем ровно на
                // столько, на сколько нижняя точка не достаёт до нуля.
                var parts = model.GetComponentsInChildren<Renderer>(true);

                if (parts.Length > 0)
                {
                    var box = parts[0].bounds;
                    foreach (var part in parts) box.Encapsulate(part.bounds);

                    float groundY = terrain.SampleHeight(new Vector3(spot.x, 0f, spot.y)) +
                                    terrain.transform.position.y;

                    // Считаем от ЗЕМЛИ, а не от корня.
                    //
                    // Корень стоит на точке навигационной сетки, а она может
                    // лежать заметно выше грунта — у нас на одном из мест
                    // разошлось на 89 см: сетку пекли по коллайдерам, и она
                    // легла поверх кустов. Просадка от корня в таком месте
                    // честно считалась и честно оставляла волка в воздухе.
                    // Посадку в игре ведёт GroundAlign — каждый кадр. Здесь
                    // ставим только начальное значение, чтобы волк не мигал
                    // в первом кадре и красиво выглядел в редакторе.
                    float lift = groundY - box.min.y;
                    tilt.transform.localPosition = new Vector3(0f, lift, 0f);

                    // Числа в журнал, а не в догадки. Я дважды подряд менял
                    // просадку наугад и оба раза промахнулся — теперь видно,
                    // что именно расходится: земля, точка на сетке или низ
                    // модели.
                    Debug.Log("[IsoRPG] Волк " + (i + 1) + " (" + spotInfo.Prefab + ", ×" +
                              spotInfo.Scale.ToString("0.0") + "): земля " + groundY.ToString("0.00") +
                              ", корень " + wolf.transform.position.y.ToString("0.00") +
                              ", низ модели " + box.min.y.ToString("0.00") +
                              ", подъём модели " + lift.ToString("0.00") + " м.");
                }

                // Аниматор ставим НА модель и подменяем контроллер: родной
                // знает свои состояния, а наш боевой код говорит с ним
                // нашими параметрами.
                var animator = wolf.GetComponentInChildren<Animator>();
                if (animator == null) animator = wolf.AddComponent<Animator>();

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;   // ведёт навигация

                var body = wolf.GetComponent<CapsuleCollider>();
                if (body == null) body = wolf.AddComponent<CapsuleCollider>();

                // Хитбокс и агент растут вместе с моделью: у крупных белых
                // волков коллайдер обычного размера тонул бы внутри модели
                // на треть, и удары мимо силуэта засчитывались бы мимо.
                // Пошире силуэта — клик по краю модели (лапы, хвост)
                // промахивался мимо узкой капсулы.
                body.height = 1.5f * spotInfo.Scale;
                body.radius = 0.6f * spotInfo.Scale;
                body.center = new Vector3(0f, 0.65f * spotInfo.Scale, 0f);

                var targetable = wolf.AddComponent<Targetable>();
                targetable.Setup("Волк", Faction.Hostile);

                // Лицо по масти.
                //
                // Имя у всех волков одно, «Волк», поэтому таблица портретов
                // по имени выдаёт им общее серое лицо — Павлон 03.09.2026
                // увидел серую морду у белого волка. Масть же известна прямо
                // здесь, из имени модели, и назначить портрет проще, чем
                // разводить зверей по именам: «Белый волк» в интерфейсе
                // потребовал бы и своей строки перевода, и своей записи в
                // каждом задании на охоту.
                bool white = spotInfo.Prefab.IndexOf("White",
                                 System.StringComparison.OrdinalIgnoreCase) >= 0;

                targetable.SetPortraitKey(white ? "Wolf_White" : "Wolf_Grey");

                var health = wolf.AddComponent<Health>();
                health.Setup(Hp);

                var defense = wolf.AddComponent<DefenseStats>();
                defense.Setup(Level, Armor);

                var agent = wolf.AddComponent<NavMeshAgent>();
                agent.speed = ChaseSpeed;
                agent.angularSpeed = 720f;
                agent.acceleration = 12f;
                agent.radius = 0.45f * spotInfo.Scale;
                agent.height = 1.2f * spotInfo.Scale;

                // Та же малая дистанция остановки, что у прочих монстров:
                // погоня ведёт зверя в точку перед героем, и большой отступ
                // складывается с ней — волк встал бы поодаль и не доставал.
                agent.stoppingDistance = 0.1f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                agent.avoidancePriority = 45;

                var selector = wolf.AddComponent<TargetSelector>();
                selector.SetFaction(Faction.Hostile);

                wolf.AddComponent<MeleeCombatant>();
                wolf.AddComponent<MonsterBrain>();
                wolf.AddComponent<CharacterAnimatorDriver>();

                EditorUtility.SetDirty(wolf);
                placed++;
            }

            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Стая волков: поставлено " + placed + " по карте, " +
                      "здоровье " + Hp + ", уровень " + Level + ", броня " + Armor + ".");
        }
    }
}
