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
    /// Ставит на карту кабанов — тем же приёмом, что и волков
    /// (<see cref="WolfPack"/>): три этажа корень/наклон/модель, боевые
    /// части общие с прочими монстрами.
    ///
    /// Разведены по всей карте, а не одной стаей: кабан — одиночный зверь,
    /// не стайный, и десяток в одном месте выглядел бы неестественно.
    /// </summary>
    public static class BoarPack
    {
        private const string PrefabPath =
            "Assets/Malbers Animations/Animals Packs/01 Forest Pack/Boar/Models/Boar.prefab";
        private const string GroupName = "Кабаны";

        // Кластер у (30, −62) — координаты Павлона прямо с миникарты в игре,
        // проверено на месте, воды там нет.
        private static readonly Vector2[] Spots =
        {
            new Vector2( 20f, -70f),
            new Vector2( 35f, -68f),
            new Vector2( 42f, -60f),
            new Vector2( 38f, -52f),
            new Vector2( 22f, -55f),
            new Vector2( 28f, -72f),
            new Vector2( 45f, -72f),
            new Vector2( 15f, -62f),
            new Vector2( 32f, -45f),
            new Vector2( 48f, -50f),
        };

        private const int Hp = 60;
        private const int Level = 3;
        private const int Armor = 3;

        [MenuItem("Tools/IsoRPG/Мир: поставить кабанов", priority = 39)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            var controller = BoarAnimations.Build();
            if (controller == null) return;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (source == null)
            {
                Debug.LogError("[IsoRPG] Не найден префаб кабана: " + PrefabPath);
                return;
            }

            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — не на что ставить кабанов.");
                return;
            }

            var old = GameObject.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old);

            var group = new GameObject(GroupName);
            int placed = 0;

            for (int i = 0; i < Spots.Length; i++)
            {
                var spot = Spots[i];

                // Проверка InBowl() тут больше не стоит НАРОЧНО: этот
                // прогон она дважды подряд ошибочно отвергала точки,
                // которые Павлон лично проверил в игре и подтвердил сухими
                // — она смотрит на круг по максимальному радиусу залива, а
                // не на форму настоящего берега.

                float y = terrain.SampleHeight(new Vector3(spot.x, 0f, spot.y)) +
                          terrain.transform.position.y;

                if (NavMesh.SamplePosition(new Vector3(spot.x, y, spot.y),
                                           out var hit, 6f, NavMesh.AllAreas))
                {
                    y = hit.position.y;
                }
                else
                {
                    Debug.LogWarning("[IsoRPG] Точка " + spot + " вне навигационной сетки — " +
                                     "кабан там стоять сможет, а ходить нет.");
                }

                var boar = new GameObject("Кабан " + (i + 1));
                boar.transform.SetParent(group.transform, false);
                boar.transform.position = new Vector3(spot.x, y, spot.y);
                boar.transform.rotation = Quaternion.Euler(0f, i * 97f, 0f);

                var tilt = new GameObject("Наклон");
                tilt.transform.SetParent(boar.transform, false);
                tilt.AddComponent<IsoRPG.World.GroundAlign>();

                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.name = "Модель";
                model.transform.SetParent(tilt.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                int fixedMats = MobMaterials.FixLegacyShaders(model);

                var parts = model.GetComponentsInChildren<Renderer>(true);

                if (parts.Length > 0)
                {
                    var box = parts[0].bounds;
                    foreach (var part in parts) box.Encapsulate(part.bounds);

                    float groundY = terrain.SampleHeight(new Vector3(spot.x, 0f, spot.y)) +
                                    terrain.transform.position.y;

                    float lift = groundY - box.min.y;
                    tilt.transform.localPosition = new Vector3(0f, lift, 0f);

                    Debug.Log("[IsoRPG] Кабан " + (i + 1) + ": земля " + groundY.ToString("0.00") +
                              ", низ модели " + box.min.y.ToString("0.00") +
                              ", подъём модели " + lift.ToString("0.00") +
                              " м, материалов на URP переведено " + fixedMats + ".");
                }

                var animator = boar.GetComponentInChildren<Animator>();
                if (animator == null) animator = boar.AddComponent<Animator>();

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                var body = boar.GetComponent<CapsuleCollider>();
                if (body == null) body = boar.AddComponent<CapsuleCollider>();

                // Заметно шире силуэта — ноги и клыки торчат за пределы
                // капсулы, и клик по краю модели промахивался мимо
                // коллайдера.
                body.height = 1.5f;
                body.radius = 0.75f;
                body.center = new Vector3(0f, 0.6f, 0f);

                var targetable = boar.AddComponent<Targetable>();
                targetable.Setup("Кабан", Faction.Hostile);

                var health = boar.AddComponent<Health>();
                health.Setup(Hp);

                var defense = boar.AddComponent<DefenseStats>();
                defense.Setup(Level, Armor);

                var agent = boar.AddComponent<NavMeshAgent>();
                agent.speed = 3.0f;
                agent.angularSpeed = 540f;
                agent.acceleration = 10f;
                agent.radius = 0.55f;
                agent.height = 1.1f;
                agent.stoppingDistance = 0.1f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                agent.avoidancePriority = 46;

                var selector = boar.AddComponent<TargetSelector>();
                selector.SetFaction(Faction.Hostile);

                boar.AddComponent<MeleeCombatant>();
                boar.AddComponent<MonsterBrain>();

                // Добыча: клык падает с каждого. По нему считается квест
                // Талина Кини — случайный дроп превратил бы «убей двенадцать»
                // в «убей сколько-то, как повезёт».
                var loot = boar.AddComponent<IsoRPG.Items.LootSource>();
                var table = ItemsBuilder.LoadTable("LT_Boar");

                if (table == null)
                    Debug.LogError("[IsoRPG] Нет таблицы LT_Boar — прогони «Создать предметы и добычу».");

                loot.Setup(table);
                loot.SetupModels(
                    SandboxSceneBuilder.LoadDungeonModel("box_small"),
                    AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/_Game/Art/Materials/M_Silhouette_Ally.mat"));
                EditorUtility.SetDirty(loot);
                boar.AddComponent<CharacterAnimatorDriver>();

                EditorUtility.SetDirty(boar);
                placed++;
            }

            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Кабаны: поставлено " + placed + " по карте, " +
                      "здоровье " + Hp + ", уровень " + Level + ", броня " + Armor + ".");
        }
    }
}
