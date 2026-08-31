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
    /// Ставит одного босса-кабана — акцентного врага покрупнее и покрепче
    /// рядовых, тем же трёхэтажным приёмом (корень/наклон/модель), что и
    /// у волков с обычными кабанами. Один экземпляр, вдали от прочей
    /// живности и от водоёмов — редкая встреча, а не рядовой моб стаи.
    /// </summary>
    public static class BoarBossPack
    {
        private const string PrefabPath =
            "Assets/Blink/Art/Animals/Stylized/BoarBoss/BoarBoss_Prefabs/BoarBoss_1.prefab";
        private const string GroupName = "Босс-кабан";

        // Рядом с кластером рядовых кабанов у (30, −62), но чуть в стороне.
        private static readonly Vector2 Spot = new Vector2(30f, -40f);

        private const int Hp = 220;
        private const int Level = 6;
        private const int Armor = 5;

        [MenuItem("Tools/IsoRPG/Мир: поставить босса-кабана", priority = 41)]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[IsoRPG] В режиме Play изменения не сохранятся.");
                return;
            }

            var controller = BoarBossAnimations.Build();
            if (controller == null) return;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (source == null)
            {
                Debug.LogError("[IsoRPG] Не найден префаб босса-кабана: " + PrefabPath);
                return;
            }

            var terrain = Object.FindObjectsByType<Terrain>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();

            if (terrain == null)
            {
                Debug.LogError("[IsoRPG] Террейна нет — не на что ставить босса.");
                return;
            }

            var old = GameObject.Find(GroupName);
            if (old != null) Object.DestroyImmediate(old);

            float y = terrain.SampleHeight(new Vector3(Spot.x, 0f, Spot.y)) +
                      terrain.transform.position.y;

            if (NavMesh.SamplePosition(new Vector3(Spot.x, y, Spot.y), out var hit, 6f, NavMesh.AllAreas))
                y = hit.position.y;
            else
                Debug.LogWarning("[IsoRPG] Точка босса вне навигационной сетки — стоять сможет, ходить нет.");

            var boss = new GameObject(GroupName);
            boss.transform.position = new Vector3(Spot.x, y, Spot.y);

            var tilt = new GameObject("Наклон");
            tilt.transform.SetParent(boss.transform, false);
            tilt.AddComponent<IsoRPG.World.GroundAlign>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Модель";
            model.transform.SetParent(tilt.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            int fixedMats = MobMaterials.FixLegacyShaders(model);

            var parts = model.GetComponentsInChildren<Renderer>(true);
            float lift = 0f;

            if (parts.Length > 0)
            {
                var box = parts[0].bounds;
                foreach (var part in parts) box.Encapsulate(part.bounds);

                float groundY = terrain.SampleHeight(new Vector3(Spot.x, 0f, Spot.y)) +
                                terrain.transform.position.y;

                lift = groundY - box.min.y;
                tilt.transform.localPosition = new Vector3(0f, lift, 0f);
            }

            var animator = boss.GetComponentInChildren<Animator>();
            if (animator == null) animator = boss.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var body = boss.AddComponent<CapsuleCollider>();
            // Пошире силуэта — ирокез и клыки торчат за пределы капсулы.
            body.height = 2.2f;
            body.radius = 1.1f;
            body.center = new Vector3(0f, 1f, 0f);

            var targetable = boss.AddComponent<Targetable>();
            targetable.Setup("Вожак кабанов", Faction.Hostile);

            var health = boss.AddComponent<Health>();
            health.Setup(Hp);

            var defense = boss.AddComponent<DefenseStats>();
            defense.Setup(Level, Armor);

            var agent = boss.AddComponent<NavMeshAgent>();
            agent.speed = 2.6f;
            agent.angularSpeed = 480f;
            agent.acceleration = 8f;
            agent.radius = 0.85f;
            agent.height = 1.8f;
            agent.stoppingDistance = 0.1f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = 40;   // ниже число — выше приоритет: босс не уступает дорогу рядовым

            var selector = boss.AddComponent<TargetSelector>();
            selector.SetFaction(Faction.Hostile);

            boss.AddComponent<MeleeCombatant>();
            var brain = boss.AddComponent<MonsterBrain>();
            brain.GiveVoice(10f);   // рычит при захвате цели — редкая встреча должна быть слышна
            boss.AddComponent<CharacterAnimatorDriver>();

            EditorUtility.SetDirty(boss);
            EditorSceneManager.MarkAllScenesDirty();

            Debug.Log("[IsoRPG] Босс-кабан поставлен на " + boss.transform.position.ToString("0.0") +
                      ", подъём модели " + lift.ToString("0.00") + " м, материалов на URP переведено " +
                      fixedMats + ", здоровье " + Hp + ", уровень " + Level + ", броня " + Armor + ".");
        }
    }
}
